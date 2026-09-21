using Courier.Data;
using Courier.Messaging.Settings;
using Microsoft.EntityFrameworkCore;

namespace Courier.App;

/// <summary>Everything the app needs to exist, built once at start-up. Small enough
/// that a container would be more machinery than it saves.</summary>
public sealed class AppServices
{
    /// <summary>Which directory file is open. Changeable, because the file does not have
    /// to live where Courier first put it.</summary>
    public string DatabasePath { get; private set; }

    /// <summary>Always in the same place, whatever the database does — it is where the
    /// database's location is recorded, so it cannot live beside it.</summary>
    public string SettingsPath { get; }

    private AppServices(string databasePath, string settingsPath)
    {
        DatabasePath = databasePath;
        SettingsPath = settingsPath;
    }

    /// <summary>The paths are only ever passed in by tests; the app reads the database's
    /// location from its settings, falling back to the usual place.</summary>
    public static AppServices Start(string? databasePath = null, string? settingsPath = null)
    {
        var settings = settingsPath ?? Path.Combine(CourierDatabase.DefaultFolder, "settings.json");
        var chosen = databasePath
            ?? Blank(new SettingsStore(settings).Load().DatabasePath)
            ?? CourierDatabase.DefaultPath;

        var services = new AppServices(chosen, settings);
        Prepare(chosen);
        return services;
    }

    /// <summary>Opens a different directory file. Anything that is not one is rejected
    /// before it becomes the open database, rather than after.</summary>
    public void SwitchTo(string path)
    {
        Prepare(path);
        DatabasePath = path;
    }

    private static void Prepare(string path)
    {
        var folder = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(folder)) Directory.CreateDirectory(folder);

        using var db = CourierDatabase.Open(path);
        db.Database.Migrate();
    }

    private static string? Blank(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

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

/// <summary>Choosing a directory file: an existing one, or somewhere to make a new one.</summary>
public interface IDatabasePicker
{
    Task<string?> PickExistingAsync();
    Task<string?> PickNewAsync();
}

/// <summary>Same again for the clipboard, which lives on the window.</summary>
public interface IClipboardWriter
{
    Task CopyAsync(string text);
}
