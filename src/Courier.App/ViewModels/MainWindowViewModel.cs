using CommunityToolkit.Mvvm.ComponentModel;

namespace Courier.App.ViewModels;

/// <summary>A screen that is not built yet, so the shell can be navigated end to end
/// while the rest is written.</summary>
public sealed partial class ComingSoonViewModel(string title, string note) : ObservableObject
{
    public string Title { get; } = title;
    public string Note { get; } = note;
}

public sealed partial class MainWindowViewModel : ObservableObject
{
    private readonly ImportViewModel _import;
    private readonly ComingSoonViewModel _people =
        new("People", "The directory, with the preferred channel for each person.");
    private readonly ComingSoonViewModel _send =
        new("Send a message", "Choose who it goes to, write it once, and each person gets it their way.");
    private readonly ComingSoonViewModel _lcr =
        new("To enter in LCR", "Contact details people gave you that LCR does not have yet.");
    private readonly ComingSoonViewModel _setup =
        new("Setup", "Where your directory lives, and the accounts Courier sends through.");

    [ObservableProperty] private object _current;
    [ObservableProperty] private string _databaseLabel;

    public MainWindowViewModel(AppServices services, IFilePicker picker)
    {
        _import = new ImportViewModel(services, picker);
        _current = _import;
        _databaseLabel = services.DatabasePath.Replace(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "~");
    }

    public void ShowImport() => Current = _import;
    public void ShowPeople() => Current = _people;
    public void ShowSend() => Current = _send;
    public void ShowLcr() => Current = _lcr;
    public void ShowSetup() => Current = _setup;
}
