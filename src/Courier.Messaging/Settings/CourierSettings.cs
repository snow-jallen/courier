using System.Text.Json.Serialization;
using Courier.Core.Domain;

namespace Courier.Messaging.Settings;

public sealed record EmailSettings
{
    public string Address { get; init; } = "";

    /// <summary>Not the account password — the 16-character one Google issues per app.</summary>
    public string AppPassword { get; init; } = "";

    public string DisplayName { get; init; } = "";
    public string Host { get; init; } = "smtp.gmail.com";
    public int Port { get; init; } = 587;

    public bool IsComplete => Address.Length > 0 && AppPassword.Length > 0 && Host.Length > 0;
}

public sealed record TwilioSettings
{
    public string AccountSid { get; init; } = "";
    public string AuthToken { get; init; } = "";

    /// <summary>The Twilio number texts and calls come from, in E.164.</summary>
    public string FromNumber { get; init; } = "";

    /// <summary>Where the Test buttons send to — the user's own phone, in E.164.</summary>
    public string TestNumber { get; init; } = "";

    /// <summary>A Twilio Messaging Service with an RCS sender attached, if there is one.
    ///
    /// Sending through the service rather than the bare number is what turns a plain
    /// text into RCS — a named, verified sender with real formatting — for the people
    /// whose phones support it. Twilio falls back to SMS from the same request for
    /// everyone else, so nobody receives less than they do today. Empty means texts go
    /// out from <see cref="FromNumber"/> as ordinary SMS.</summary>
    public string MessagingServiceSid { get; init; } = "";

    public bool SendsRichText => MessagingServiceSid.Trim().Length > 0;

    /// <summary>Twilio-hosted URL of the recording played to people who prefer a call.
    /// Produced by having Courier ring the user and record them, so no file ever needs
    /// hosting anywhere.</summary>
    public string VoiceRecordingUrl { get; init; } = "";

    public bool IsComplete =>
        AccountSid.Length > 0 && AuthToken.Length > 0
        && (FromNumber.Length > 0 || MessagingServiceSid.Length > 0);
}

/// <summary>Where texts actually leave from.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<TextTransport>))]
public enum TextTransport
{
    /// <summary>A messaging service. Reaches everybody, costs money, and arrives from a
    /// number nobody recognises.</summary>
    Twilio = 1,

    /// <summary>The Messages app on this Mac, which sends over the user's own line —
    /// their real number, replies in their own Messages app. Meant for a ward or a
    /// committee rather than the whole directory. iPhone owners with a Mac.</summary>
    MacMessages = 2,

    /// <summary>A gateway app on the user's own Android phone, which Courier asks over
    /// the local network. Same idea as the Mac route and the same limits, but it works
    /// from any computer and needs no Mac.</summary>
    AndroidGateway = 3,
}

/// <summary>The SMS Gateway app running on the user's Android phone, in local-server
/// mode — the phone answers on the home network and nothing leaves it for anyone
/// else's server, which matters when the payload is 427 people's phone numbers.</summary>
public sealed record AndroidGatewaySettings
{
    /// <summary>What the app shows as its local address, e.g. http://192.168.1.44:8080</summary>
    public string BaseUrl { get; init; } = "";

    public string Username { get; init; } = "";
    public string Password { get; init; } = "";

    public bool IsComplete => BaseUrl.Trim().Length > 0 && Username.Length > 0 && Password.Length > 0;
}

public sealed record CourierSettings
{
    public TextTransport TextVia { get; init; } = TextTransport.Twilio;

    /// <summary>Whose messages these are. Courier augments one person's calling rather
    /// than speaking for the stake, so every message says who sent it.</summary>
    public SenderIdentity Sender { get; init; } = SenderIdentity.Unknown;

    public EmailSettings Email { get; init; } = new();
    public AndroidGatewaySettings AndroidGateway { get; init; } = new();
    public TwilioSettings Twilio { get; init; } = new();

    /// <summary>Supplied when the report prints a seven-digit number. Sanpete County.</summary>
    public string DefaultAreaCode { get; init; } = "435";
}

public interface ISettingsStore
{
    string Path { get; }
    CourierSettings Load();
    void Save(CourierSettings settings);
}
