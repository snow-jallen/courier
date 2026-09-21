using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Courier.Messaging;
using Courier.Messaging.Email;
using Courier.Messaging.Settings;
using Courier.Messaging.Android;
using Courier.Messaging.Mac;
using Courier.Messaging.Twilio;
using Courier.Core.Diagnostics;
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
    private readonly IDatabasePicker? _databases;
    private readonly IClipboardWriter? _clipboard;
    private readonly Action? _databaseChanged;

    public SetupViewModel(
        AppServices services,
        ISettingsStore store,
        bool? isMac = null,
        IDatabasePicker? databases = null,
        Action? databaseChanged = null,
        IClipboardWriter? clipboard = null)
    {
        _services = services;
        _store = store;
        _databases = databases;
        _databaseChanged = databaseChanged;
        _clipboard = clipboard;
        _logFolder = services.Activity.Folder;
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
        _databasePath = services.DatabasePath;
        _settingsPath = store.Path;
        _saved = settings;
    }

    // --- where the directory lives ---------------------------------------------
    [ObservableProperty] private string _databasePath;
    [ObservableProperty] private string _settingsPath;
    [ObservableProperty] private string _backupStatus = "";

    // --- getting help --------------------------------------------------------------
    [ObservableProperty] private string _logFolder;
    [ObservableProperty] private string _logStatus = "";

    public bool CanCopyLog => _clipboard is not null;

    /// <summary>Puts the recent log on the clipboard to paste to whoever is helping.
    /// It records what was done and what failed, never a name, a number or a word of
    /// any message.</summary>
    [RelayCommand]
    private async Task CopyLogAsync()
    {
        if (_clipboard is null) return;
        try
        {
            await _clipboard.CopyAsync(_services.Activity.Recent());
            LogStatus = "Copied. Paste it into an email or a message to whoever is helping you.";
            Log.Record("log.copied");
        }
        catch (Exception e)
        {
            LogStatus = $"The log could not be copied. {e.Message}";
        }
    }

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

    /// <summary>Calls always go through Twilio, so the account is needed even when
    /// texts come from the user's own phone. Saying which of the two it is for stops
    /// the credentials looking like leftovers from a route no longer in use.</summary>
    public string TwilioPurpose => UseTwilioForText
        ? "Used for your texts and your phone calls"
        : "Used for phone calls — your texts go out from your own phone";

    public string TestTextLabel => TextVia switch
    {
        TextTransport.MacMessages => "Text myself through Messages",
        TextTransport.AndroidGateway => "Text myself through my phone",
        _ => "Send myself a test text",
    };

    public string TextRouteBlockedMessage =>
        "Texting from your iPhone needs Courier running on a Mac, because it works by asking the Messages app to send. "
        + "On this computer, choose Twilio, or your Android phone if you have one. Your iPhone setting is kept for when you are back on the Mac.";

    partial void OnTextViaChanged(TextTransport value)
    {
        foreach (var name in (string[])
                 ["UseTwilioForText", "UseIphone", "UseAndroid", "TextRouteBlocked",
                  "TwilioPurpose", "TestTextLabel"])
            OnPropertyChanged(name);
    }

    partial void OnMessagingServiceSidChanged(string value) => OnPropertyChanged(nameof(SendsRichText));
    [ObservableProperty] private string _twilioStatus = "";
    [ObservableProperty] private bool _twilioOk;
    [ObservableProperty] private bool _twilioBusy;

    /// <summary>What was last written to disk. Everything on this screen is compared
    /// against it, so the Save button can say whether it needs pressing instead of
    /// leaving somebody to wonder.</summary>
    private CourierSettings _saved;

    public bool IsDirty => Current != _saved;

    public string SaveHint => IsDirty
        ? "You have changes that are not saved yet."
        : "Everything here is saved.";

    private static readonly string[] NotWorthRechecking =
        [nameof(IsDirty), nameof(SaveHint), nameof(EmailStatus), nameof(TwilioStatus),
         nameof(BackupStatus), nameof(EmailBusy), nameof(TwilioBusy), nameof(EmailOk), nameof(TwilioOk),
         nameof(TwilioPurpose), nameof(TestTextLabel), nameof(TextRouteBlocked)];

    /// <summary>Any field changing can make the screen dirty, and there are a lot of
    /// fields. Watching them all in one place beats remembering to add each new one.</summary>
    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.PropertyName is null || NotWorthRechecking.Contains(e.PropertyName)) return;
        OnPropertyChanged(nameof(IsDirty));
        OnPropertyChanged(nameof(SaveHint));
    }

    private void MarkSaved()
    {
        _saved = Current;
        OnPropertyChanged(nameof(IsDirty));
        OnPropertyChanged(nameof(SaveHint));
    }

    private CourierSettings Current => new()
    {
        DatabasePath = DatabasePath,
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
        },
    };

    [RelayCommand]
    private void Save()
    {
        _store.Save(Current);
        MarkSaved();

        // What was filled in, never what was typed into it.
        Log.Record("settings.save", Log.Details(
            ("sender", Current.Sender.IsComplete), ("email", Current.Email.IsComplete),
            ("twilio", Current.Twilio.IsComplete), ("textVia", TextVia.ToString()),
            ("rcs", SendsRichText), ("androidGateway", Current.AndroidGateway.IsComplete)));
    }

    [RelayCommand]
    private async Task TestEmailAsync()
    {
        EmailBusy = true;
        EmailStatus = "Sending you a test message…";
        try
        {
            _store.Save(Current);
            MarkSaved();
            var sender = new EmailSender(new SmtpTransport(), Current.Email);
            var check = await sender.TestAsync("");
            EmailOk = check.Ok;
            EmailStatus = check.Message;
            Log.Record("test.email", Log.Details(
                ("ok", check.Ok), ("host", Current.Email.Host),
                ("result", Redact.Failure(check.Message))));
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
            MarkSaved();
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
            Log.Record("test.text", Log.Details(
                ("ok", check.Ok), ("via", TextVia.ToString()),
                ("rcs", SendsRichText), ("result", Redact.Failure(check.Message))));
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
            MarkSaved();
            var sender = new VoiceSender(new TwilioGateway(Current.Twilio), Current.Twilio);
            var check = await sender.TestAsync("");
            TwilioOk = check.Ok;
            TwilioStatus = check.Message;
        }
        finally { TwilioBusy = false; }
    }

    public bool CanBrowseForDatabase => _databases is not null;

    [RelayCommand]
    private Task OpenDatabaseAsync() => SwitchAsync(existing: true);

    [RelayCommand]
    private Task NewDatabaseAsync() => SwitchAsync(existing: false);

    /// <summary>Opens a different directory file, or starts one somewhere else. The
    /// chosen file has to be a Courier database before it becomes the open one —
    /// finding out afterwards would mean the app is already pointed at it.</summary>
    private async Task SwitchAsync(bool existing)
    {
        if (_databases is null) return;

        var path = existing ? await _databases.PickExistingAsync() : await _databases.PickNewAsync();
        if (path is null) return;

        if (string.Equals(path, _services.DatabasePath, StringComparison.Ordinal))
        {
            BackupStatus = "That is the file already open.";
            return;
        }

        var previous = _services.DatabasePath;
        try
        {
            _services.SwitchTo(path);
        }
        catch (Exception e)
        {
            Log.Failure("database.switch", e, Log.Details(("path", Redact.Path(path))));
            BackupStatus = $"That file could not be opened as a Courier directory, so nothing changed. {e.Message}";
            return;
        }

        _store.Save(Current with { DatabasePath = path });
        DatabasePath = path;
        MarkSaved();

        BackupStatus = existing
            ? $"Opened {Path.GetFileName(path)}. The previous file at {previous} is untouched."
            : $"Started a new, empty directory at {Path.GetFileName(path)}. Import an LCR export to fill it.";

        _databaseChanged?.Invoke();
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
