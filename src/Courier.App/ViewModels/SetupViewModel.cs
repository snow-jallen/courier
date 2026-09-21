using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Courier.Messaging;
using Courier.Messaging.Email;
using Courier.Messaging.Settings;
using Courier.Messaging.Android;
using Courier.Messaging.Mac;
using Courier.Messaging.Twilio;
using Courier.Core.Domain;
using Courier.Data;

namespace Courier.App.ViewModels;

public sealed partial class SetupViewModel : ObservableObject
{
    private readonly AppServices _services;
    private readonly ISettingsStore _store;

    private readonly bool _isMac;

    /// <summary><paramref name="isMac"/> is only passed by tests; the app reads the
    /// platform it is actually running on.</summary>
    public SetupViewModel(AppServices services, ISettingsStore store, bool? isMac = null)
    {
        _services = services;
        _store = store;
        _isMac = isMac ?? OperatingSystem.IsMacOS();

        var settings = store.Load();
        _senderName = settings.Sender.Name;
        _senderCalling = settings.Sender.Calling;
        _emailAddress = settings.Email.Address;
        _appPassword = settings.Email.AppPassword;
        _displayName = settings.Email.DisplayName;
        _accountSid = settings.Twilio.AccountSid;
        _authToken = settings.Twilio.AuthToken;
        _fromNumber = settings.Twilio.FromNumber;
        _testNumber = settings.Twilio.TestNumber;
        _messagingServiceSid = settings.Twilio.MessagingServiceSid;
        _textVia = settings.TextVia;
        _gatewayUrl = settings.AndroidGateway.BaseUrl;
        _gatewayUser = settings.AndroidGateway.Username;
        _gatewayPassword = settings.AndroidGateway.Password;
        _recordingUrl = settings.Twilio.VoiceRecordingUrl;
        _databasePath = services.DatabasePath;
        _settingsPath = store.Path;
    }

    // --- where the directory lives ---------------------------------------------
    [ObservableProperty] private string _databasePath;
    [ObservableProperty] private string _settingsPath;
    [ObservableProperty] private string _backupStatus = "";

    // --- who the messages are from ------------------------------------------------
    [ObservableProperty] private string _senderName;
    [ObservableProperty] private string _senderCalling;

    public string SignaturePreview =>
        new SenderIdentity(SenderName, SenderCalling) is { IsComplete: true } sender
            ? $"\u2014 {sender.Line}"
            : "Nobody has said who these messages are from yet.";

    partial void OnSenderNameChanged(string value) => OnPropertyChanged(nameof(SignaturePreview));
    partial void OnSenderCallingChanged(string value) => OnPropertyChanged(nameof(SignaturePreview));

    // --- email -------------------------------------------------------------------
    [ObservableProperty] private string _emailAddress;
    [ObservableProperty] private string _appPassword;
    [ObservableProperty] private string _displayName;
    [ObservableProperty] private string _emailStatus = "";
    [ObservableProperty] private bool _emailOk;
    [ObservableProperty] private bool _emailBusy;

    // --- twilio ------------------------------------------------------------------
    [ObservableProperty] private string _accountSid;
    [ObservableProperty] private string _authToken;
    [ObservableProperty] private string _fromNumber;
    [ObservableProperty] private string _testNumber;
    [ObservableProperty] private string _messagingServiceSid;

    public bool SendsRichText => MessagingServiceSid.Trim().Length > 0;

    /// <summary>Where texts leave from: a service, or the user's own phone — through
    /// the Messages app on a Mac for an iPhone, or through a gateway app for an
    /// Android. Both phone routes send from the user's real number.</summary>
    [ObservableProperty] private TextTransport _textVia;

    [ObservableProperty] private string _gatewayUrl;
    [ObservableProperty] private string _gatewayUser;
    [ObservableProperty] private string _gatewayPassword;

    public bool UseTwilioForText
    {
        get => TextVia == TextTransport.Twilio;
        set { if (value) TextVia = TextTransport.Twilio; }
    }

    /// <summary>Only selectable on a Mac. The iPhone route works by driving the
    /// Messages app, which exists nowhere else — so this is refused rather than
    /// accepted and then failing at the moment somebody presses Send.</summary>
    public bool UseIphone
    {
        get => TextVia == TextTransport.MacMessages;
        set { if (value && IphoneRouteAvailable) TextVia = TextTransport.MacMessages; }
    }

    public bool UseAndroid
    {
        get => TextVia == TextTransport.AndroidGateway;
        set { if (value) TextVia = TextTransport.AndroidGateway; }
    }

    /// <summary>The iPhone route drives the Messages app, which only exists on a Mac.
    /// The Android route is an HTTP call, so it works from anywhere.</summary>
    public bool IphoneRouteAvailable => _isMac;

    /// <summary>True when the saved choice cannot work here — an iPhone route carried
    /// over from a Mac, opened on Windows or Linux.</summary>
    public bool TextRouteBlocked => TextVia == TextTransport.MacMessages && !IphoneRouteAvailable;

    public string TextRouteBlockedMessage =>
        "Texting from your iPhone needs Courier running on a Mac, because it works by asking the Messages app to send. "
        + "On this computer, choose Twilio, or your Android phone if you have one. Your iPhone setting is kept for when you are back on the Mac.";

    partial void OnTextViaChanged(TextTransport value)
    {
        foreach (var name in (string[])["UseTwilioForText", "UseIphone", "UseAndroid", "TextRouteBlocked"])
            OnPropertyChanged(name);
    }

    partial void OnMessagingServiceSidChanged(string value) => OnPropertyChanged(nameof(SendsRichText));
    [ObservableProperty] private string _recordingUrl;
    [ObservableProperty] private string _twilioStatus = "";
    [ObservableProperty] private bool _twilioOk;
    [ObservableProperty] private bool _twilioBusy;

    public bool HasRecording => RecordingUrl.Length > 0;

    private CourierSettings Current => new()
    {
        TextVia = TextVia,
        AndroidGateway = new AndroidGatewaySettings
        {
            BaseUrl = GatewayUrl.Trim(),
            Username = GatewayUser.Trim(),
            Password = GatewayPassword.Trim(),
        },
        Sender = new SenderIdentity(SenderName.Trim(), SenderCalling.Trim()),
        Email = new EmailSettings
        {
            Address = EmailAddress.Trim(),
            AppPassword = AppPassword.Trim(),

            // What a recipient sees in their inbox. Falling back to the sender's own
            // name keeps mail consistent with the way texts are signed.
            DisplayName = DisplayName.Trim().Length > 0 ? DisplayName.Trim() : SenderName.Trim(),
        },
        Twilio = new TwilioSettings
        {
            AccountSid = AccountSid.Trim(),
            AuthToken = AuthToken.Trim(),
            FromNumber = FromNumber.Trim(),
            TestNumber = TestNumber.Trim(),
            MessagingServiceSid = MessagingServiceSid.Trim(),
            VoiceRecordingUrl = RecordingUrl.Trim(),
        },
    };

    [RelayCommand]
    private void Save()
    {
        _store.Save(Current);
        EmailStatus = "Saved.";
        TwilioStatus = "Saved.";
    }

    [RelayCommand]
    private async Task TestEmailAsync()
    {
        EmailBusy = true;
        EmailStatus = "Sending you a test message…";
        try
        {
            _store.Save(Current);
            var sender = new EmailSender(new SmtpTransport(), Current.Email);
            var check = await sender.TestAsync("");
            EmailOk = check.Ok;
            EmailStatus = check.Message;
        }
        finally { EmailBusy = false; }
    }

    [RelayCommand]
    private async Task TestTextAsync()
    {
        TwilioBusy = true;
        TwilioStatus = "Sending you a test text…";
        try
        {
            _store.Save(Current);
            IMessageSender sender = TextVia switch
            {
                TextTransport.MacMessages => new MessagesTextSender(new AppleScriptRunner()),
                TextTransport.AndroidGateway => new AndroidGatewayTextSender(new HttpClient(), Current.AndroidGateway),
                _ => new TextSender(new TwilioGateway(Current.Twilio), Current.Twilio),
            };
            var check = await sender.TestAsync(
                TextVia == TextTransport.Twilio ? "" : TestNumber.Trim());
            TwilioOk = check.Ok;
            TwilioStatus = check.Message;
        }
        finally { TwilioBusy = false; }
    }

    /// <summary>Rings the user so they can speak the message everyone who prefers a
    /// call will hear. Twilio keeps the recording, so nothing needs hosting.</summary>
    [RelayCommand]
    private async Task RecordVoiceAsync()
    {
        TwilioBusy = true;
        try
        {
            _store.Save(Current);
            var sender = new VoiceSender(new TwilioGateway(Current.Twilio), Current.Twilio);
            var session = await sender.StartRecordingAsync();

            if (session is null || session.CallSid.Length == 0)
            {
                TwilioStatus = session?.Message ?? "Fill in your Twilio details and your own number first.";
                TwilioOk = false;
                return;
            }

            TwilioStatus = session.Message + " Courier will pick the recording up once you hang up.";

            // Twilio only has the recording after the call ends, so wait for it rather
            // than making the user press another button at exactly the right moment.
            for (var attempt = 0; attempt < 40; attempt++)
            {
                await Task.Delay(TimeSpan.FromSeconds(3));
                var url = await sender.CollectRecordingAsync(session.CallSid);
                if (url is null) continue;

                RecordingUrl = url;
                OnPropertyChanged(nameof(HasRecording));
                _store.Save(Current);
                TwilioOk = true;
                TwilioStatus = "Got it. That recording is what people who prefer a call will hear.";
                return;
            }

            TwilioStatus = "Courier did not get a recording. Try again, and speak after the beep before hanging up.";
        }
        finally { TwilioBusy = false; }
    }

    [RelayCommand]
    private async Task TestCallAsync()
    {
        TwilioBusy = true;
        TwilioStatus = "Calling you now…";
        try
        {
            _store.Save(Current);
            var sender = new VoiceSender(new TwilioGateway(Current.Twilio), Current.Twilio);
            var check = await sender.TestAsync("");
            TwilioOk = check.Ok;
            TwilioStatus = check.Message;
        }
        finally { TwilioBusy = false; }
    }

    [RelayCommand]
    private void BackUpNow()
    {
        try
        {
            var to = CourierDatabase.BackUp(_services.DatabasePath, DateTimeOffset.Now);
            BackupStatus = $"Copied to {Path.GetFileName(to)}.";
        }
        catch (Exception e)
        {
            BackupStatus = $"Could not make a backup. {e.Message}";
        }
    }
}
