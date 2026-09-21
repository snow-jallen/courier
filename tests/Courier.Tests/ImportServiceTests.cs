using Courier.Core.Domain;
using Courier.Core.Import;
using Courier.Data;
using Courier.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Courier.Tests;

public sealed class ImportServiceTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"courier-{Guid.NewGuid():N}.db");
    private static readonly DateOnly Today = new(2026, 9, 16);
    private static readonly DateOnly Later = new(2026, 10, 14);

    private CourierDbContext Open()
    {
        var db = CourierDatabase.Open(_path);
        db.Database.Migrate();
        return db;
    }

    private static NormalizedPerson Person(
        string last, string first, string? email = null, string? phone = null,
        string ward = "Manti 2nd Ward") =>
        new(last, first, $"{last}, {first}", ward, 40, 3, 4, "1 Main",
            email, phone, phone is null ? null : "+1435555" + phone[^4..], false, ward);

    private static LcrReport Report(int rows) => new([], 29, "manti-singles.pdf", new string('a', 64));

    private static async Task<ImportRun> ImportAsync(
        CourierDbContext db, IReadOnlyList<NormalizedPerson> people, DateOnly on)
    {
        var service = new ImportService(db);
        var existing = await DirectoryService.ExistingPeople(db).ToListAsync();
        var plan = ImportPlanner.Plan(people, existing);
        return await service.ApplyAsync(Report(people.Count), plan, on);
    }

    [Fact]
    public async Task First_import_stores_everyone_with_their_contact_details()
    {
        using var db = Open();
        var run = await ImportAsync(db, [
            Person("Ashby", "Miriam", "m.ashby@example.com", "555-0111"),
            Person("Quilley", "Barnaby", phone: "555-0127"),
        ], Today);

        Assert.Equal(2, run.AddedCount);
        Assert.Equal(2, await db.People.CountAsync());

        var miriam = await db.People.Include(p => p.ContactPoints)
            .FirstAsync(p => p.LastName == "Ashby");
        Assert.Equal(2, miriam.ContactPoints.Count);
        Assert.All(miriam.ContactPoints, c => Assert.Equal(ContactSource.Lcr, c.Source));
        Assert.All(miriam.ContactPoints, c => Assert.Equal(Today, c.LastSeenInLcrOn));
        Assert.Equal(Today, miriam.FirstSeenOn);
    }

    [Fact]
    public async Task Someone_dropped_from_the_export_is_deactivated_not_deleted()
    {
        using var db = Open();
        await ImportAsync(db, [Person("Ashby", "Miriam"), Person("Peacock", "Alvin")], Today);
        var run = await ImportAsync(db, [Person("Ashby", "Miriam")], Later);

        Assert.Equal(1, run.DeactivatedCount);
        Assert.Equal(2, await db.People.CountAsync());

        var alvin = await db.People.FirstAsync(p => p.LastName == "Peacock");
        Assert.False(alvin.IsActive);
        Assert.Equal(Later, alvin.DeactivatedOn);
    }

    [Fact]
    public async Task An_import_never_changes_a_preferred_channel_or_a_note()
    {
        using var db = Open();
        await ImportAsync(db, [Person("Quilley", "Barnaby", phone: "555-0127")], Today);

        var before = await db.People.FirstAsync();
        before.PreferredChannel = Channel.Voice;
        before.Notes = "Hard of hearing — call the landline.";
        await db.SaveChangesAsync();

        await ImportAsync(db, [Person("Quilley", "Barnaby", phone: "555-0999")], Later);

        var after = await db.People.FirstAsync();
        Assert.Equal(Channel.Voice, after.PreferredChannel);
        Assert.Equal("Hard of hearing — call the landline.", after.Notes);
        Assert.Equal("555-0999", after.LcrPhone);
    }

    [Fact]
    public async Task A_locally_added_address_is_waiting_to_be_entered_in_lcr()
    {
        using var db = Open();
        await ImportAsync(db, [Person("Quilley", "Barnaby", phone: "555-0127")], Today);

        var rulon = await db.People.Include(p => p.ContactPoints).FirstAsync();
        rulon.ContactPoints.Add(new ContactPoint
        {
            PersonId = rulon.Id,
            Kind = ContactKind.Email,
            Source = ContactSource.Local,
            Value = "b.quilley@example.com",
            Normalized = "b.quilley@example.com",
            AddedOn = Today,
        });
        await db.SaveChangesAsync();

        var waiting = await OutstandingAsync(db);
        Assert.Equal("b.quilley@example.com", Assert.Single(waiting).Value);
    }

    [Fact]
    public async Task Once_lcr_has_the_address_it_drops_off_the_report_by_itself()
    {
        using var db = Open();
        await ImportAsync(db, [Person("Quilley", "Barnaby", phone: "555-0127")], Today);

        var rulon = await db.People.Include(p => p.ContactPoints).FirstAsync();
        rulon.ContactPoints.Add(new ContactPoint
        {
            PersonId = rulon.Id,
            Kind = ContactKind.Email,
            Source = ContactSource.Local,
            Value = "b.quilley@example.com",
            Normalized = "b.quilley@example.com",
            AddedOn = Today,
        });
        await db.SaveChangesAsync();

        // The next export now carries the address someone typed in by hand.
        await ImportAsync(db, [Person("Quilley", "Barnaby", "b.quilley@example.com", "555-0127")], Later);

        Assert.Empty(await OutstandingAsync(db));

        // It is marked as seen, not duplicated.
        var emails = await db.ContactPoints.Where(c => c.Kind == ContactKind.Email).ToListAsync();
        var email = Assert.Single(emails);
        Assert.Equal(Later, email.LastSeenInLcrOn);
        Assert.Equal(ContactSource.Local, email.Source);
    }

    [Fact]
    public async Task Every_change_is_recorded_against_the_import_that_made_it()
    {
        using var db = Open();
        await ImportAsync(db, [Person("Winslade", "Verity", phone: "555-0142")], Today);
        var run = await ImportAsync(db, [Person("Winslade", "Verity", phone: "555-0199")], Later);

        var change = Assert.Single(await db.PersonChanges.Where(c => c.ImportRunId == run.Id).ToListAsync());
        Assert.Equal(ChangeKind.Updated, change.Kind);
        Assert.Equal("Phone", change.Field);
        Assert.Equal("555-0142", change.OldValue);
        Assert.Equal("555-0199", change.NewValue);
    }

    [Fact]
    public async Task Someone_who_comes_back_keeps_the_history_they_had()
    {
        using var db = Open();
        await ImportAsync(db, [Person("Peacock", "Alvin")], Today);
        var id = (await db.People.FirstAsync()).Id;

        await ImportAsync(db, [], Later);
        await ImportAsync(db, [Person("Peacock", "Alvin")], Later.AddDays(30));

        var alvin = await db.People.FirstAsync();
        Assert.Equal(id, alvin.Id);
        Assert.True(alvin.IsActive);
        Assert.Null(alvin.DeactivatedOn);
        Assert.Equal(Today, alvin.FirstSeenOn);
    }

    private static Task<List<ContactPoint>> OutstandingAsync(CourierDbContext db) =>
        db.ContactPoints
            .Where(c => c.Source == ContactSource.Local
                     && c.LastSeenInLcrOn == null
                     && c.EnteredInLcrOn == null)
            .ToListAsync();

    public void Dispose()
    {
        // Deliberately not ClearAllPools: it is process-wide, and clearing pools while
        // another test class still holds a connection makes unrelated tests fail.
        try { if (File.Exists(_path)) File.Delete(_path); }
        catch (IOException) { /* a temp file left behind harms nothing */ }
    }
}
