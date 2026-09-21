using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Courier.Core.Domain;
using Courier.Data;
using Courier.Messaging;
using Courier.Messaging.Email;
using Courier.Messaging.Settings;
using Courier.Messaging.Android;
using Courier.Messaging.Mac;
using Courier.Messaging.Twilio;
using Entities = Courier.Data.Entities;

namespace Courier.App.ViewModels;

public sealed partial class SendRow : ObservableObject
{
    private readonly Action<Guid, bool> _pick;
    private readonly Func<Guid, Channel, Task> _choose;

    public SendRow(Recipient person, Action<Guid, bool> pick, Func<Guid, Channel, Task> choose)
    {
        Person = person;
        _pick = pick;
        _choose = choose;
        _channel = person.PreferredChannel;
    }

    public Recipient Person { get; }
    public Guid Id => Person.Id;
    public string Name => Person.SortName;
    public string Ward => Person.Ward ?? "—";
    public string Age => Person.Age?.ToString() ?? "—";
    public string Birthday => Person.BirthMonth is int m && Person.BirthDay is int d
        ? $"{d} {Months[m - 1]}" : "—";

    /// <summary>Editable here as well as on the People screen: deciding how to reach
    /// somebody usually occurs to you while looking at who is about to be sent to.</summary>
    [ObservableProperty] private Channel _channel;

    public bool IsEmail => Channel == Channel.Email;
    public bool IsText => Channel == Channel.Text;
    public bool IsVoice => Channel == Channel.Voice;
    public bool NoChannel => Channel == Channel.None;

    [RelayCommand] private Task ChooseEmail() => Set(Channel.Email);
    [RelayCommand] private Task ChooseText() => Set(Channel.Text);
    [RelayCommand] private Task ChooseVoice() => Set(Channel.Voice);

    private async Task Set(Channel channel)
    {
        Channel = Channel == channel ? Channel.None : channel;
        await _choose(Person.Id, Channel);
    }

    partial void OnChannelChanged(Channel value)
    {
        foreach (var name in (string[])["IsEmail", "IsText", "IsVoice", "NoChannel", "CanReceive", "GoesTo"])
            OnPropertyChanged(name);
    }

    private Recipient AsChosen => Person with { PreferredChannel = Channel };

    public bool CanReceive => AsChosen.CanReceive;
    /// <summary>The address this person would actually be reached at, in the form
    /// people read rather than the form a service dials.</summary>
    public string GoesTo => AsChosen.Reachability is { CanReceive: true, Address: { } address }
        ? (AsChosen.Reachability.Channel is Channel.Email ? address : PhoneFormat.ForDisplay(address))
        : Problem;

    public string Note => Person.Notes ?? "";
    public bool HasNote => Person.HasNote;

    private string Problem => AsChosen.Reachability.Reason switch
    {
        UnreachableReason.NoChannelChosen => "no channel chosen",
        UnreachableReason.MissingAddress => Channel == Channel.Email
            ? "no email address" : "no phone number",
        UnreachableReason.ChannelNotSupported => "channel not supported yet",
        UnreachableReason.NotInDirectory => "no longer in the directory",
        _ => "cannot be reached",
    };

    [ObservableProperty] private bool _isSelected = true;

    partial void OnIsSelectedChanged(bool value) => _pick(Id, value);

    private static readonly string[] Months =
        ["Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec"];
}

public sealed partial class SendViewModel(AppServices services, ISettingsStore store, bool? isMac = null)
    : ObservableObject
{
    private readonly bool _isMac = isMac ?? OperatingSystem.IsMacOS();

    private const decimal TextCost = 0.0079m;
    private const decimal CallCost = 0.014m;

    private IReadOnlyList<Recipient> _all = [];
    private readonly HashSet<Guid> _deselected = [];
    private AudienceSort _sort = AudienceSort.ByName;

    public const string AllWards = "All wards";
    public const string AnyChannel = "Any channel";
    public const string NoChannelChosen = "No channel chosen";
    public const string AnyMonth = "Any birthday";
    public const string EachPersonsChoice = "However each person prefers";

    [ObservableProperty] private string _search = "";
    [ObservableProperty] private string _ward = AllWards;
    [ObservableProperty] private string _channel = AnyChannel;
    [ObservableProperty] private string _birthdayMonth = AnyMonth;
    [ObservableProperty] private string _minAge = "";
    [ObservableProperty] private string _maxAge = "";

    /// <summary>Overrides everyone's preference for this one send — for something
    /// urgent enough to text the people who would normally be e-mailed.</summary>
    [ObservableProperty] private string _sendVia = EachPersonsChoice;

    [ObservableProperty] private string _subject = "";
    [ObservableProperty] private string _body = "";
    [ObservableProperty] private string _messagePreview = "";
    [ObservableProperty] private string _lengthLine = "";
    [ObservableProperty] private string _senderLine = "";

    // --- how the people who prefer a call will hear this -------------------------
    [ObservableProperty] private bool _speakAloud = true;
    [ObservableProperty] private string _recordingUrl = "";
    [ObservableProperty] private string _voiceStatus = "";
    [ObservableProperty] private bool _voiceBusy;

    public bool UseMyVoice
    {
        get => !SpeakAloud;
        set => SpeakAloud = !value;
    }

    public bool HasRecording => RecordingUrl.Length > 0;

    partial void OnSpeakAloudChanged(bool value)
    {
        OnPropertyChanged(nameof(UseMyVoice));
        VoiceStatus = value
            ? "Twilio will read the message out."
            : HasRecording ? "Your recording will play." : "Record yourself reading it, then it will play.";
    }

    partial void OnRecordingUrlChanged(string value) => OnPropertyChanged(nameof(HasRecording));

    [ObservableProperty] private string _matchLine = "";
    [ObservableProperty] private string _selectedLine = "";
    [ObservableProperty] private int _emailCount;
    [ObservableProperty] private int _textCount;
    [ObservableProperty] private int _voiceCount;
    [ObservableProperty] private string _costLine = "";
    [ObservableProperty] private string _costTotal = "$0.00";
    [ObservableProperty] private string _unreachableLine = "";
    [ObservableProperty] private bool _anyUnreachable;
    [ObservableProperty] private string _sendLabel = "Send";
    [ObservableProperty] private string _status = "";
    [ObservableProperty] private bool _sending;
    [ObservableProperty] private bool _loaded;
    [ObservableProperty] private string _blockedReason = "";

    public bool IsBlocked => BlockedReason.Length > 0;

    partial void OnBlockedReasonChanged(string value) => OnPropertyChanged(nameof(IsBlocked));

    /// <summary>Refuses the send outright rather than letting every message fail one at
    /// a time — the saved route is an iPhone, and this is not a Mac.</summary>
    private void CheckRoute()
    {
        var settings = store.Load();
        BlockedReason = settings.TextVia == TextTransport.MacMessages && !_isMac
            ? "Texts cannot go out from this computer: Courier is set to send from your iPhone, which needs to run on a Mac. Open Setup and choose Twilio, or your Android phone."
            : "";
    }

    public ObservableCollection<SendRow> Rows { get; } = [];
    public IReadOnlyList<string> WardOptions { get; } = [AllWards, .. Wards.All];
    public IReadOnlyList<string> ChannelOptions { get; } =
        [AnyChannel, "Email", "Text", "Voice", NoChannelChosen];
    public IReadOnlyList<string> SendViaOptions { get; } =
        [EachPersonsChoice, "Everyone by email", "Everyone by text", "Everyone by voice"];

    public IReadOnlyList<string> MonthOptions { get; } =
        [AnyMonth, "Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec"];

    public async Task LoadAsync()
    {
        await using var db = services.Db();
        _all = await new DirectoryService(db).RecipientsAsync();
        Loaded = true;
        OnSpeakAloudChanged(SpeakAloud);
        CheckRoute();
        Refresh();
        RefreshMessage();
    }

    partial void OnBodyChanged(string value)
    {
        // The message is the script. Once it changes, a recording of the old wording is
        // worse than none — it would go out sounding confident and be wrong.
        if (HasRecording)
        {
            RecordingUrl = "";
            VoiceStatus = "The message changed, so the recording was cleared. Record it again before sending.";
        }

        RefreshMessage();
    }

    /// <summary>Rings the user, reads them the message so they are not improvising, and
    /// records them reading it. Waits for the recording rather than making them press
    /// another button at exactly the right moment.</summary>
    [RelayCommand]
    private async Task RecordVoiceAsync()
    {
        VoiceBusy = true;
        try
        {
            var settings = store.Load();
            var sender = new VoiceSender(new TwilioGateway(settings.Twilio), settings.Twilio);
            var session = await sender.StartRecordingAsync(Signed);

            if (session is null || session.CallSid.Length == 0)
            {
                VoiceStatus = session?.Message ?? "Fill in your Twilio details and your own number on the Setup screen first.";
                return;
            }

            VoiceStatus = session.Message + " Courier will pick it up once you hang up.";

            for (var attempt = 0; attempt < 60; attempt++)
            {
                await Task.Delay(TimeSpan.FromSeconds(3));
                var url = await sender.CollectRecordingAsync(session.CallSid);
                if (url is null) continue;

                RecordingUrl = url;
                SpeakAloud = false;
                VoiceStatus = "Got it. That is what the people who prefer a call will hear.";
                return;
            }

            VoiceStatus = "Courier did not get a recording. Try again, and read the message after the beep before hanging up.";
        }
        finally { VoiceBusy = false; }
    }

    /// <summary>Calls the user and plays exactly what everyone else would hear.</summary>
    [RelayCommand]
    private async Task PreviewVoiceAsync()
    {
        VoiceBusy = true;
        try
        {
            var settings = store.Load();
            if (settings.Twilio.TestNumber.Trim().Length == 0)
            {
                VoiceStatus = "Add your own mobile number on the Setup screen so Courier knows where to call.";
                return;
            }

            var sender = new VoiceSender(new TwilioGateway(settings.Twilio), settings.Twilio);
            var outcome = await sender.PreviewAsync(
                settings.Twilio.TestNumber.Trim(),
                new OutgoingMessage(Subject, Signed, HasRecording ? RecordingUrl : null, SpeakAloud));

            VoiceStatus = outcome.Status == SendStatus.Sent
                ? $"Calling {PhoneFormat.ForDisplay(settings.Twilio.TestNumber)} now — answer it to hear what they will hear."
                : outcome.Error ?? "Courier could not place the call.";
        }
        finally { VoiceBusy = false; }
    }

    partial void OnSendViaChanged(string value) => Summarise();

    private Core.Domain.Channel? Override => SendVia switch
    {
        "Everyone by email" => Core.Domain.Channel.Email,
        "Everyone by text" => Core.Domain.Channel.Text,
        "Everyone by voice" => Core.Domain.Channel.Voice,
        _ => null,
    };

    /// <summary>The body as a recipient will read it: signed, once, here — so the
    /// preview, the length shown and what actually leaves are the same string.</summary>
    private string Signed => Signature.Compose(Body, store.Load().Sender);

    private void RefreshMessage()
    {
        var sender = store.Load().Sender;
        MessagePreview = Signed;
        SenderLine = sender.IsComplete
            ? $"Signed \u2014 {sender.Line}"
            : "Nobody has said who these messages are from. Add your name and calling on the Setup screen.";

        var segments = Signature.TextSegments(Signed);
        LengthLine = segments <= 1
            ? $"{Signed.Length} characters \u2014 fits in one text message."
            : $"{Signed.Length} characters \u2014 sends as {segments} text segments, billed separately.";
    }

    partial void OnSearchChanged(string value) => Refresh();
    partial void OnWardChanged(string value) => Refresh();
    partial void OnChannelChanged(string value) => Refresh();
    partial void OnBirthdayMonthChanged(string value) => Refresh();
    partial void OnMinAgeChanged(string value) => Refresh();
    partial void OnMaxAgeChanged(string value) => Refresh();

    [RelayCommand]
    private void SortBy(string key)
    {
        var chosen = key switch
        {
            "ward" => AudienceSortKey.Ward,
            "age" => AudienceSortKey.Age,
            "birthday" => AudienceSortKey.Birthday,
            "channel" => AudienceSortKey.Channel,
            _ => AudienceSortKey.Name,
        };
        _sort = _sort.Key == chosen ? _sort.Reversed() : new AudienceSort(chosen);
        Refresh();
    }

    [RelayCommand]
    private void ClearFilters()
    {
        Search = "";
        Ward = AllWards;
        Channel = AnyChannel;
        BirthdayMonth = AnyMonth;
        MinAge = "";
        MaxAge = "";
    }

    [RelayCommand]
    private void SelectAll()
    {
        foreach (var row in Rows) { _deselected.Remove(row.Id); row.IsSelected = true; }
        Summarise();
    }

    [RelayCommand]
    private void SelectNone()
    {
        foreach (var row in Rows) { _deselected.Add(row.Id); row.IsSelected = false; }
        Summarise();
    }

    private AudienceFilter Filter()
    {
        var channel = Channel switch
        {
            "Email" => ChannelFilter.Is(Core.Domain.Channel.Email),
            "Text" => ChannelFilter.Is(Core.Domain.Channel.Text),
            "Voice" => ChannelFilter.Is(Core.Domain.Channel.Voice),
            NoChannelChosen => ChannelFilter.NoneChosen,
            _ => ChannelFilter.Any,
        };

        var month = MonthOptions.ToList().IndexOf(BirthdayMonth);

        return new AudienceFilter
        {
            Search = string.IsNullOrWhiteSpace(Search) ? null : Search,
            Ward = Ward == AllWards ? null : Ward,
            Channel = channel,
            BirthdayMonth = month > 0 ? month : null,
            MinAge = int.TryParse(MinAge, out var min) ? min : null,
            MaxAge = int.TryParse(MaxAge, out var max) ? max : null,
        };
    }

    private void Refresh()
    {
        if (!Loaded) return;

        var matching = Audience.Select(_all, Filter(), _sort);
        Rows.Clear();
        foreach (var person in matching)
            Rows.Add(new SendRow(person, Pick, SaveChannelAsync) { IsSelected = !_deselected.Contains(person.Id) });

        MatchLine = $"{matching.Count} of {_all.Count(p => p.IsActive)} people match these filters.";
        Summarise();
    }

    private void Pick(Guid id, bool selected)
    {
        if (selected) _deselected.Remove(id); else _deselected.Add(id);
        Summarise();
    }

    /// <summary>The people ticked, carrying any channel changed on this screen so the
    /// summary and the send agree.</summary>
    private IReadOnlyList<Recipient> Chosen =>
        [.. Rows.Where(r => r.IsSelected).Select(r => r.Person with { PreferredChannel = r.Channel })];

    private async Task SaveChannelAsync(Guid personId, Core.Domain.Channel channel)
    {
        await using var db = services.Db();
        await new DirectoryService(db).SetPreferredChannelAsync(personId, channel);
        _all = [.. _all.Select(p => p.Id == personId ? p with { PreferredChannel = channel } : p)];
        Summarise();
    }

    private void Summarise()
    {
        var summary = Audience.Summarise(Chosen, Override);

        EmailCount = summary.Count(Core.Domain.Channel.Email);
        TextCount = summary.Count(Core.Domain.Channel.Text);
        VoiceCount = summary.Count(Core.Domain.Channel.Voice);

        SelectedLine = $"{summary.Chosen} selected";

        var cost = TextCount * TextCost + VoiceCount * CallCost;
        CostTotal = cost.ToString("C2", System.Globalization.CultureInfo.GetCultureInfo("en-US"));
        CostLine = $"{TextCount} texts at $0.0079 · {VoiceCount} calls at about $0.014 · email is free.";

        AnyUnreachable = summary.Unreachable.Count > 0;
        if (AnyUnreachable)
        {
            var names = summary.Unreachable.Take(3).Select(u => u.Person.LastName);
            var more = summary.Unreachable.Count > 3 ? $" and {summary.Unreachable.Count - 3} more" : "";
            UnreachableLine =
                $"{summary.Unreachable.Count} selected {(summary.Unreachable.Count == 1 ? "person has" : "people have")} " +
                $"no way to receive this — {string.Join(", ", names)}{more}. They will be skipped and listed afterwards.";
        }

        SendLabel = summary.WillReceive == 1 ? "Send to 1 person" : $"Send to {summary.WillReceive} people";
    }

    [RelayCommand]
    private async Task SendAsync()
    {
        CheckRoute();
        if (IsBlocked)
        {
            Status = BlockedReason;
            return;
        }

        var chosen = Chosen;
        if (chosen.Count == 0 || string.IsNullOrWhiteSpace(Body)) return;

        Sending = true;
        try
        {
            var settings = store.Load();
            var senders = new Dictionary<Core.Domain.Channel, IMessageSender>
            {
                [Core.Domain.Channel.Email] = new EmailSender(new SmtpTransport(), settings.Email),
                [Core.Domain.Channel.Text] = settings.TextVia switch
                {
                    TextTransport.MacMessages => new MessagesTextSender(new AppleScriptRunner()),
                    TextTransport.AndroidGateway =>
                        new AndroidGatewayTextSender(new HttpClient(), settings.AndroidGateway),
                    _ => new TextSender(new TwilioGateway(settings.Twilio), settings.Twilio),
                },
                [Core.Domain.Channel.Voice] = new VoiceSender(new TwilioGateway(settings.Twilio), settings.Twilio),
            };

            await using var db = services.Db();
            var service = new BroadcastService(db, senders);
            var progress = new Progress<BroadcastProgress>(p =>
                Status = $"Sending… {p.Done} of {p.Total} ({p.Who})");

            var batch = await service.SendAsync(
                Subject, Signed, Describe(chosen.Count), chosen, progress, Override,
                HasRecording ? RecordingUrl : null, SpeakAloud);

            var sent = await CountAsync(db, batch.Id, Entities.DeliveryStatus.Sent);
            var failed = await CountAsync(db, batch.Id, Entities.DeliveryStatus.Failed);
            var skipped = await CountAsync(db, batch.Id, Entities.DeliveryStatus.Skipped);

            Status = $"Sent to {sent} {(sent == 1 ? "person" : "people")}."
                   + (failed > 0 ? $" {failed} failed." : "")
                   + (skipped > 0 ? $" {skipped} skipped." : "");
        }
        catch (Exception e)
        {
            Status = $"The send stopped. {e.Message} Anything already sent is recorded and will not go out twice.";
        }
        finally { Sending = false; }
    }

    private static Task<int> CountAsync(CourierDbContext db, Guid batchId, Entities.DeliveryStatus status) =>
        Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.CountAsync(
            db.MessageDeliveries, d => d.MessageBatchId == batchId && d.Status == status);

    private string Describe(int count)
    {
        var parts = new List<string>();
        if (Ward != AllWards) parts.Add(Ward);
        if (Channel != AnyChannel) parts.Add(Channel);
        if (BirthdayMonth != AnyMonth) parts.Add($"birthdays in {BirthdayMonth}");
        if (MinAge.Length > 0 || MaxAge.Length > 0) parts.Add($"ages {(MinAge.Length > 0 ? MinAge : "any")}–{(MaxAge.Length > 0 ? MaxAge : "any")}");
        if (Search.Length > 0) parts.Add($"matching “{Search}”");
        return parts.Count == 0 ? $"Everyone active ({count})" : $"{string.Join(", ", parts)} ({count})";
    }
}
