using Avalonia;
using Avalonia.Headless;

[assembly: AvaloniaTestApplication(typeof(Courier.Tests.HeadlessApp))]

namespace Courier.Tests;

/// <summary>Boots the real application with no window server behind it.</summary>
public static class HeadlessApp
{
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<Courier.App.App>()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = true });
}
