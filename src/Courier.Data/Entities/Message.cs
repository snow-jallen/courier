using Courier.Core.Domain;

namespace Courier.Data.Entities;

/// <summary>One message written once and delivered across several channels.</summary>
public class MessageBatch
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? SentAt { get; set; }

    /// <summary>Used for e-mail only; texts and calls have no subject.</summary>
    public string? Subject { get; set; }
    public required string Body { get; set; }

    /// <summary>Audio played to people who prefer a call.</summary>
    public string? VoiceRecordingPath { get; set; }

    /// <summary>How the audience was chosen, in the words shown at the time,
    /// e.g. "Manti 2nd Ward (41)".</summary>
    public required string AudienceDescription { get; set; }

    public List<MessageDelivery> Deliveries { get; set; } = [];
}

public enum DeliveryStatus { Pending = 1, Sent = 2, Failed = 3, Skipped = 4 }

public class MessageDelivery
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid MessageBatchId { get; set; }
    public MessageBatch? MessageBatch { get; set; }

    public Guid PersonId { get; set; }
    public Person? Person { get; set; }

    public Channel Channel { get; set; }

    /// <summary>The address actually used, kept even if the person later changes it.</summary>
    public required string Address { get; set; }

    public DeliveryStatus Status { get; set; } = DeliveryStatus.Pending;
    public string? ProviderMessageId { get; set; }

    /// <summary>Plain-language failure, shown to the user as-is.</summary>
    public string? Error { get; set; }
    public DateTimeOffset? SentAt { get; set; }

    /// <summary>Provider charge in millionths of a dollar; null until the provider says.</summary>
    public long? CostMicros { get; set; }
}
