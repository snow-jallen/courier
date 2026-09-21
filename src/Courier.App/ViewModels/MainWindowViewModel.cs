using CommunityToolkit.Mvvm.ComponentModel;
using Courier.Messaging.Settings;

namespace Courier.App.ViewModels;

public sealed partial class MainWindowViewModel : ObservableObject
{
    private readonly ISettingsStore _store;
    private readonly ImportViewModel _import;
    private readonly PeopleViewModel _people;
    private readonly SendViewModel _send;
    private readonly LcrBacklogViewModel _lcr;
    private readonly SetupViewModel _setup;

    [ObservableProperty] private object _current;
    [ObservableProperty] private string _databaseLabel;
    [ObservableProperty] private string _senderLabel;

    public MainWindowViewModel(AppServices services, IFilePicker picker, IClipboardWriter clipboard)
    {
        _store = new SettingsStore(services.SettingsPath);
        var store = _store;

        _import = new ImportViewModel(services, picker);
        _people = new PeopleViewModel(services);
        _send = new SendViewModel(services, store);
        _lcr = new LcrBacklogViewModel(services, clipboard);
        _setup = new SetupViewModel(services, store);

        var sender = store.Load().Sender;
        _senderLabel = sender.IsComplete ? sender.Line.ToUpperInvariant() : "SET UP WHO THIS IS FROM";

        _current = _import;
        _databaseLabel = services.DatabasePath.Replace(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "~");
    }

    public void ShowImport()
    {
        RefreshSender();
        Current = _import;
    }

    /// <summary>The list screens reload each time they are opened, so a channel chosen
    /// on one of them, or an import just applied, is reflected on the others without
    /// anything having to coordinate.</summary>
    public async Task ShowPeopleAsync()
    {
        RefreshSender();
        Current = _people;
        await _people.LoadAsync();
    }

    public async Task ShowSendAsync()
    {
        RefreshSender();
        Current = _send;
        await _send.LoadAsync();
    }

    public async Task ShowLcrAsync()
    {
        RefreshSender();
        Current = _lcr;
        await _lcr.LoadAsync();
    }

    public void ShowSetup() => Current = _setup;

    /// <summary>The rail names the person whose calling this serves, not an
    /// organisation: Courier augments an individual and should not look official.
    /// Re-read on every move between screens, so a name typed into Setup shows up
    /// without anything having to notify anything.</summary>
    private void RefreshSender()
    {
        var sender = _store.Load().Sender;
        SenderLabel = sender.IsComplete ? sender.Line.ToUpperInvariant() : "SET UP WHO THIS IS FROM";
    }
}

/// <summary>Shown when a screen cannot be opened, in place of the screen.</summary>
public sealed class ScreenFailedViewModel(string message)
{
    public string Message { get; } = message;
}
