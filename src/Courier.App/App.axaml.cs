using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Courier.App.Views;

namespace Courier.App;

public partial class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var window = new MainWindow(AppServices.Start());
            desktop.MainWindow = window;

            // Deliberately not awaited: the window opens now, and the rail grows a
            // restart button later if there turns out to be anything to restart into.
            window.StartUpdateCheck();
        }

        base.OnFrameworkInitializationCompleted();
    }
}
