using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Courier.Core.Diagnostics;
using Courier.Messaging.Settings;

namespace Courier.App.ViewModels;

public sealed partial class MainWindowViewModel : ObservableObject
{
    private readonly AppServices _services;
    private readonly IFilePicker _picker;
    private readonly IClipboardWriter _clipboard;
    private readonly ISettingsStore _store;

    /// <summary>Shared with the Setup screen rather than one each: the updater holds
    /// the downloaded update in a field, so two of them would mean the rail fetches
    /// something the Setup screen's restart button knows nothing about.</summary>
    private readonly IUpdates _updates;

    private ImportViewModel _import = null!;
    private PeopleViewModel _people = null!;
    private SendViewModel _send = null!;
    private LcrBacklogViewModel _lcr = null!;
    private HistoryViewModel _history = null!;
    private SetupViewModel _setup = null!;

    [ObservableProperty] private object _current;
    [ObservableProperty] private string _databaseLabel;
    [ObservableProperty] private string _senderLabel;

    /// <summary>The running version where a user can always see it, so "which version
    /// are you on?" is answered by looking rather than by hunting through Setup.</summary>
    public static string WindowTitle =>
        UpdateService.CurrentVersion is "unknown"
            ? "Courier"
            : $"Courier (v{UpdateService.CurrentVersion})";

    /// <summary>True once a newer Courier is downloaded and waiting. The rail shows a
    /// restart button only then, so the rest of the time it looks exactly as it did.</summary>
    [ObservableProperty] private bool _updateReady;

    /// <summary>Looks for a newer Courier and fetches it in the background, leaving the
    /// user nothing to do but restart when it suits them.
    ///
    /// Silent from end to end. Nobody asked for this, so a copy run from a build
    /// folder, a machine that is offline, and a version that is already current all
    /// look the same from the rail: nothing appears. Setup's own button still reports
    /// every one of those out loud, because there the user did ask.</summary>
    public async Task CheckForUpdateAsync()
    {
        try
        {
            if (!_updates.Installed) return;

            var found = await _updates.CheckAsync();
            if (found.Version is null) return;

            var ready = await _updates.DownloadAsync();
            UpdateReady = ready.UpdateReady;
        }
        catch (Exception failure)
        {
            Log.Failure("update.startup", failure);
        }
    }

    [RelayCommand]
    private void RestartToUpdate() => _updates.ApplyAndRestart();

    public MainWindowViewModel(
        AppServices services, IFilePicker picker, IClipboardWriter clipboard, IUpdates? updates = null)
    {
        _services = services;
        _picker = picker;
        _clipboard = clipboard;
        _updates = updates ?? new UpdateService(UpdateService.DefaultRepository);
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
        _history = new HistoryViewModel(_services, _clipboard);
        _setup = new SetupViewModel(
            _services, _store,
            databases: _picker as IDatabasePicker,
            databaseChanged: OnDatabaseChanged,
            clipboard: _clipboard,
            updates: _updates);
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
        Log.Record("screen.open", Log.Details(("screen", "import")));
        RefreshSender();
        Current = _import;
    }

    /// <summary>The list screens reload each time they are opened, so a channel chosen
    /// on one of them, or an import just applied, is reflected on the others without
    /// anything having to coordinate.</summary>
    public async Task ShowPeopleAsync()
    {
        Log.Record("screen.open", Log.Details(("screen", "people")));
        RefreshSender();
        Current = _people;
        await _people.LoadAsync();
    }

    public async Task ShowSendAsync()
    {
        Log.Record("screen.open", Log.Details(("screen", "send")));
        RefreshSender();
        Current = _send;
        await _send.LoadAsync();
    }

    public async Task ShowLcrAsync()
    {
        Log.Record("screen.open", Log.Details(("screen", "lcr")));
        RefreshSender();
        Current = _lcr;
        await _lcr.LoadAsync();
    }

    public async Task ShowHistoryAsync()
    {
        Log.Record("screen.open", Log.Details(("screen", "history")));
        RefreshSender();
        Current = _history;
        await _history.LoadAsync();
    }

    public void ShowSetup()
    {
        Log.Record("screen.open", Log.Details(("screen", "setup")));
        Current = _setup;
    }

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
