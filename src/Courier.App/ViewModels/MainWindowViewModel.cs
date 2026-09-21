using CommunityToolkit.Mvvm.ComponentModel;
using Courier.Messaging.Settings;

namespace Courier.App.ViewModels;

public sealed partial class MainWindowViewModel : ObservableObject
{
    private readonly AppServices _services;
    private readonly IFilePicker _picker;
    private readonly IClipboardWriter _clipboard;
    private readonly ISettingsStore _store;

    private ImportViewModel _import = null!;
    private PeopleViewModel _people = null!;
    private SendViewModel _send = null!;
    private LcrBacklogViewModel _lcr = null!;
    private SetupViewModel _setup = null!;

    [ObservableProperty] private object _current;
    [ObservableProperty] private string _databaseLabel;
    [ObservableProperty] private string _senderLabel;

    public MainWindowViewModel(AppServices services, IFilePicker picker, IClipboardWriter clipboard)
    {
        _services = services;
        _picker = picker;
        _clipboard = clipboard;
        _store = new SettingsStore(services.SettingsPath);

        Build();

        var sender = _store.Load().Sender;
        _senderLabel = sender.IsComplete ? sender.Line.ToUpperInvariant() : "SET UP WHO THIS IS FROM";

        _current = _import;
        _databaseLabel = Shorten(services.DatabasePath);
    }

    /// <summary>Every screen holds a view of one database, so opening another means
    /// building them again rather than asking each to notice.</summary>
    private void Build()
    {
        _import = new ImportViewModel(_services, _picker);
        _people = new PeopleViewModel(_services);
        _send = new SendViewModel(_services, _store);
        _lcr = new LcrBacklogViewModel(_services, _clipboard);
        _setup = new SetupViewModel(
            _services, _store,
            databases: _picker as IDatabasePicker,
            databaseChanged: OnDatabaseChanged);
    }

    private void OnDatabaseChanged()
    {
        var setup = _setup;
        Build();
        DatabaseLabel = Shorten(_services.DatabasePath);

        // Keep the user on Setup, where they just pressed the button, and keep the
        // instance they are looking at so its message does not vanish.
        _setup = setup;
        Current = setup;
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
    private static string Shorten(string path) =>
        path.Replace(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "~");

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
