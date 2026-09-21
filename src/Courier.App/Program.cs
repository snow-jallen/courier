using Avalonia;
using Courier.Core.Diagnostics;
using System;

namespace Courier.App;

class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        // Must be the first thing that happens: on the run straight after an update this
        // is what finishes installing it, and it exits rather than returning.
        Velopack.VelopackApp.Build().Run();

        // A crash is the one thing a user cannot describe usefully, so it is the one
        // thing most worth writing down.
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            if (e.ExceptionObject is Exception error) Log.Failure("app.crash", error);
        };
        System.Threading.Tasks.TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            Log.Failure("app.unobserved", e.Exception);
            e.SetObserved();
        };

        try
        {
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        catch (Exception error)
        {
            Log.Failure("app.start", error);
            throw;
        }

        Log.Record("session.end");
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
#if DEBUG
            .WithDeveloperTools()
#endif
            .WithInterFont()
            .LogToTrace();
}
