using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Courier.Data;

namespace Courier.App.ViewModels;

public sealed partial class SentBatchRow(SentBatch batch, Action<SentBatchRow> open) : ObservableObject
{
    public SentBatch Batch { get; } = batch;
    public string When => Batch.When;
    public string Headline => Batch.Headline;
    public string Audience => Batch.Audience;
    public string Outcome => Batch.Outcome;
    public bool AnyTrouble => Batch.Failed > 0 || Batch.Skipped > 0;

    [ObservableProperty] private bool _isSelected;

    [RelayCommand] private void Open() => open(this);
}

public sealed partial class HistoryViewModel(AppServices services, IClipboardWriter clipboard) : ObservableObject
{
    private List<SentDelivery> _allDeliveries = [];

    [ObservableProperty] private string _summary = "";
    [ObservableProperty] private bool _isEmpty = true;
    [ObservableProperty] private SentBatchRow? _selected;
    [ObservableProperty] private string _search = "";
    [ObservableProperty] private string _detailHeadline = "";
    [ObservableProperty] private string _detailBody = "";
    [ObservableProperty] private string _copyStatus = "";

    public ObservableCollection<SentBatchRow> Batches { get; } = [];
    public ObservableCollection<SentDelivery> Deliveries { get; } = [];

    public bool HasSelection => Selected is not null;

    partial void OnSelectedChanged(SentBatchRow? value) => OnPropertyChanged(nameof(HasSelection));
    partial void OnSearchChanged(string value) => ShowDeliveries();

    public async Task LoadAsync()
    {
        await using var db = services.Db();
        var batches = await new MessageLogService(db).RecentAsync();

        Batches.Clear();
        foreach (var batch in batches) Batches.Add(new SentBatchRow(batch, OpenAsync));

        IsEmpty = Batches.Count == 0;
        Summary = Batches.Count switch
        {
            0 => "Nothing has been sent yet. Every message will be listed here afterwards, with what happened to each person's copy.",
            1 => "1 message sent.",
            _ => $"{Batches.Count} messages sent.",
        };

        if (Batches.Count > 0) await SelectAsync(Batches[0]);
        else { Selected = null; Deliveries.Clear(); }
    }

    private async void OpenAsync(SentBatchRow row) => await SelectAsync(row);

    private async Task SelectAsync(SentBatchRow row)
    {
        foreach (var other in Batches) other.IsSelected = ReferenceEquals(other, row);
        Selected = row;
        CopyStatus = "";
        DetailHeadline = row.Batch.Headline;
        DetailBody = row.Batch.Body;

        await using var db = services.Db();
        _allDeliveries = await new MessageLogService(db).DeliveriesAsync(row.Batch.Id);
        ShowDeliveries();
    }

    private void ShowDeliveries()
    {
        var term = Search.Trim();
        Deliveries.Clear();

        foreach (var delivery in _allDeliveries)
        {
            if (term.Length > 0
                && !delivery.PersonName.Contains(term, StringComparison.OrdinalIgnoreCase)
                && !delivery.AddressForDisplay.Contains(term, StringComparison.OrdinalIgnoreCase))
                continue;

            Deliveries.Add(delivery);
        }
    }

    /// <summary>The whole record of one send, for pasting into a report or an e-mail to
    /// somebody who wants to know who was told.</summary>
    [RelayCommand]
    private async Task CopyAsync()
    {
        if (Selected is null) return;

        await using var db = services.Db();
        await clipboard.CopyAsync(await new MessageLogService(db).AsTextAsync(Selected.Batch.Id));
        CopyStatus = "Copied — paste it wherever you need it.";
    }
}
