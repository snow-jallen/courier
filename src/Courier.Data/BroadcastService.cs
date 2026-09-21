using Courier.Core.Domain;
using Courier.Data.Entities;
using Courier.Messaging;

namespace Courier.Data;

public sealed record BroadcastProgress(int Done, int Total, string Who);

/// <summary>Sends one message to a chosen set of people and writes down what happened
/// to each of them.</summary>
public sealed class BroadcastService(CourierDbContext db, IReadOnlyDictionary<Channel, IMessageSender> senders)
{
    /// <summary>Sends, recording every delivery as it goes.
    ///
    /// Each person is saved as their own row before the next is attempted, so a send
    /// that is interrupted half way leaves an honest record rather than nothing: the
    /// messages already sent cannot be unsent, and the user has to be able to see which
    /// those were.</summary>
    public async Task<MessageBatch> SendAsync(
        string subject,
        string body,
        string audienceDescription,
        IReadOnlyList<Recipient> chosen,
        IProgress<BroadcastProgress>? progress = null,
        CancellationToken cancellation = default)
    {
        var batch = new MessageBatch
        {
            Subject = string.IsNullOrWhiteSpace(subject) ? null : subject,
            Body = body,
            AudienceDescription = audienceDescription,
        };
        db.MessageBatches.Add(batch);
        await db.SaveChangesAsync(cancellation);

        var done = 0;
        foreach (var person in chosen)
        {
            cancellation.ThrowIfCancellationRequested();

            var reach = person.Reachability;
            var delivery = new MessageDelivery
            {
                MessageBatchId = batch.Id,
                PersonId = person.Id,
                Channel = reach.Channel,
                Address = reach.Address ?? "",
            };

            if (!reach.CanReceive)
            {
                delivery.Status = DeliveryStatus.Skipped;
                delivery.Error = Explain(reach.Reason);
            }
            else if (!senders.TryGetValue(reach.Channel, out var sender))
            {
                delivery.Status = DeliveryStatus.Skipped;
                delivery.Error = $"Courier cannot send by {reach.Channel.ToWire()} yet.";
            }
            else
            {
                var outcome = await sender.SendAsync(
                    reach.Address!, new OutgoingMessage(subject, body), cancellation);

                delivery.Status = outcome.Status switch
                {
                    SendStatus.Sent => DeliveryStatus.Sent,
                    SendStatus.Skipped => DeliveryStatus.Skipped,
                    _ => DeliveryStatus.Failed,
                };
                delivery.ProviderMessageId = outcome.ProviderMessageId;
                delivery.Error = outcome.Error;
                delivery.CostMicros = outcome.CostMicros;
                if (outcome.Status == SendStatus.Sent) delivery.SentAt = DateTimeOffset.UtcNow;
            }

            db.MessageDeliveries.Add(delivery);
            await db.SaveChangesAsync(cancellation);

            progress?.Report(new BroadcastProgress(++done, chosen.Count, person.FullName));
        }

        batch.SentAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellation);
        return batch;
    }

    private static string Explain(UnreachableReason reason) => reason switch
    {
        UnreachableReason.NoChannelChosen => "Nobody has chosen how to contact this person yet.",
        UnreachableReason.MissingAddress => "There is no address or number for the channel they prefer.",
        UnreachableReason.ChannelNotSupported => "Courier cannot send on the channel they prefer yet.",
        UnreachableReason.NotInDirectory => "This person is no longer in the directory.",
        _ => "This person could not be reached.",
    };
}
