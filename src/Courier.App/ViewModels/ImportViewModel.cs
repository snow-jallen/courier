using System.Collections.ObjectModel;
using Courier.Core.Import;
using Courier.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Courier.App.ViewModels;

public sealed record ChangeRow(string Kind, string Name, string Ward, string Detail)
{
    public bool IsAdded => Kind == "New";
    public bool IsUpdated => Kind == "Updated";
    public bool IsGone => Kind == "Not listed";
    public bool IsBack => Kind == "Back";
}

public sealed partial class ImportViewModel(AppServices services, IFilePicker picker) : ObservableObject
{
    private LcrReport? _report;
    private ImportPlan? _plan;

    [ObservableProperty] private string _fileName = "";
    [ObservableProperty] private string _fileDetail = "";
    [ObservableProperty] private bool _hasFile;
    [ObservableProperty] private bool _busy;
    [ObservableProperty] private string _status = "";
    [ObservableProperty] private bool _failed;

    [ObservableProperty] private int _inFile;
    [ObservableProperty] private int _added;
    [ObservableProperty] private int _updated;
    [ObservableProperty] private int _deactivated;
    [ObservableProperty] private int _unchanged;

    public ObservableCollection<ChangeRow> Changes { get; } = [];
    public ObservableCollection<string> Warnings { get; } = [];
    public bool HasWarnings => Warnings.Count > 0;

    [RelayCommand]
    private async Task ChooseFileAsync()
    {
        var path = await picker.PickPdfAsync();
        if (path is null) return;
        await LoadAsync(path);
    }

    private async Task LoadAsync(string path)
    {
        Busy = true;
        Failed = false;
        Status = "Reading the report…";
        Changes.Clear();
        Warnings.Clear();

        try
        {
            await using var db = services.Db();
            var (report, plan) = await new ImportService(db).PrepareAsync(path);
            _report = report;
            _plan = plan;

            FileName = report.FileName;
            FileDetail = $"{report.PageCount} pages · {report.Rows.Count} people";
            HasFile = true;

            InFile = plan.TotalInFile;
            Added = plan.Added.Count;
            Updated = plan.Updated.Count;
            Deactivated = plan.Deactivated.Count;
            Unchanged = plan.Unchanged;

            foreach (var p in plan.Added)
                Changes.Add(new ChangeRow("New", p.DisplayName, p.Ward ?? "—", Describe(p)));
            foreach (var u in plan.Updated)
                Changes.Add(new ChangeRow("Updated", u.Incoming.DisplayName, u.Incoming.Ward ?? "—",
                    string.Join("   ", u.Changes.Select(c =>
                        $"{c.Field}: {Blank(c.From)} → {Blank(c.To)}"))));
            foreach (var r in plan.Reactivated)
                Changes.Add(new ChangeRow("Back", r.Incoming.DisplayName, r.Incoming.Ward ?? "—",
                    "Listed again — restored with everything they had"));
            foreach (var d in plan.Deactivated)
                Changes.Add(new ChangeRow("Not listed", $"{d.LastName}, {d.FirstName}", d.Ward ?? "—",
                    "Kept and marked inactive — nothing is deleted"));

            foreach (var w in plan.Warnings) Warnings.Add(w);
            OnPropertyChanged(nameof(HasWarnings));

            Status = plan.Added.Count + plan.Updated.Count + plan.Deactivated.Count + plan.Reactivated.Count == 0
                ? "Nothing in this file has changed since the last import."
                : "Nothing has been saved yet. Look over the changes, then apply them.";
        }
        catch (LcrReportException e)
        {
            Failed = true;
            HasFile = false;
            Status = e.Message;
        }
        catch (Exception e)
        {
            Failed = true;
            HasFile = false;
            Status = $"That file could not be read. {e.Message}";
        }
        finally
        {
            Busy = false;
        }
    }

    [RelayCommand]
    private async Task ApplyAsync()
    {
        if (_report is null || _plan is null) return;
        Busy = true;
        try
        {
            if (File.Exists(services.DatabasePath))
                CourierDatabase.BackUp(services.DatabasePath, DateTimeOffset.Now);

            await using var db = services.Db();
            await new ImportService(db).ApplyAsync(_report, _plan, AppServices.Today);

            Status = $"Saved. {Added} added, {Updated} updated, {Deactivated} marked inactive.";
            HasFile = false;
            Changes.Clear();
            Warnings.Clear();
            OnPropertyChanged(nameof(HasWarnings));
            _report = null;
            _plan = null;
        }
        catch (Exception e)
        {
            Failed = true;
            Status = $"Nothing was saved. {e.Message}";
        }
        finally
        {
            Busy = false;
        }
    }

    [RelayCommand]
    private void Cancel()
    {
        HasFile = false;
        Changes.Clear();
        Warnings.Clear();
        OnPropertyChanged(nameof(HasWarnings));
        Status = "";
        _report = null;
        _plan = null;
    }

    private static string Blank(string? s) => string.IsNullOrWhiteSpace(s) ? "(none)" : s;

    private static string Describe(NormalizedPerson p)
    {
        var has = new List<string>();
        if (p.Email is not null) has.Add("email");
        if (p.PhoneRaw is not null) has.Add("phone");
        if (p.Address is not null) has.Add("address");
        return has.Count == 0 ? "Added with no contact details" : "Added with " + string.Join(", ", has);
    }
}
