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
    private readonly Action<PersonRow> _edit;

    public PersonRow(Recipient person, Func<Guid, Channel, Task> choose, Action<PersonRow> edit)
    {
        Person = person;
        _choose = choose;
        _edit = edit;
        _channel = person.PreferredChannel;
    }

    [RelayCommand] private void Edit() => _edit(this);

    public Recipient Person { get; }
    public Guid Id => Person.Id;
    public string Name => Person.SortName;
    public string Ward => Person.Ward ?? "—";
    public string Email => Person.Email ?? "—";
    public string Phone => Person.Phone is null ? "—" : Person.PhoneForDisplay;
    public string Age => Person.Age?.ToString() ?? "—";
    public string Note => Person.Notes ?? "";
    public bool HasNote => Person.HasNote;
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

/// <summary>Correcting what Courier holds for one person. Saving does not overwrite
/// what the export said — it records the correction, which is what puts it on the
/// "to enter in LCR" list and what keeps it through the next import.</summary>
public sealed partial class PersonEditor : ObservableObject
{
    private readonly Func<PersonEditor, Task> _save;
    private readonly Action _close;

    /// <summary>Correcting somebody already in the directory.</summary>
    public PersonEditor(Recipient person, Func<PersonEditor, Task> save, Action close)
    {
        _save = save;
        _close = close;
        Id = person.Id;
        Name = person.SortName;
        Ward = person.Ward ?? "no ward";
        _lastName = person.LastName;
        _firstName = person.FirstName;
        _wardChoice = person.Ward ?? "";
        _email = person.Email ?? "";
        _phone = person.PhoneForDisplay;
        _address = person.Address ?? "";
        _notes = person.Notes ?? "";
    }

    /// <summary>Adding somebody the export does not carry.</summary>
    public PersonEditor(Func<PersonEditor, Task> save, Action close)
    {
        _save = save;
        _close = close;
        IsNew = true;
        Name = "Someone new";
        Ward = "not in the export";
    }

    public Guid Id { get; }
    public string Name { get; }
    public string Ward { get; }
    public bool IsNew { get; }

    [ObservableProperty] private string _lastName = "";
    [ObservableProperty] private string _firstName = "";
    [ObservableProperty] private string _wardChoice = "";

    public IReadOnlyList<string> WardOptions { get; } = ["", .. Wards.All];

    [ObservableProperty] private string _email = "";
    [ObservableProperty] private string _phone = "";
    [ObservableProperty] private string _address = "";
    [ObservableProperty] private string _notes = "";
    [ObservableProperty] private string _status = "";
    [ObservableProperty] private bool _busy;

    [RelayCommand]
    private async Task Save()
    {
        if (IsNew && LastName.Trim().Length == 0)
        {
            Status = "A surname is needed, so there is something to sort them by.";
            return;
        }

        Busy = true;
        try { await _save(this); }
        catch (Exception e) { Status = $"Not saved. {e.Message}"; }
        finally { Busy = false; }
    }

    [RelayCommand] private void Cancel() => _close();
}

public sealed partial class PeopleViewModel(AppServices services) : ObservableObject
{
    private IReadOnlyList<Recipient> _all = [];
    private AudienceSort _sort = AudienceSort.ByName;

    [ObservableProperty] private string _search = "";
    [ObservableProperty] private string _ward = AllWards;
    [ObservableProperty] private string _summary = "";
    [ObservableProperty] private bool _loaded;
    [ObservableProperty] private PersonEditor? _editing;
    [ObservableProperty] private string _editStatus = "";

    public bool IsEditing => Editing is not null;

    partial void OnEditingChanged(PersonEditor? value) => OnPropertyChanged(nameof(IsEditing));

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
        foreach (var person in shown) Rows.Add(new PersonRow(person, SaveChannelAsync, StartEditing));

        var active = _all.Count(p => p.IsActive);
        Summary = shown.Count == active
            ? $"{active} people"
            : $"{shown.Count} of {active} people";
    }

    /// <summary>Somebody the export does not carry — a spouse, a visitor, anybody the
    /// directory has not caught up with.</summary>
    [RelayCommand]
    private void AddPerson()
    {
        EditStatus = "";
        Editing = new PersonEditor(SaveDetailsAsync, () => Editing = null);
    }

    private void StartEditing(PersonRow row)
    {
        EditStatus = "";
        Editing = new PersonEditor(row.Person, SaveDetailsAsync, () => Editing = null);
    }

    private async Task SaveDetailsAsync(PersonEditor editor)
    {
        if (editor.IsNew)
        {
            await using (var adding = services.Db())
            {
                await new DirectoryService(adding).AddPersonAsync(
                    editor.LastName, editor.FirstName, editor.WardChoice,
                    editor.Email, editor.Phone, editor.Address, editor.Notes, AppServices.Today);
            }

            EditStatus = $"Added {editor.LastName.Trim()}. They are not in the export, so no import will remove them.";
            Editing = null;
            await LoadAsync();
            return;
        }

        int added;
        await using (var db = services.Db())
        {
            added = await new DirectoryService(db).UpdateDetailsAsync(
                editor.Id, editor.Email, editor.Phone, editor.Address, editor.Notes, AppServices.Today);
        }

        EditStatus = added switch
        {
            0 => $"Saved. Nothing new for LCR — {editor.Name} already matched.",
            1 => $"Saved. One detail for {editor.Name} is now waiting to be entered in LCR.",
            _ => $"Saved. {added} details for {editor.Name} are now waiting to be entered in LCR.",
        };

        Editing = null;
        await LoadAsync();
    }

    private async Task SaveChannelAsync(Guid personId, Channel channel)
    {
        await using var db = services.Db();
        await new DirectoryService(db).SetPreferredChannelAsync(personId, channel);

        // Keep the copy in memory in step, so re-filtering does not undo the click.
        _all = [.. _all.Select(p => p.Id == personId ? p with { PreferredChannel = channel } : p)];
    }
}
