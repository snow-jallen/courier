using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Courier.Data;

namespace Courier.App.ViewModels;

public sealed partial class PendingRow : ObservableObject
{
    private readonly Func<Guid, Task> _markEntered;
    private readonly Func<string, Task> _copy;

    public PendingRow(PendingEntry entry, Func<Guid, Task> markEntered, Func<string, Task> copy)
    {
        Entry = entry;
        _markEntered = markEntered;
        _copy = copy;
    }

    public PendingEntry Entry { get; }
    public string PersonName => Entry.PersonName;
    public string Ward => Entry.Ward ?? "—";
    public string KindLabel => Entry.KindLabel;
    public string Value => Entry.Value;
    public string AddedOn => Entry.AddedOn.ToString("d MMM");

    [RelayCommand] private Task Copy() => _copy(Entry.Value);
    [RelayCommand] private Task Entered() => _markEntered(Entry.ContactPointId);
}

public sealed partial class LcrBacklogViewModel(AppServices services, IClipboardWriter clipboard) : ObservableObject
{
    [ObservableProperty] private string _summary = "";
    [ObservableProperty] private bool _isEmpty;

    public ObservableCollection<PendingRow> Rows { get; } = [];

    public async Task LoadAsync()
    {
        await using var db = services.Db();
        var pending = await new DirectoryService(db).PendingLcrEntriesAsync();

        Rows.Clear();
        foreach (var entry in pending) Rows.Add(new PendingRow(entry, MarkEnteredAsync, CopyAsync));

        IsEmpty = Rows.Count == 0;
        Summary = Rows.Count switch
        {
            0 => "Nothing waiting. Everything people have given you is already in LCR.",
            1 => "1 detail to enter in LCR.",
            _ => $"{Rows.Count} details to enter in LCR.",
        };
    }

    [RelayCommand]
    private async Task CopyAll()
    {
        var lines = Rows.Select(r => $"{r.PersonName}\t{r.Ward}\t{r.KindLabel}\t{r.Value}");
        await clipboard.CopyAsync(string.Join(Environment.NewLine, lines));
    }

    private Task CopyAsync(string value) => clipboard.CopyAsync(value);

    private async Task MarkEnteredAsync(Guid contactPointId)
    {
        await using var db = services.Db();
        await new DirectoryService(db).MarkEnteredInLcrAsync(contactPointId, AppServices.Today);
        await LoadAsync();
    }
}
