using Courier.Core.Domain;
using Courier.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Courier.Data;

/// <summary>One message that was sent, as the history screen lists it.</summary>
public sealed record SentBatch(
    Guid Id,
    DateTimeOffset At,
    string? Subject,
    string Body,
    string Audience,
    int Sent,
    int Failed,
    int Skipped)
{
    public int Total => Sent + Failed + Skipped;

    /// <summary>"16 Sep 2026, 6:42 PM", in the reader's own time zone.</summary>
    public string When => At.ToLocalTime().ToString("d MMM yyyy, h:mm tt");

    public string Headline => string.IsNullOrWhiteSpace(Subject) ? FirstLine : Subject!;

    private string FirstLine
    {
        get
        {
            var line = Body.Split('\n')[0].Trim();
            return line.Length <= 80 ? line : line[..80].TrimEnd() + "…";
        }
    }

    public string Outcome => Failed == 0 && Skipped == 0
        ? $"{Sent} sent"
        : $"{Sent} sent, {Failed} failed, {Skipped} skipped";
}

/// <summary>One person's copy of a message, and what became of it.</summary>
public sealed record SentDelivery(
    Guid PersonId,
    string PersonName,
    string? Ward,
    Channel Channel,
    string Address,
    DeliveryStatus Status,
    DateTimeOffset? At,
    string? Error)
{
    public string ChannelLabel => Channel switch
    {
        Channel.Email => "Email",
        Channel.Text => "Text",
        Channel.Voice => "Voice",
        _ => "—",
    };

    public string AddressForDisplay =>
        Channel is Channel.Email ? Address : PhoneFormat.ForDisplay(Address);

    public string StatusLabel => Status switch
    {
        DeliveryStatus.Sent => "Sent",
        DeliveryStatus.Failed => "Failed",
        DeliveryStatus.Skipped => "Skipped",
        _ => "Pending",
    };

    public string When => At is { } at ? at.ToLocalTime().ToString("h:mm tt") : "—";

    public bool Delivered => Status == DeliveryStatus.Sent;
    public bool Wrong => Status is DeliveryStatus.Failed;
}

/// <summary>What was sent, when, and to whom. Written as each message goes out; this
/// only reads it back.</summary>
public sealed class MessageLogService(CourierDbContext db)
{
    public Task<List<SentBatch>> RecentAsync(int take = 200, CancellationToken cancellation = default) =>
        db.MessageBatches
            .OrderByDescending(b => b.SentAt ?? b.CreatedAt)
            .Take(take)
            .Select(b => new SentBatch(
                b.Id,
                b.SentAt ?? b.CreatedAt,
                b.Subject,
                b.Body,
                b.AudienceDescription,
                b.Deliveries.Count(d => d.Status == DeliveryStatus.Sent),
                b.Deliveries.Count(d => d.Status == DeliveryStatus.Failed),
                b.Deliveries.Count(d => d.Status == DeliveryStatus.Skipped)))
            .AsNoTracking()
            .ToListAsync(cancellation);

    public Task<List<SentDelivery>> DeliveriesAsync(Guid batchId, CancellationToken cancellation = default) =>
        db.MessageDeliveries
            .Where(d => d.MessageBatchId == batchId)
            .OrderBy(d => d.Person!.LastName).ThenBy(d => d.Person!.FirstName)
            .Select(d => new SentDelivery(
                d.PersonId, d.Person!.DisplayName, d.Person.Ward,
                d.Channel, d.Address, d.Status, d.SentAt, d.Error))
            .AsNoTracking()
            .ToListAsync(cancellation);

    /// <summary>Everything ever sent to one person, newest first — the answer to "when
    /// did I last reach them, and did it work?"</summary>
    public Task<List<SentDelivery>> ForPersonAsync(Guid personId, CancellationToken cancellation = default) =>
        db.MessageDeliveries
            .Where(d => d.PersonId == personId)
            .OrderByDescending(d => d.SentAt ?? d.MessageBatch!.CreatedAt)
            .Select(d => new SentDelivery(
                d.PersonId, d.Person!.DisplayName, d.Person.Ward,
                d.Channel, d.Address, d.Status, d.SentAt, d.Error))
            .AsNoTracking()
            .ToListAsync(cancellation);

    /// <summary>The whole log as text, for pasting into a report or an email.</summary>
    public async Task<string> AsTextAsync(Guid batchId, CancellationToken cancellation = default)
    {
        var batch = await db.MessageBatches.AsNoTracking()
            .FirstOrDefaultAsync(b => b.Id == batchId, cancellation);
        if (batch is null) return "";

        var deliveries = await DeliveriesAsync(batchId, cancellation);
        var lines = deliveries.Select(d =>
            $"{d.PersonName}\t{d.ChannelLabel}\t{d.AddressForDisplay}\t{d.StatusLabel}\t{d.When}"
            + (d.Error is null ? "" : $"\t{d.Error}"));

        return string.Join(Environment.NewLine,
            [$"Sent {(batch.SentAt ?? batch.CreatedAt).ToLocalTime():d MMM yyyy, h:mm tt}",
             batch.AudienceDescription,
             "",
             batch.Body,
             "",
             .. lines]);
    }
}
