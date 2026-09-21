using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using Courier.Core.Diagnostics;

namespace Courier.App;

/// <summary>Writes the log as one JSON object per line, which is the format that can be
/// read by eye in a text editor and fed to a script without parsing anything.
///
/// Every write is best-effort: a diagnostic that can take the app down is worse than no
/// diagnostic at all.</summary>
public sealed class JsonlActivityLog : IActivityLog
{
    private static readonly JsonSerializerOptions Compact = new() { WriteIndented = false };

    private readonly object _gate = new();
    private readonly string _folder;
    private readonly string _session = Guid.NewGuid().ToString("N")[..8];
    private readonly Stopwatch _since = Stopwatch.StartNew();

    public JsonlActivityLog(string folder, int keepDays = 30)
    {
        _folder = folder;
        Directory.CreateDirectory(folder);
        Tidy(keepDays);
        Record("session.start", Log.Details(
            ("version", Version),
            ("os", RuntimeInformation.OSDescription),
            ("arch", RuntimeInformation.OSArchitecture.ToString()),
            ("dotnet", RuntimeInformation.FrameworkDescription),
            ("culture", System.Globalization.CultureInfo.CurrentCulture.Name),
            ("session", _session)));
    }

    public string Folder => _folder;

    public string TodaysFile =>
        Path.Combine(_folder, $"courier-{DateTime.Now:yyyy-MM-dd}.jsonl");

    public static string Version =>
        Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? "unknown";

    public void Record(string action, IReadOnlyDictionary<string, object?>? details = null) =>
        Write("info", action, details, error: null);

    public void Failure(string action, Exception error, IReadOnlyDictionary<string, object?>? details = null) =>
        Write("error", action, details, error);

    private void Write(string level, string action, IReadOnlyDictionary<string, object?>? details, Exception? error)
    {
        try
        {
            var line = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["at"] = DateTimeOffset.Now.ToString("O"),
                ["since"] = (int)_since.Elapsed.TotalSeconds,
                ["session"] = _session,
                ["level"] = level,
                ["action"] = action,
            };
            if (details is { Count: > 0 }) line["details"] = details;
            if (error is not null) line["error"] = Describe(error);

            lock (_gate)
            {
                File.AppendAllText(TodaysFile, JsonSerializer.Serialize(line, Compact) + Environment.NewLine,
                    Encoding.UTF8);
            }
        }
        catch
        {
            // A log that can break the app is worse than no log.
        }
    }

    /// <summary>The exception and everything under it. The inner one is usually the
    /// one that says what actually happened.</summary>
    private static object Describe(Exception error)
    {
        var chain = new List<object>();
        for (var current = error; current is not null && chain.Count < 5; current = current.InnerException)
        {
            chain.Add(new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["type"] = current.GetType().FullName,
                ["message"] = Redact.Failure(current.Message),
                ["where"] = Where(current),
            });
        }
        return chain;
    }

    /// <summary>The first few frames of our own code, which is the part worth reading.
    /// Framework frames are dropped.</summary>
    private static string Where(Exception error)
    {
        var frames = (error.StackTrace ?? "")
            .Split('\n')
            .Select(line => line.Trim())
            .Where(line => line.Contains("Courier.", StringComparison.Ordinal))
            .Take(6);
        return string.Join(" | ", frames);
    }

    private void Tidy(int keepDays)
    {
        try
        {
            var cutoff = DateTime.Now.AddDays(-keepDays);
            foreach (var file in Directory.EnumerateFiles(_folder, "courier-*.jsonl"))
                if (File.GetLastWriteTime(file) < cutoff) File.Delete(file);
        }
        catch
        {
            // Housekeeping is not worth a crash either.
        }
    }

    /// <summary>The recent log as text, for pasting into a message to whoever is
    /// helping. Newest lines last, as they were written.</summary>
    public string Recent(int lines = 400)
    {
        try
        {
            var files = Directory.EnumerateFiles(_folder, "courier-*.jsonl")
                .OrderByDescending(File.GetLastWriteTime)
                .Take(3)
                .Reverse()
                .ToList();

            var all = files.SelectMany(File.ReadAllLines).ToList();
            return string.Join(Environment.NewLine, all.Skip(Math.Max(0, all.Count - lines)));
        }
        catch (Exception e)
        {
            return $"The log could not be read: {e.Message}";
        }
    }
}
