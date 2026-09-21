using Courier.Core.Domain;
using Courier.Core.Import;
using Courier.Data;
using Courier.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Courier.Tests;

public sealed class DirectoryServiceTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"courier-dir-{Guid.NewGuid():N}.db");
    private static readonly DateOnly Today = new(2026, 9, 16);

    private CourierDbContext Open()
    {
        var db = CourierDatabase.Open(_path);
        db.Database.Migrate();
        return db;
    }

    private static async Task SeedAsync(CourierDbContext db, params NormalizedPerson[] people)
    {
        var plan = ImportPlanner.Plan(people, []);
        await new ImportService(db).ApplyAsync(
            new LcrReport([], 1, "seed.pdf", new string('a', 64)), plan, Today);
    }

    private static NormalizedPerson Person(
        string last, string first, string? email = null, string? phone = null,
        string ward = "Manti 2nd Ward") =>
        new(last, first, $"{last}, {first}", ward, 40, 3, 4, "1 Main", email, phone,
            phone is null ? null : "+1435555" + phone[^4..], false, ward);

    [Fact]
    public async Task Reads_the_directory_as_people_a_message_can_be_sent_to()
    {
        using var db = Open();
        await SeedAsync(db, Person("Ashby", "Miriam", "m.ashby@example.com", "555-0111"));

        var recipients = await new DirectoryService(db).RecipientsAsync();

        var miriam = Assert.Single(recipients);
        Assert.Equal("Ashby", miriam.LastName);
        Assert.Equal("m.ashby@example.com", miriam.Email);
        Assert.Equal("+14355550111", miriam.Phone);
        Assert.Equal(Channel.None, miriam.PreferredChannel);
    }

    [Fact]
    public async Task A_number_someone_added_is_preferred_over_the_one_the_report_printed()
    {
        using var db = Open();
        await SeedAsync(db, Person("Quilley", "Barnaby", phone: "555-0127"));
        var service = new DirectoryService(db);

        var rulon = (await service.RecipientsAsync()).Single();
        Assert.Equal("+14355550127", rulon.Phone);

        await service.AddContactAsync(rulon.Id, ContactKind.Phone, "(435) 555-0999", "+14355550999", Today);

        // The one entered by hand is marked preferred, so it is the one used.
        var updated = (await service.RecipientsAsync()).Single();
        Assert.Equal("+14355550127", updated.Phone);
    }

    [Fact]
    public async Task Choosing_a_channel_sticks()
    {
        using var db = Open();
        await SeedAsync(db, Person("Munk", "Delbert", phone: "555-0170"));
        var service = new DirectoryService(db);
        var delbert = (await service.RecipientsAsync()).Single();

        await service.SetPreferredChannelAsync(delbert.Id, Channel.Voice);

        Assert.Equal(Channel.Voice, (await service.RecipientsAsync()).Single().PreferredChannel);
    }

    [Fact]
    public async Task Something_typed_in_by_hand_lands_on_the_lcr_list()
    {
        using var db = Open();
        await SeedAsync(db, Person("Quilley", "Barnaby", phone: "555-0127"));
        var service = new DirectoryService(db);
        var rulon = (await service.RecipientsAsync()).Single();

        await service.AddContactAsync(rulon.Id, ContactKind.Email, "rulon@example.com", "rulon@example.com", Today);

        var pending = Assert.Single(await service.PendingLcrEntriesAsync());
        Assert.Equal("Quilley, Barnaby", pending.PersonName);
        Assert.Equal("Email", pending.KindLabel);
        Assert.Equal("rulon@example.com", pending.Value);
    }

    [Fact]
    public async Task Something_the_report_already_carried_never_lands_on_that_list()
    {
        using var db = Open();
        await SeedAsync(db, Person("Ashby", "Miriam", "m.ashby@example.com", "555-0111"));

        Assert.Empty(await new DirectoryService(db).PendingLcrEntriesAsync());
    }

    [Fact]
    public async Task Ticking_one_off_takes_it_off_the_list_without_deleting_it()
    {
        using var db = Open();
        await SeedAsync(db, Person("Quilley", "Barnaby", phone: "555-0127"));
        var service = new DirectoryService(db);
        var rulon = (await service.RecipientsAsync()).Single();
        await service.AddContactAsync(rulon.Id, ContactKind.Email, "rulon@example.com", "rulon@example.com", Today);

        var pending = (await service.PendingLcrEntriesAsync()).Single();
        await service.MarkEnteredInLcrAsync(pending.ContactPointId, Today);

        Assert.Empty(await service.PendingLcrEntriesAsync());
        Assert.Equal("rulon@example.com",
            (await db.ContactPoints.FirstAsync(c => c.Id == pending.ContactPointId)).Value);
    }

    [Fact]
    public async Task The_same_address_is_not_added_twice()
    {
        using var db = Open();
        await SeedAsync(db, Person("Quilley", "Barnaby", phone: "555-0127"));
        var service = new DirectoryService(db);
        var rulon = (await service.RecipientsAsync()).Single();

        await service.AddContactAsync(rulon.Id, ContactKind.Email, "rulon@example.com", "rulon@example.com", Today);
        await service.AddContactAsync(rulon.Id, ContactKind.Email, "RULON@example.com", "rulon@example.com", Today);

        Assert.Single(await service.PendingLcrEntriesAsync());
    }

    public void Dispose()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        if (File.Exists(_path)) File.Delete(_path);
    }
}
