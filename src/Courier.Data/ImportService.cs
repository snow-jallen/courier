using Courier.Core.Import;
using Courier.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Courier.Data;

/// <summary>Reads a report, works out what it would change, and — only when asked —
/// applies it in one transaction.</summary>
public sealed class ImportService(CourierDbContext db)
{
    public async Task<(LcrReport Report, ImportPlan Plan)> PrepareAsync(
        string pdfPath, CancellationToken cancellation = default)
    {
        var report = LcrReportParser.Parse(pdfPath);
        var incoming = report.Rows.Select(r => LcrNormalizer.Normalize(r)).ToList();

        var existing = await DirectoryService.ExistingPeople(db).ToListAsync(cancellation);

        return (report, ImportPlanner.Plan(incoming, existing, report.Source));
    }

    /// <summary>Writes the plan. Everything lands or nothing does.</summary>
    public async Task<ImportRun> ApplyAsync(
        LcrReport report, ImportPlan plan, DateOnly today, CancellationToken cancellation = default)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellation);

        var run = new ImportRun
        {
            FileName = report.FileName,
            Sha256 = report.Sha256,
            PageCount = report.PageCount,
            RowCount = report.Rows.Count,
            AddedCount = plan.Added.Count,
            UpdatedCount = plan.Updated.Count,
            DeactivatedCount = plan.Deactivated.Count,
            ReactivatedCount = plan.Reactivated.Count,
            UnchangedCount = plan.Unchanged,
        };
        db.ImportRuns.Add(run);

        foreach (var incoming in plan.Added)
        {
            var person = new Person
            {
                LastName = incoming.LastName,
                FirstName = incoming.FirstName,
                DisplayName = incoming.DisplayName,
                FirstSeenOn = today,
                LastSeenOn = today,
            };
            ApplyFields(person, incoming, report.Source, today);
            db.People.Add(person);
            run.Changes.Add(new PersonChange { PersonId = person.Id, Kind = ChangeKind.Added });
            await SyncLcrContactAsync(person, incoming, report.Source, today, cancellation);
        }

        foreach (var update in plan.Updated)
        {
            var person = await LoadAsync(update.Existing.Id, cancellation);
            ApplyFields(person, update.Incoming, report.Source, today);
            person.LastSeenOn = today;
            foreach (var change in update.Changes)
                run.Changes.Add(new PersonChange
                {
                    PersonId = person.Id,
                    Kind = ChangeKind.Updated,
                    Field = change.Field,
                    OldValue = change.From,
                    NewValue = change.To,
                });
            await SyncLcrContactAsync(person, update.Incoming, report.Source, today, cancellation);
        }

        foreach (var returning in plan.Reactivated)
        {
            var person = await LoadAsync(returning.Existing.Id, cancellation);
            person.IsActive = true;
            person.DeactivatedOn = null;
            person.LastSeenOn = today;
            ApplyFields(person, returning.Incoming, report.Source, today);
            run.Changes.Add(new PersonChange { PersonId = person.Id, Kind = ChangeKind.Reactivated });
            await SyncLcrContactAsync(person, returning.Incoming, report.Source, today, cancellation);
        }

        foreach (var gone in plan.Deactivated)
        {
            var person = await LoadAsync(gone.Id, cancellation);
            person.IsActive = false;
            person.DeactivatedOn = today;
            person.UpdatedAt = DateTimeOffset.UtcNow;
            run.Changes.Add(new PersonChange { PersonId = person.Id, Kind = ChangeKind.Deactivated });
        }

        await db.SaveChangesAsync(cancellation);
        await transaction.CommitAsync(cancellation);
        return run;
    }

    private Task<Person> LoadAsync(Guid id, CancellationToken cancellation) =>
        db.People.Include(p => p.ContactPoints).FirstAsync(p => p.Id == id, cancellation);

    /// <summary>Copies the fields this report prints. Preferred channel and notes are
    /// untouched by design, and so is any field the report has no column for: it has
    /// said nothing about them, and silence is not an instruction to blank them.</summary>
    private static void ApplyFields(Person person, NormalizedPerson incoming, ReportSource source, DateOnly today)
    {
        person.DisplayName = incoming.DisplayName;
        if (source.Carry(ReportFields.Unit)) person.Ward = incoming.Ward;
        if (source.Carry(ReportFields.Age)) person.Age = incoming.Age;
        if (source.Carry(ReportFields.Birthday))
        {
            person.BirthMonth = incoming.BirthMonth;
            person.BirthDay = incoming.BirthDay;
        }
        if (source.Carry(ReportFields.Address)) person.Address = incoming.Address;
        if (source.Carry(ReportFields.Email)) person.LcrEmail = incoming.Email;
        if (source.Carry(ReportFields.Phone)) person.LcrPhone = incoming.PhoneRaw;

        person.LastSeenOn = today;
        person.UpdatedAt = DateTimeOffset.UtcNow;

        // The export now carries them, so they are LCR's to manage from here.
        person.Source = PersonSource.Lcr;
    }

    /// <summary>Keeps the contact points in step with what LCR printed.
    ///
    /// If a value someone added locally now appears in the export, it is marked as
    /// seen rather than duplicated — which is what quietly clears it from the
    /// "to enter in LCR" report once the entry has been made.
    /// </summary>
    private async Task SyncLcrContactAsync(
        Person person, NormalizedPerson incoming, ReportSource source, DateOnly today, CancellationToken cancellation)
    {
        if (source.Carry(ReportFields.Email))
            await UpsertAsync(person, ContactKind.Email, incoming.Email, Normalize(incoming.Email),
                false, today, cancellation);
        if (source.Carry(ReportFields.Phone))
            await UpsertAsync(person, ContactKind.Phone, incoming.PhoneRaw, incoming.PhoneE164,
                incoming.AreaCodeAssumed, today, cancellation);
    }

    private async Task UpsertAsync(
        Person person, ContactKind kind, string? value, string? normalized,
        bool areaCodeAssumed, DateOnly today, CancellationToken cancellation)
    {
        if (value is null) return;

        if (db.Entry(person).State != EntityState.Added)
            await db.Entry(person).Collection(p => p.ContactPoints).LoadAsync(cancellation);

        var existing = person.ContactPoints.FirstOrDefault(c =>
            c.Kind == kind && string.Equals(c.Normalized ?? c.Value, normalized ?? value, StringComparison.OrdinalIgnoreCase));

        if (existing is not null)
        {
            existing.LastSeenInLcrOn = today;
            existing.Value = value;
            existing.Normalized = normalized;
            existing.AreaCodeAssumed = areaCodeAssumed;
            return;
        }

        person.ContactPoints.Add(new ContactPoint
        {
            PersonId = person.Id,
            Kind = kind,
            Source = ContactSource.Lcr,
            Value = value,
            Normalized = normalized,
            AreaCodeAssumed = areaCodeAssumed,
            AddedOn = today,
            LastSeenInLcrOn = today,
            IsPreferred = !person.ContactPoints.Any(c => c.Kind == kind && c.IsPreferred),
        });
    }

    private static string? Normalize(string? email) => email?.Trim().ToLowerInvariant();
}
