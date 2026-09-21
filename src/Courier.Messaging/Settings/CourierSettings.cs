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

    /// <summary>Twilio-hosted URL of the recording played to people who prefer a call.
    /// Produced by having Courier ring the user and record them, so no file ever needs
    /// hosting anywhere.</summary>
    public string VoiceRecordingUrl { get; init; } = "";

    public bool IsComplete => AccountSid.Length > 0 && AuthToken.Length > 0 && FromNumber.Length > 0;
}

public sealed record CourierSettings
{
    public EmailSettings Email { get; init; } = new();
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
