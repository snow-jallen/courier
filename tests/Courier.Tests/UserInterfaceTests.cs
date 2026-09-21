using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Courier.App;
using Courier.App.ViewModels;
using Courier.App.Views;
using Courier.Data;
using Courier.Messaging.Settings;
using Microsoft.EntityFrameworkCore;

namespace Courier.Tests;

/// <summary>Builds the real window against a real database, with no screen attached.
///
/// A view model compiles whatever its XAML says, so a mistyped binding, a resource that
/// does not exist or a template bound to the wrong type is invisible until someone opens
/// the app. These walk every screen and would have caught each of those.</summary>
public sealed class UserInterfaceTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), $"courier-ui-{Guid.NewGuid():N}");

    private sealed class NoFiles : IFilePicker
    {
        public Task<string?> PickPdfAsync() => Task.FromResult<string?>(null);
    }

    private sealed class NoClipboard : IClipboardWriter
    {
        public Task CopyAsync(string text) => Task.CompletedTask;
    }

    private static readonly Lazy<HeadlessUnitTestSession> Session = new(() =>
        HeadlessUnitTestSession.StartNew(typeof(HeadlessApp)));

    private static Task InWindow(Func<MainWindow, MainWindowViewModel, Task> body, string folder) =>
        Session.Value.Dispatch(async () =>
        {
            Directory.CreateDirectory(folder);
            var services = AppServices.Start(Path.Combine(folder, "contacts.db"));
            var window = new MainWindow(services);
            window.Show();

            var model = (MainWindowViewModel)window.DataContext!;
            await body(window, model);
        }, CancellationToken.None);

    [Fact]
    public Task The_window_opens_on_the_import_screen() => InWindow((window, model) =>
    {
        Assert.IsType<ImportViewModel>(model.Current);
        Assert.Equal("Courier", window.Title);
        return Task.CompletedTask;
    }, _folder);

    [Fact]
    public Task Every_screen_opens() => InWindow(async (_, model) =>
    {
        await model.ShowPeopleAsync();
        Assert.IsType<PeopleViewModel>(model.Current);

        await model.ShowSendAsync();
        Assert.IsType<SendViewModel>(model.Current);

        await model.ShowLcrAsync();
        Assert.IsType<LcrBacklogViewModel>(model.Current);

        model.ShowSetup();
        Assert.IsType<SetupViewModel>(model.Current);

        model.ShowImport();
        Assert.IsType<ImportViewModel>(model.Current);
    }, _folder);

    [Fact]
    public Task A_new_database_shows_an_empty_directory_rather_than_failing() =>
        InWindow(async (_, model) =>
        {
            await model.ShowPeopleAsync();
            var people = (PeopleViewModel)model.Current;
            Assert.Empty(people.Rows);
            Assert.Equal("0 people", people.Summary);

            await model.ShowLcrAsync();
            var lcr = (LcrBacklogViewModel)model.Current;
            Assert.True(lcr.IsEmpty);
            Assert.Contains("Nothing waiting", lcr.Summary, StringComparison.Ordinal);
        }, _folder);

    [Fact]
    public Task The_send_screen_offers_nobody_when_the_directory_is_empty() =>
        InWindow(async (_, model) =>
        {
            await model.ShowSendAsync();
            var send = (SendViewModel)model.Current;
            Assert.Empty(send.Rows);
            Assert.Equal("Send to 0 people", send.SendLabel);
        }, _folder);

    [Fact]
    public Task Setup_starts_from_the_settings_on_disk_and_says_where_the_file_is() =>
        InWindow((_, model) =>
        {
            model.ShowSetup();
            var setup = (SetupViewModel)model.Current;

            Assert.Equal("smtp.gmail.com", new SettingsStore(setup.SettingsPath).Load().Email.Host);
            Assert.EndsWith("contacts.db", setup.DatabasePath, StringComparison.Ordinal);
            Assert.False(setup.HasRecording);
            return Task.CompletedTask;
        }, _folder);

    public void Dispose()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        if (Directory.Exists(_folder)) Directory.Delete(_folder, recursive: true);
    }
}
