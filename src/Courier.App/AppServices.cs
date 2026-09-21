using Courier.Data;
using Courier.Messaging.Settings;
using Microsoft.EntityFrameworkCore;

namespace Courier.App;

/// <summary>Everything the app needs to exist, built once at start-up. Small enough
/// that a container would be more machinery than it saves.</summary>
public sealed class AppServices
{
    public string DatabasePath { get; }
    public string SettingsPath { get; }

    private AppServices(string databasePath)
    {
        DatabasePath = databasePath;
        SettingsPath = Path.Combine(Path.GetDirectoryName(databasePath)!, "settings.json");
    }

    /// <summary>The path is only ever passed in by tests; the app itself keeps the
    /// directory where the user can find it.</summary>
    public static AppServices Start(string? databasePath = null)
    {
        var services = new AppServices(databasePath ?? CourierDatabase.DefaultPath);
        using var db = CourierDatabase.Open(services.DatabasePath);
        db.Database.Migrate();
        return services;
    }

    /// <summary>A context per unit of work. Desktop app, one user, no sharing.</summary>
    public CourierDbContext Db() => CourierDatabase.Open(DatabasePath);

    /// <summary>The day the user is living in. The server never decides this and
    /// neither does UTC.</summary>
    public static DateOnly Today => DateOnly.FromDateTime(DateTime.Now);
}

/// <summary>Lets a view model ask for a file without knowing about windows.</summary>
public interface IFilePicker
{
    Task<string?> PickPdfAsync();
}

/// <summary>Same again for the clipboard, which lives on the window.</summary>
public interface IClipboardWriter
{
    Task CopyAsync(string text);
}
