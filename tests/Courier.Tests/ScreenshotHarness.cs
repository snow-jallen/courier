using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Courier.App;
using Courier.App.ViewModels;
using Courier.Data;
using Courier.Messaging.Settings;
using Xunit.Abstractions;

namespace Courier.Tests;

/// <summary>Drives the real window with rendering turned on and writes a picture of
/// each screen into .screenshots at the repository root.
///
/// This exists because a window that lays out wrongly still passes every assertion you
/// can write about it. Within minutes of the first capture it turned up a search box
/// overflowing off the left of the screen, an address column clipped at the card edge,
/// a header whose columns did not match the rows beneath it, and every subtitle centred
/// instead of sitting under its heading — none of which 258 tests had noticed.</summary>
public sealed class ScreenshotHarness(ITestOutputHelper output) : IDisposable
{
    private readonly string _folder =
        Path.Combine(TestPaths.RepoRoot ?? Path.GetTempPath(), ".screenshots");

    private sealed class NoFiles : IFilePicker { public Task<string?> PickPdfAsync() => Task.FromResult<string?>(null); }
    private sealed class NoClipboard : IClipboardWriter { public Task CopyAsync(string t) => Task.CompletedTask; }

    [Fact]
    public Task Photograph_every_screen() => HeadlessApp.Session.Value.Dispatch(async () =>
    {
        if (Directory.Exists(_folder)) Directory.Delete(_folder, recursive: true);
        Directory.CreateDirectory(_folder);
        var services = AppServices.Start(
            Path.Combine(_folder, "contacts.db"), Path.Combine(_folder, "settings.json"));

        await SeedAsync(services);

        var store = new SettingsStore(services.SettingsPath);
        store.Save(store.Load() with
        {
            Sender = new Courier.Core.Domain.SenderIdentity("Jonathan Allen", "Stake Singles Representative"),
        });

        var window = new Courier.App.Views.MainWindow(services);
        window.Width = 1400;
        window.Height = 900;
        window.Show();

        var model = (MainWindowViewModel)window.DataContext!;

        Shoot(window, "1-import");
        await model.ShowPeopleAsync(); Shoot(window, "2-people");
        await model.ShowSendAsync();
        ((SendViewModel)model.Current).Body = "Stake singles dinner this Friday, 6:30 PM at the stake center. Bring a side if you can.";
        Shoot(window, "3-send");
        await model.ShowLcrAsync(); Shoot(window, "4-lcr");
        await model.ShowHistoryAsync(); Shoot(window, "5-history");
        model.ShowSetup(); Shoot(window, "6-setup");

        var shots = Directory.GetFiles(_folder, "*.png");
        output.WriteLine($"FOLDER {_folder} — {shots.Length} images");
        Assert.Equal(6, shots.Length);
        Assert.All(shots, f => Assert.True(new FileInfo(f).Length > 5000, $"{f} is suspiciously small"));
    }, CancellationToken.None);

    /// <summary>No Task.Delay here: the headless dispatcher does not pump timers, so a
    /// delay never resumes and the whole run stops silently. A render tick is the
    /// supported way to make a frame happen.</summary>
    private void Shoot(Window window, string name)
    {
        window.Measure(new Size(window.Width, window.Height));
        window.Arrange(new Rect(0, 0, window.Width, window.Height));
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();

        var frame = window.CaptureRenderedFrame()
            ?? throw new InvalidOperationException($"nothing rendered for {name}");
        var path = Path.Combine(_folder, $"{name}.png");
        frame.Save(path, new Avalonia.Media.Imaging.PngBitmapEncoderOptions());
        output.WriteLine($"SHOT {path} {new FileInfo(path).Length} bytes");
    }

    private static async Task SeedAsync(AppServices services)
    {
        await using var db = services.Db();
        var directory = new DirectoryService(db);
        var people = new[]
        {
            ("Ashgrove", "Adelaide", "Manti 2nd Ward", "a.ashgrove@example.com", "(435) 555-0111"),
            ("Bellweather", "Horatio", "Manti 5th Ward", "", "(435) 555-0127"),
            ("Carrowmore", "Ophelia", "Sterling Ward", "o.carrowmore@example.com", "(435) 555-0133"),
            ("Denholm", "Verity", "Manti 9th Ward", "v.denholm@example.com", "(435) 555-0140"),
            ("Eastleigh", "Cordelia", "Manti 1st Ward", "", "(435) 555-0152"),
            ("Fairbourne", "Jemima", "Manti 6th Ward", "j.fairbourne@example.com", ""),
            ("Glenhaven", "Percival", "Manti 3rd Ward", "p.glenhaven@example.com", "(435) 555-0160"),
            ("Harrowfield", "Nathaniel", "Manti 4th Ward", "", "(435) 555-0166"),
        };
        foreach (var (last, first, ward, email, phone) in people)
            await directory.AddPersonAsync(last, first, ward, email, phone, null, null, AppServices.Today);
    }

    public void Dispose() { }
}
