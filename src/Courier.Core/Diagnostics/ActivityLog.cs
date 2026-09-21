namespace Courier.Core.Diagnostics;

/// <summary>Records what the app was asked to do and what went wrong, so somebody
/// helping a user afterwards can see the sequence rather than guess at it.</summary>
public interface IActivityLog
{
    void Record(string action, IReadOnlyDictionary<string, object?>? details = null);
    void Failure(string action, Exception error, IReadOnlyDictionary<string, object?>? details = null);
}

public sealed class NullActivityLog : IActivityLog
{
    public static readonly NullActivityLog Instance = new();
    public void Record(string action, IReadOnlyDictionary<string, object?>? details = null) { }
    public void Failure(string action, Exception error, IReadOnlyDictionary<string, object?>? details = null) { }
}

/// <summary>Where the rest of the app reaches the log.
///
/// Deliberately a static hand-off rather than a parameter on every method. Logging is
/// the one concern that belongs everywhere, and threading it through each service would
/// change every signature in the app to record a line of diagnostics. It starts as a
/// no-op, so nothing writes anything until the app says where.</summary>
public static class Log
{
    private static IActivityLog _app = NullActivityLog.Instance;

    /// <summary>An override confined to one flow of execution. Without this, two tests
    /// running side by side would each redirect the other's lines into the wrong file —
    /// which is exactly what a single shared static does.</summary>
    private static readonly AsyncLocal<IActivityLog?> Scoped = new();

    public static IActivityLog Current => Scoped.Value ?? _app;

    /// <summary>Where the running application writes. Set once, at start-up.</summary>
    public static void UseForApp(IActivityLog sink) => _app = sink;

    /// <summary>Redirects logging for this flow of execution only, until disposed.</summary>
    public static IDisposable Use(IActivityLog sink)
    {
        var previous = Scoped.Value;
        Scoped.Value = sink;
        return new Restore(() => Scoped.Value = previous);
    }

    public static void Record(string action, IReadOnlyDictionary<string, object?>? details = null) =>
        Current.Record(action, details);

    public static void Failure(string action, Exception error, IReadOnlyDictionary<string, object?>? details = null) =>
        Current.Failure(action, error, details);

    /// <summary>Shorthand for the common case: a handful of named values.</summary>
    public static Dictionary<string, object?> Details(params (string Key, object? Value)[] pairs)
    {
        var details = new Dictionary<string, object?>(pairs.Length, StringComparer.Ordinal);
        foreach (var (key, value) in pairs) details[key] = value;
        return details;
    }

    private sealed class Restore(Action undo) : IDisposable
    {
        public void Dispose() => undo();
    }
}
