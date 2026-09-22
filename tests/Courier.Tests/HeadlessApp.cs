using Avalonia;
using Avalonia.Headless;

[assembly: AvaloniaTestApplication(typeof(Courier.Tests.HeadlessApp))]

namespace Courier.Tests;

/// <summary>Boots the real application with no window server behind it.
///
/// One definition for the whole assembly: Avalonia initialises its platform once per
/// process, so a second session with different options fails on whichever test happens
/// to run second. Drawing is real rather than stubbed, so the screenshot harness can
/// capture frames and the layout tests measure what would actually be painted.</summary>
public static class HeadlessApp
{
    /// <summary>The one session for the whole assembly. Two of them means two
    /// dispatchers, and whichever test runs second fails with "a different thread owns
    /// this object" — which is not a clue anybody enjoys following.</summary>
    public static readonly Lazy<HeadlessUnitTestSession> Session =
        new(() => HeadlessUnitTestSession.StartNew(typeof(HeadlessApp)));

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<Courier.App.App>()
            .UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
}
