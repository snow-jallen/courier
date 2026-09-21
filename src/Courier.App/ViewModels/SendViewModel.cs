using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Courier.Core.Domain;
using Courier.Data;
using Courier.Messaging;
using Courier.Messaging.Email;
using Courier.Messaging.Settings;
using Courier.Messaging.Twilio;
using Entities = Courier.Data.Entities;

namespace Courier.App.ViewModels;

public sealed partial class SendRow(Recipient person, Action<Guid, bool> pick) : ObservableObject
{
    public Recipient Person { get; } = person;
    public Guid Id => Person.Id;
    public string Name => Person.SortName;
    public string Ward => Person.Ward ?? "—";
    public string Age => Person.Age?.ToString() ?? "—";
    public string Birthday => Person.BirthMonth is int m && Person.BirthDay is int d
        ? $"{d} {Months[m - 1]}" : "—";

    public string ChannelLabel => Person.PreferredChannel switch
    {
        Channel.Email => "Email",
        Channel.Text => "Text",
        Channel.Voice => "Voice",
        _ => "Not set",
    };

    public bool IsEmail => Person.PreferredChannel == Channel.Email;
    public bool IsText => Person.PreferredChannel == Channel.Text;
    public bool IsVoice => Person.PreferredChannel == Channel.Voice;
    public bool NoChannel => Person.PreferredChannel == Channel.None;

    public bool CanReceive => Person.CanReceive;
    public string GoesTo => Person.Reachability.Address ?? Problem;

    private string Problem => Person.Reachability.Reason switch
    {
        UnreachableReason.NoChannelChosen => "no channel chosen",
        UnreachableReason.MissingAddress => Person.PreferredChannel == Channel.Email
            ? "no email address" : "no phone number",
        UnreachableReason.ChannelNotSupported => "channel not supported yet",
        UnreachableReason.NotInDirectory => "no longer in the directory",
        _ => "cannot be reached",
    };

    [ObservableProperty] private bool _isSelected = true;

    partial void OnIsSelectedChanged(bool value) => pick(Id, value);

    private static readonly string[] Months =
        ["Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec"];
}

public sealed partial class SendViewModel(AppServices services, ISettingsStore store) : ObservableObject
{
    private const decimal TextCost = 0.0079m;
    private const decimal CallCost = 0.014m;

    private IReadOnlyList<Recipient> _all = [];
    private readonly HashSet<Guid> _deselected = [];
    private AudienceSort _sort = AudienceSort.ByName;

    public const string AllWards = "All wards";
    public const string AnyChannel = "Any channel";
    public const string NoChannelChosen = "No channel chosen";
    public const string AnyMonth = "Any birthday";

    [ObservableProperty] private string _search = "";
    [ObservableProperty] private string _ward = AllWards;
    [ObservableProperty] private string _channel = AnyChannel;
    [ObservableProperty] private string _birthdayMonth = AnyMonth;
    [ObservableProperty] private string _minAge = "";
    [ObservableProperty] private string _maxAge = "";

    [ObservableProperty] private string _subject = "";
    [ObservableProperty] private string _body = "";

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

    public ObservableCollection<SendRow> Rows { get; } = [];
    public IReadOnlyList<string> WardOptions { get; } = [AllWards, .. Wards.All];
    public IReadOnlyList<string> ChannelOptions { get; } =
        [AnyChannel, "Email", "Text", "Voice", NoChannelChosen];
    public IReadOnlyList<string> MonthOptions { get; } =
        [AnyMonth, "Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec"];

    public async Task LoadAsync()
    {
        await using var db = services.Db();
        _all = await new DirectoryService(db).RecipientsAsync();
        Loaded = true;
        Refresh();
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
            Rows.Add(new SendRow(person, Pick) { IsSelected = !_deselected.Contains(person.Id) });

        MatchLine = $"{matching.Count} of {_all.Count(p => p.IsActive)} people match these filters.";
        Summarise();
    }

    private void Pick(Guid id, bool selected)
    {
        if (selected) _deselected.Remove(id); else _deselected.Add(id);
        Summarise();
    }

    private IReadOnlyList<Recipient> Chosen =>
        [.. Rows.Where(r => r.IsSelected).Select(r => r.Person)];

    private void Summarise()
    {
        var summary = Audience.Summarise(Chosen);

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
        var chosen = Chosen;
        if (chosen.Count == 0 || string.IsNullOrWhiteSpace(Body)) return;

        Sending = true;
        try
        {
            var settings = store.Load();
            var senders = new Dictionary<Core.Domain.Channel, IMessageSender>
            {
                [Core.Domain.Channel.Email] = new EmailSender(new SmtpTransport(), settings.Email),
                [Core.Domain.Channel.Text] = new TextSender(new TwilioGateway(settings.Twilio), settings.Twilio),
                [Core.Domain.Channel.Voice] = new VoiceSender(new TwilioGateway(settings.Twilio), settings.Twilio),
            };

            await using var db = services.Db();
            var service = new BroadcastService(db, senders);
            var progress = new Progress<BroadcastProgress>(p =>
                Status = $"Sending… {p.Done} of {p.Total} ({p.Who})");

            var batch = await service.SendAsync(Subject, Body, Describe(chosen.Count), chosen, progress);

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
