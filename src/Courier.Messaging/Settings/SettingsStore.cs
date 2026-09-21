using System.Text.Json;

namespace Courier.Messaging.Settings;

/// <summary>Keeps the accounts Courier sends through in a JSON file beside the
/// database. Deliberately not in the database: credentials are not directory data and
/// should not ride along in its backups.</summary>
public sealed class SettingsStore(string path) : ISettingsStore
{
    private static readonly JsonSerializerOptions Format = new() { WriteIndented = true };

    public string Path => path;

    /// <summary>Where a file that could not be read is moved, so a typo or a half-written
    /// file never silently costs someone their Twilio token. Nothing reads it back —
    /// it is there so the user can open it and copy a value out.</summary>
    public string SalvagePath => path + ".unreadable";

    public CourierSettings Load()
    {
        if (!File.Exists(path)) return new CourierSettings();

        try
        {
            return JsonSerializer.Deserialize<CourierSettings>(File.ReadAllText(path)) ?? new CourierSettings();
        }
        catch (Exception e) when (e is JsonException or IOException or UnauthorizedAccessException)
        {
            // Starting from defaults is the only way forward, but overwriting the old
            // file on the next save would destroy credentials the user may not have
            // written down anywhere else. Keep it.
            TrySalvage();
            return new CourierSettings();
        }
    }

    public void Save(CourierSettings settings)
    {
        var folder = System.IO.Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(folder)) Directory.CreateDirectory(folder);

        // Write beside the target and move it into place, so a crash half way through
        // cannot leave a file that parses as neither the old settings nor the new.
        var temporary = path + ".writing";
        File.WriteAllText(temporary, JsonSerializer.Serialize(settings, Format));
        Restrict(temporary);
        File.Move(temporary, path, overwrite: true);
        Restrict(path);
    }

    /// <summary>Owner-only. This file holds a mail app password and a Twilio token,
    /// and unlike the directory itself those can be used to spend money.</summary>
    private static void Restrict(string file)
    {
        if (OperatingSystem.IsWindows()) return;
        try
        {
            File.SetUnixFileMode(file, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or PlatformNotSupportedException)
        {
            // A file system that cannot express permissions is not a reason to refuse
            // to save the settings.
        }
    }

    private void TrySalvage()
    {
        try
        {
            File.Move(path, SalvagePath, overwrite: true);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // Nothing more to do; Load still returns usable defaults.
        }
    }
}
