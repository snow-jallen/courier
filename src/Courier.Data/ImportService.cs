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

        var existing = await db.People
            .Select(p => new ExistingPerson(
                p.Id, p.LastName, p.FirstName, p.BirthMonth, p.BirthDay,
                p.Ward, p.Age, p.Address, p.LcrEmail, p.LcrPhone, p.IsActive))
            .ToListAsync(cancellation);

        return (report, ImportPlanner.Plan(incoming, existing));
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
            ApplyFields(person, incoming, today);
            db.People.Add(person);
            run.Changes.Add(new PersonChange { PersonId = person.Id, Kind = ChangeKind.Added });
            await SyncLcrContactAsync(person, incoming, today, cancellation);
        }

        foreach (var update in plan.Updated)
        {
            var person = await LoadAsync(update.Existing.Id, cancellation);
            ApplyFields(person, update.Incoming, today);
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
            await SyncLcrContactAsync(person, update.Incoming, today, cancellation);
        }

        foreach (var returning in plan.Reactivated)
        {
            var person = await LoadAsync(returning.Existing.Id, cancellation);
            person.IsActive = true;
            person.DeactivatedOn = null;
            person.LastSeenOn = today;
            ApplyFields(person, returning.Incoming, today);
            run.Changes.Add(new PersonChange { PersonId = person.Id, Kind = ChangeKind.Reactivated });
            await SyncLcrContactAsync(person, returning.Incoming, today, cancellation);
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

    /// <summary>Copies the fields an import owns. Preferred channel and notes are
    /// untouched by design.</summary>
    private static void ApplyFields(Person person, NormalizedPerson incoming, DateOnly today)
    {
        person.DisplayName = incoming.DisplayName;
        person.Ward = incoming.Ward;
        person.Age = incoming.Age;
        person.BirthMonth = incoming.BirthMonth;
        person.BirthDay = incoming.BirthDay;
        person.Address = incoming.Address;
        person.LcrEmail = incoming.Email;
        person.LcrPhone = incoming.PhoneRaw;
        person.LastSeenOn = today;
        person.UpdatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>Keeps the contact points in step with what LCR printed.
    ///
    /// If a value someone added locally now appears in the export, it is marked as
    /// seen rather than duplicated — which is what quietly clears it from the
    /// "to enter in LCR" report once the entry has been made.
    /// </summary>
    private async Task SyncLcrContactAsync(
        Person person, NormalizedPerson incoming, DateOnly today, CancellationToken cancellation)
    {
        await UpsertAsync(person, ContactKind.Email, incoming.Email, Normalize(incoming.Email), false, today, cancellation);
        await UpsertAsync(person, ContactKind.Phone, incoming.PhoneRaw, incoming.PhoneE164, incoming.AreaCodeAssumed, today, cancellation);
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
