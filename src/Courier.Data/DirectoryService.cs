using Courier.Core.Domain;
using Courier.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Courier.Data;

/// <summary>One contact detail someone added that LCR has never carried — a row of
/// the "to enter in LCR" list.</summary>
public sealed record PendingEntry(
    Guid ContactPointId, Guid PersonId, string PersonName, string? Ward,
    ContactKind Kind, string Value, DateOnly AddedOn)
{
    public string KindLabel => Kind == ContactKind.Email ? "Email" : "Phone";
}

/// <summary>Reading and editing the directory. The screens talk to this; nothing in
/// the user interface touches a DbContext.</summary>
public sealed class DirectoryService(CourierDbContext db)
{
    public async Task<IReadOnlyList<Recipient>> RecipientsAsync(CancellationToken cancellation = default)
    {
        var people = await db.People
            .Include(p => p.ContactPoints)
            .AsNoTracking()
            .ToListAsync(cancellation);

        return people.Select(ToRecipient).ToList();
    }

    /// <summary>The address Courier would actually use, preferring one someone chose
    /// over whatever the last import happened to print.</summary>
    private static Recipient ToRecipient(Person p)
    {
        var email = Best(p, ContactKind.Email)?.Value ?? p.LcrEmail;
        var phone = Best(p, ContactKind.Phone)?.Normalized;

        return new Recipient(
            p.Id, p.LastName, p.FirstName, p.DisplayName, p.Ward, p.Age,
            p.BirthMonth, p.BirthDay, p.PreferredChannel, email, phone, p.IsActive);
    }

    private static ContactPoint? Best(Person p, ContactKind kind) =>
        p.ContactPoints.Where(c => c.Kind == kind)
            .OrderByDescending(c => c.IsPreferred)
            .ThenByDescending(c => c.AddedOn)
            .FirstOrDefault();

    /// <summary>Courier's own field. No import may write it, so it is saved on its own.</summary>
    public async Task SetPreferredChannelAsync(
        Guid personId, Channel channel, CancellationToken cancellation = default)
    {
        var person = await db.People.FirstAsync(p => p.Id == personId, cancellation);
        person.PreferredChannel = channel;
        person.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellation);
    }

    public async Task AddContactAsync(
        Guid personId, ContactKind kind, string value, string? normalized,
        DateOnly today, bool areaCodeAssumed = false, CancellationToken cancellation = default)
    {
        var person = await db.People.Include(p => p.ContactPoints)
            .FirstAsync(p => p.Id == personId, cancellation);

        var already = person.ContactPoints.FirstOrDefault(c =>
            c.Kind == kind
            && string.Equals(c.Normalized ?? c.Value, normalized ?? value, StringComparison.OrdinalIgnoreCase));
        if (already is not null) return;

        person.ContactPoints.Add(new ContactPoint
        {
            PersonId = person.Id,
            Kind = kind,
            Source = ContactSource.Local,
            Value = value,
            Normalized = normalized,
            AreaCodeAssumed = areaCodeAssumed,
            AddedOn = today,
            IsPreferred = !person.ContactPoints.Any(c => c.Kind == kind && c.IsPreferred),
        });
        person.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellation);
    }

    /// <summary>Everything supplied locally that no export has carried and nobody has
    /// ticked off yet.</summary>
    public async Task<IReadOnlyList<PendingEntry>> PendingLcrEntriesAsync(
        CancellationToken cancellation = default)
    {
        return await db.ContactPoints
            .Where(c => c.Source == ContactSource.Local
                     && c.LastSeenInLcrOn == null
                     && c.EnteredInLcrOn == null)
            .Include(c => c.Person)
            .OrderBy(c => c.Person!.LastName).ThenBy(c => c.Person!.FirstName)
            .AsNoTracking()
            .Select(c => new PendingEntry(
                c.Id, c.PersonId, c.Person!.DisplayName, c.Person.Ward, c.Kind, c.Value, c.AddedOn))
            .ToListAsync(cancellation);
    }

    /// <summary>Ticks one off by hand. A later export carrying the same value clears it
    /// on its own, so this is only for closing the loop sooner.</summary>
    public async Task MarkEnteredInLcrAsync(
        Guid contactPointId, DateOnly on, CancellationToken cancellation = default)
    {
        var contact = await db.ContactPoints.FirstAsync(c => c.Id == contactPointId, cancellation);
        contact.EnteredInLcrOn = on;
        await db.SaveChangesAsync(cancellation);
    }

    public Task<int> ActiveCountAsync(CancellationToken cancellation = default) =>
        db.People.CountAsync(p => p.IsActive, cancellation);

    public async Task<DateTimeOffset?> LastImportAtAsync(CancellationToken cancellation = default) =>
        await db.ImportRuns.OrderByDescending(r => r.ImportedAt)
            .Select(r => (DateTimeOffset?)r.ImportedAt).FirstOrDefaultAsync(cancellation);
}
