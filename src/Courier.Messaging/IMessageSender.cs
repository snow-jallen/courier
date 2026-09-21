using Courier.Core.Domain;

namespace Courier.Messaging;

public enum SendStatus { Sent = 1, Failed = 2, Skipped = 3 }

public sealed record SendOutcome(SendStatus Status, string? ProviderMessageId, string? Error, long? CostMicros)
{
    public static SendOutcome Sent(string? id, long? costMicros = null) => new(SendStatus.Sent, id, null, costMicros);

    /// <summary>The message is shown to the user as-is, so write it in plain language:
    /// what went wrong and what to do about it.</summary>
    public static SendOutcome Failed(string message) => new(SendStatus.Failed, null, message, null);

    public static SendOutcome Skipped(string why) => new(SendStatus.Skipped, null, why, null);
}

/// <summary>One message, in every form a channel might need.
///
/// The two voice fields are alternatives: a recording of the sender reading this
/// message, or the same words read aloud by Twilio. They belong to the message rather
/// than to the settings, because the script is the message — a recording made once and
/// kept in settings would be read out for every announcement thereafter.</summary>
public sealed record OutgoingMessage(
    string Subject,
    string Body,
    string? VoiceRecordingUrl = null,
    bool SpeakAloud = false);

/// <summary>The result of a Test button. <paramref name="Message"/> is shown to the
/// user, so it says what happened in their words, never a provider's error code.</summary>
public sealed record CredentialCheck(bool Ok, string Message)
{
    public static CredentialCheck Working(string message) => new(true, message);
    public static CredentialCheck Broken(string message) => new(false, message);
}

public interface IMessageSender
{
    Channel Channel { get; }

    /// <summary>False when the settings for this channel are missing or incomplete.</summary>
    bool IsConfigured { get; }

    Task<SendOutcome> SendAsync(string address, OutgoingMessage message, CancellationToken cancellation = default);

    /// <summary>Sends a real message to the user's own address or number, so that
    /// "Working" means it actually worked.</summary>
    Task<CredentialCheck> TestAsync(string address, CancellationToken cancellation = default);
}
