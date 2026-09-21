using Courier.Core.Domain;
using Courier.Data;
using Courier.Data.Entities;
using Courier.Messaging;
using Microsoft.EntityFrameworkCore;

namespace Courier.Tests;

public sealed class BroadcastServiceTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"courier-send-{Guid.NewGuid():N}.db");

    private sealed class FakeSender(Channel channel, SendOutcome? outcome = null) : IMessageSender
    {
        public List<string> SentTo { get; } = [];
        public Channel Channel { get; } = channel;
        public bool IsConfigured => true;

        public Task<SendOutcome> SendAsync(string address, OutgoingMessage message, CancellationToken ct)
        {
            SentTo.Add(address);
            return Task.FromResult(outcome ?? SendOutcome.Sent("ID" + SentTo.Count));
        }

        public Task<CredentialCheck> TestAsync(string address, CancellationToken ct) =>
            Task.FromResult(CredentialCheck.Working("fine"));
    }

    private CourierDbContext Open()
    {
        var db = CourierDatabase.Open(_path);
        db.Database.Migrate();
        return db;
    }

    /// <summary>A delivery hangs off a real person, so the people have to be in the
    /// database before anything can be sent to them.</summary>
    private static async Task<Recipient> PersonAsync(
        CourierDbContext db, string last, Channel channel = Channel.Email,
        string? email = "someone@example.com", string? phone = "+14355550100")
    {
        var person = new Courier.Data.Entities.Person
        {
            LastName = last,
            FirstName = "Test",
            DisplayName = $"{last}, Test",
            Ward = "Manti 2nd Ward",
            Age = 40,
            BirthMonth = 3,
            BirthDay = 4,
            PreferredChannel = channel,
            FirstSeenOn = new DateOnly(2026, 9, 16),
            LastSeenOn = new DateOnly(2026, 9, 16),
        };
        db.People.Add(person);
        await db.SaveChangesAsync();

        return new Recipient(person.Id, last, "Test", person.DisplayName, person.Ward, 40, 3, 4,
            channel, email, phone, true);
    }

    [Fact]
    public async Task Each_person_is_sent_to_on_the_channel_they_chose()
    {
        using var db = Open();
        var email = new FakeSender(Channel.Email);
        var text = new FakeSender(Channel.Text);
        var service = new BroadcastService(db,
            new Dictionary<Channel, IMessageSender> { [Channel.Email] = email, [Channel.Text] = text });

        await service.SendAsync("Dinner", "Friday at 6:30", "Everyone (2)",
            [await PersonAsync(db, "Mail", Channel.Email), await PersonAsync(db, "Text", Channel.Text)]);

        Assert.Equal(["someone@example.com"], email.SentTo);
        Assert.Equal(["+14355550100"], text.SentTo);
    }

    [Fact]
    public async Task An_override_sends_everyone_the_same_way_whatever_they_chose()
    {
        using var db = Open();
        var email = new FakeSender(Channel.Email);
        var text = new FakeSender(Channel.Text);
        var service = new BroadcastService(db,
            new Dictionary<Channel, IMessageSender> { [Channel.Email] = email, [Channel.Text] = text });

        await service.SendAsync("Urgent", "Moved to Thursday", "Everyone, all by text (2)",
            [await PersonAsync(db, "Mail", Channel.Email), await PersonAsync(db, "None", Channel.None)],
            via: Channel.Text);

        Assert.Empty(email.SentTo);
        Assert.Equal(2, text.SentTo.Count);
        Assert.All(await db.MessageDeliveries.ToListAsync(),
            d => Assert.Equal(Channel.Text, d.Channel));
    }

    [Fact]
    public async Task Someone_unreachable_is_recorded_as_skipped_with_the_reason()
    {
        using var db = Open();
        var service = new BroadcastService(db,
            new Dictionary<Channel, IMessageSender> { [Channel.Email] = new FakeSender(Channel.Email) });

        await service.SendAsync("Dinner", "Friday", "Everyone (1)",
            [await PersonAsync(db, "NoEmail", Channel.Email, email: null)]);

        var delivery = await db.MessageDeliveries.SingleAsync();
        Assert.Equal(DeliveryStatus.Skipped, delivery.Status);
        Assert.Contains("no address", delivery.Error!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task A_failure_is_written_down_in_the_words_the_user_will_read()
    {
        using var db = Open();
        var failing = new FakeSender(Channel.Email, SendOutcome.Failed("Gmail needs an app password."));
        var service = new BroadcastService(db,
            new Dictionary<Channel, IMessageSender> { [Channel.Email] = failing });

        await service.SendAsync("Dinner", "Friday", "Everyone (1)", [await PersonAsync(db, "Mail")]);

        var delivery = await db.MessageDeliveries.SingleAsync();
        Assert.Equal(DeliveryStatus.Failed, delivery.Status);
        Assert.Equal("Gmail needs an app password.", delivery.Error);
        Assert.Null(delivery.SentAt);
    }

    [Fact]
    public async Task What_already_went_out_survives_a_send_that_is_stopped_half_way()
    {
        using var db = Open();
        using var stop = new CancellationTokenSource();
        var sender = new FakeSender(Channel.Email);
        var service = new BroadcastService(db,
            new Dictionary<Channel, IMessageSender> { [Channel.Email] = sender });

        var people = new[]
        {
            await PersonAsync(db, "One"), await PersonAsync(db, "Two"), await PersonAsync(db, "Three"),
        };
        var progress = new Progress<BroadcastProgress>(p => { if (p.Done == 2) stop.Cancel(); });

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => service.SendAsync("Dinner", "Friday", "Everyone (3)", people, progress, null, stop.Token));

        // Two were really sent, and the record says so. Pretending otherwise would have
        // the user send to them twice.
        Assert.Equal(2, await db.MessageDeliveries.CountAsync(d => d.Status == DeliveryStatus.Sent));
    }

    [Fact]
    public async Task The_batch_records_what_was_written_and_who_it_went_to()
    {
        using var db = Open();
        var service = new BroadcastService(db,
            new Dictionary<Channel, IMessageSender> { [Channel.Email] = new FakeSender(Channel.Email) });

        await service.SendAsync("Stake dinner", "Friday at 6:30", "Manti 2nd Ward (1)",
            [await PersonAsync(db, "Mail")]);

        var batch = await db.MessageBatches.SingleAsync();
        Assert.Equal("Stake dinner", batch.Subject);
        Assert.Equal("Manti 2nd Ward (1)", batch.AudienceDescription);
        Assert.NotNull(batch.SentAt);
    }

    [Fact]
    public async Task A_channel_with_no_sender_is_skipped_rather_than_crashing_the_send()
    {
        using var db = Open();
        var service = new BroadcastService(db, new Dictionary<Channel, IMessageSender>());

        await service.SendAsync("Dinner", "Friday", "Everyone (1)", [await PersonAsync(db, "Mail")]);

        var delivery = await db.MessageDeliveries.SingleAsync();
        Assert.Equal(DeliveryStatus.Skipped, delivery.Status);
    }

    public void Dispose()
    {
        // Deliberately not ClearAllPools: it is process-wide, and clearing pools while
        // another test class still holds a connection makes unrelated tests fail.
        try { if (File.Exists(_path)) File.Delete(_path); }
        catch (IOException) { /* a temp file left behind harms nothing */ }
    }
}
