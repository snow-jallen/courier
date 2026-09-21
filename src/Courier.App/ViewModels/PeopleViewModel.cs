using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Courier.Core.Domain;
using Courier.Data;

namespace Courier.App.ViewModels;

/// <summary>One person in the list. Holds its own commands so a row can change a
/// channel without every template reaching back up through the tree.</summary>
public sealed partial class PersonRow : ObservableObject
{
    private readonly Func<Guid, Channel, Task> _choose;

    public PersonRow(Recipient person, Func<Guid, Channel, Task> choose)
    {
        Person = person;
        _choose = choose;
        _channel = person.PreferredChannel;
    }

    public Recipient Person { get; }
    public Guid Id => Person.Id;
    public string Name => Person.SortName;
    public string Ward => Person.Ward ?? "—";
    public string Email => Person.Email ?? "—";
    public string Phone => Person.Phone ?? "—";
    public string Age => Person.Age?.ToString() ?? "—";
    public string Birthday => Person.BirthMonth is int m && Person.BirthDay is int d
        ? $"{d} {Months[m - 1]}" : "—";

    [ObservableProperty] private Channel _channel;

    public bool IsEmail => Channel == Channel.Email;
    public bool IsText => Channel == Channel.Text;
    public bool IsVoice => Channel == Channel.Voice;

    partial void OnChannelChanged(Channel value)
    {
        OnPropertyChanged(nameof(IsEmail));
        OnPropertyChanged(nameof(IsText));
        OnPropertyChanged(nameof(IsVoice));
    }

    [RelayCommand] private Task ChooseEmail() => Set(Channel.Email);
    [RelayCommand] private Task ChooseText() => Set(Channel.Text);
    [RelayCommand] private Task ChooseVoice() => Set(Channel.Voice);

    /// <summary>Clicking the channel already chosen clears it, which is how someone
    /// gets back to "not decided yet" without a fourth button for it.</summary>
    private async Task Set(Channel channel)
    {
        Channel = Channel == channel ? Channel.None : channel;
        await _choose(Id, Channel);
    }

    private static readonly string[] Months =
        ["Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec"];
}

public sealed partial class PeopleViewModel(AppServices services) : ObservableObject
{
    private IReadOnlyList<Recipient> _all = [];
    private AudienceSort _sort = AudienceSort.ByName;

    [ObservableProperty] private string _search = "";
    [ObservableProperty] private string _ward = AllWards;
    [ObservableProperty] private string _summary = "";
    [ObservableProperty] private bool _loaded;

    public const string AllWards = "All wards";

    public ObservableCollection<PersonRow> Rows { get; } = [];
    public IReadOnlyList<string> WardOptions { get; } = [AllWards, .. Wards.All];

    public async Task LoadAsync()
    {
        await using var db = services.Db();
        _all = await new DirectoryService(db).RecipientsAsync();
        Loaded = true;
        Refresh();
    }

    partial void OnSearchChanged(string value) => Refresh();
    partial void OnWardChanged(string value) => Refresh();

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

    private void Refresh()
    {
        if (!Loaded) return;

        var filter = new AudienceFilter
        {
            Search = string.IsNullOrWhiteSpace(Search) ? null : Search,
            Ward = Ward == AllWards ? null : Ward,
            ActiveOnly = true,
        };

        var shown = Audience.Select(_all, filter, _sort);
        Rows.Clear();
        foreach (var person in shown) Rows.Add(new PersonRow(person, SaveChannelAsync));

        var active = _all.Count(p => p.IsActive);
        Summary = shown.Count == active
            ? $"{active} people"
            : $"{shown.Count} of {active} people";
    }

    private async Task SaveChannelAsync(Guid personId, Channel channel)
    {
        await using var db = services.Db();
        await new DirectoryService(db).SetPreferredChannelAsync(personId, channel);

        // Keep the copy in memory in step, so re-filtering does not undo the click.
        _all = [.. _all.Select(p => p.Id == personId ? p with { PreferredChannel = channel } : p)];
    }
}
