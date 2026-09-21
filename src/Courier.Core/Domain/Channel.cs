namespace Courier.Core.Domain;

/// <summary>How a person has asked to be contacted. Stored as a string in the
/// database so adding a channel later never renumbers the existing rows.</summary>
public enum Channel
{
    /// <summary>No preference recorded yet.</summary>
    None = 0,
    Email = 1,
    Text = 2,
    Voice = 3,
    WhatsApp = 4,
}

public static class Channels
{
    /// <summary>Channels Courier can actually deliver on today, in the order the UI shows them.</summary>
    public static readonly IReadOnlyList<Channel> Deliverable = [Channel.Email, Channel.Text, Channel.Voice];

    public static string ToWire(this Channel c) => c switch
    {
        Channel.Email => "email",
        Channel.Text => "text",
        Channel.Voice => "voice",
        Channel.WhatsApp => "whatsapp",
        _ => "none",
    };

    public static Channel FromWire(string? s) => s switch
    {
        "email" => Channel.Email,
        "text" => Channel.Text,
        "voice" => Channel.Voice,
        "whatsapp" => Channel.WhatsApp,
        _ => Channel.None,
    };
}
