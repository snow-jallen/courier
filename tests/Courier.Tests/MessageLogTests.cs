using Courier.Core.Domain;
using Courier.Data;
using Courier.Data.Entities;
using Courier.Messaging;
using Microsoft.EntityFrameworkCore;

namespace Courier.Tests;

public sealed class MessageLogTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"courier-log-{Guid.NewGuid():N}.db");

    private sealed class FakeSender(Channel channel, SendOutcome? outcome = null) : IMessageSender
    {
        public Channel Channel { get; } = channel;
        public bool IsConfigured => true;

        public Task<SendOutcome> SendAsync(string address, OutgoingMessage message, CancellationToken ct) =>
            Task.FromResult(outcome ?? SendOutcome.Sent("ID1"));

        public Task<CredentialCheck> TestAsync(string address, CancellationToken ct) =>
            Task.FromResult(CredentialCheck.Working("fine"));
    }

    private CourierDbContext Open()
    {
        var db = CourierDatabase.Open(_path);
        db.Database.Migrate();
        return db;
    }

    private static async Task<Recipient> PersonAsync(
        CourierDbContext db, string last, Channel channel, string? email = "a@example.com",
        string? phone = "+14355550100")
    {
        var person = new Person
        {
            LastName = last, FirstName = "Test", DisplayName = $"{last}, Test",
            Ward = "Manti 2nd Ward", PreferredChannel = channel,
            FirstSeenOn = new DateOnly(2026, 9, 16), LastSeenOn = new DateOnly(2026, 9, 16),
        };
        db.People.Add(person);
        await db.SaveChangesAsync();
        return new Recipient(person.Id, last, "Test", person.DisplayName, person.Ward, 40, 3, 4,
            channel, email, phone, true);
    }

    private static BroadcastService Broadcast(CourierDbContext db, SendOutcome? outcome = null) =>
        new(db, new Dictionary<Channel, IMessageSender>
        {
            [Channel.Email] = new FakeSender(Channel.Email, outcome),
            [Channel.Text] = new FakeSender(Channel.Text, outcome),
        });

    [Fact]
    public async Task Every_send_is_listed_afterwards_with_what_it_said_and_who_it_went_to()
    {
        using var db = Open();
        var people = new[]
        {
            await PersonAsync(db, "Ashgrove", Channel.Email),
            await PersonAsync(db, "Quilley", Channel.Text),
        };

        await Broadcast(db).SendAsync("Stake dinner", "Friday at 6:30.", "Everyone active (2)", people);

        var log = new MessageLogService(db);
        var batch = Assert.Single(await log.RecentAsync());

        Assert.Equal("Stake dinner", batch.Headline);
        Assert.Equal("Everyone active (2)", batch.Audience);
        Assert.Equal(2, batch.Sent);
        Assert.Equal("2 sent", batch.Outcome);

        var deliveries = await log.DeliveriesAsync(batch.Id);
        Assert.Equal(["Ashgrove, Test", "Quilley, Test"], deliveries.Select(d => d.PersonName));
        Assert.Equal("Email", deliveries[0].ChannelLabel);
        Assert.Equal("(435) 555-0100", deliveries[1].AddressForDisplay);
        Assert.All(deliveries, d => Assert.True(d.Delivered));
    }

    [Fact]
    public async Task A_message_with_no_subject_is_listed_by_its_first_line()
    {
        using var db = Open();
        await Broadcast(db).SendAsync("", "Dinner has moved to Thursday.\nSame time.", "Everyone (1)",
            [await PersonAsync(db, "Ashgrove", Channel.Email)]);

        Assert.Equal("Dinner has moved to Thursday.", (await new MessageLogService(db).RecentAsync())[0].Headline);
    }

    [Fact]
    public async Task What_went_wrong_is_kept_in_the_words_the_user_was_shown()
    {
        using var db = Open();
        var failing = Broadcast(db, SendOutcome.Failed("Gmail needs an app password."));

        await failing.SendAsync("Dinner", "Friday.", "Everyone (1)",
            [await PersonAsync(db, "Ashgrove", Channel.Email)]);

        var log = new MessageLogService(db);
        var batch = (await log.RecentAsync())[0];
        Assert.Equal("0 sent, 1 failed, 0 skipped", batch.Outcome);

        var delivery = Assert.Single(await log.DeliveriesAsync(batch.Id));
        Assert.True(delivery.Wrong);
        Assert.Equal("Gmail needs an app password.", delivery.Error);
    }

    [Fact]
    public async Task Somebody_who_could_not_be_reached_is_recorded_as_skipped_not_sent()
    {
        using var db = Open();
        await Broadcast(db).SendAsync("Dinner", "Friday.", "Everyone (1)",
            [await PersonAsync(db, "Yardley", Channel.None, email: null, phone: null)]);

        var log = new MessageLogService(db);
        var delivery = Assert.Single(await log.DeliveriesAsync((await log.RecentAsync())[0].Id));

        Assert.Equal("Skipped", delivery.StatusLabel);
        Assert.False(delivery.Delivered);
        Assert.Contains("Nobody has chosen", delivery.Error!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task One_person_s_whole_history_can_be_read_back_newest_first()
    {
        using var db = Open();
        var person = await PersonAsync(db, "Ashgrove", Channel.Email);

        await Broadcast(db).SendAsync("First", "One.", "Everyone (1)", [person]);
        await Broadcast(db).SendAsync("Second", "Two.", "Everyone (1)", [person]);

        var history = await new MessageLogService(db).ForPersonAsync(person.Id);

        Assert.Equal(2, history.Count);
        Assert.All(history, d => Assert.Equal("Ashgrove, Test", d.PersonName));
    }

    [Fact]
    public async Task Newest_sends_are_listed_first()
    {
        using var db = Open();
        var person = await PersonAsync(db, "Ashgrove", Channel.Email);

        await Broadcast(db).SendAsync("Older", "One.", "Everyone (1)", [person]);
        await Broadcast(db).SendAsync("Newer", "Two.", "Everyone (1)", [person]);

        Assert.Equal(["Newer", "Older"], (await new MessageLogService(db).RecentAsync()).Select(b => b.Headline));
    }

    [Fact]
    public async Task The_record_of_a_send_can_be_copied_out_as_text()
    {
        using var db = Open();
        await Broadcast(db).SendAsync("Stake dinner", "Friday at 6:30.", "Manti 2nd Ward (1)",
            [await PersonAsync(db, "Ashgrove", Channel.Email)]);

        var log = new MessageLogService(db);
        var text = await log.AsTextAsync((await log.RecentAsync())[0].Id);

        Assert.Contains("Manti 2nd Ward (1)", text, StringComparison.Ordinal);
        Assert.Contains("Friday at 6:30.", text, StringComparison.Ordinal);
        Assert.Contains("Ashgrove, Test", text, StringComparison.Ordinal);
        Assert.Contains("Sent", text, StringComparison.Ordinal);
    }

    public void Dispose()
    {
        try { if (File.Exists(_path)) File.Delete(_path); }
        catch (IOException) { }
    }
}
