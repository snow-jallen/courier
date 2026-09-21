using Courier.Core.Diagnostics;
using Velopack;
using Velopack.Sources;

namespace Courier.App;

public sealed record UpdateState(string Message, bool UpdateReady = false, string? Version = null);

/// <summary>Checks GitHub for a newer Courier and installs it.
///
/// Only works in an app installed from a Velopack package — run from a build folder
/// there is nothing to replace, and saying so is better than failing obscurely.</summary>
public sealed class UpdateService(string repositoryUrl)
{
    private UpdateManager? _manager;
    private UpdateInfo? _pending;

    /// <summary>Where releases are published. The source repository is private, so this
    /// points at whichever repository actually carries the release assets.</summary>
    public const string DefaultRepository = "https://github.com/snow-jallen/courier";

    public static string CurrentVersion => JsonlActivityLog.Version;

    public bool Installed
    {
        get
        {
            try { return Manager.IsInstalled; }
            catch { return false; }
        }
    }

    private UpdateManager Manager =>
        _manager ??= new UpdateManager(new GithubSource(repositoryUrl, accessToken: null, prerelease: false));

    public async Task<UpdateState> CheckAsync()
    {
        if (!Installed)
            return new UpdateState(
                "Courier updates itself only when it has been installed from a release. This copy was run directly, so there is nothing to update — download a release to get updates.");

        try
        {
            _pending = await Manager.CheckForUpdatesAsync();
            if (_pending is null)
            {
                Log.Record("update.check", Log.Details(("found", false), ("version", CurrentVersion)));
                return new UpdateState($"Courier is up to date — version {CurrentVersion}.");
            }

            var version = _pending.TargetFullRelease.Version.ToString();
            Log.Record("update.check", Log.Details(("found", true), ("version", version)));
            return new UpdateState($"Version {version} is available.", UpdateReady: false, Version: version);
        }
        catch (Exception e)
        {
            Log.Failure("update.check", e);
            return new UpdateState(
                "Courier could not reach GitHub to look for an update. Check this computer is online and try again.");
        }
    }

    public async Task<UpdateState> DownloadAsync(IProgress<int>? progress = null)
    {
        if (_pending is null) return await CheckAsync();

        try
        {
            await Manager.DownloadUpdatesAsync(_pending, p => progress?.Report(p));
            var version = _pending.TargetFullRelease.Version.ToString();
            Log.Record("update.downloaded", Log.Details(("version", version)));
            return new UpdateState(
                $"Version {version} is ready. Courier will restart to finish installing it.",
                UpdateReady: true, Version: version);
        }
        catch (Exception e)
        {
            Log.Failure("update.download", e);
            return new UpdateState($"The update could not be downloaded. {e.Message}");
        }
    }

    /// <summary>Restarts into the new version. Nothing is in flight at this point: the
    /// directory is on disk and settings are saved as they are changed.</summary>
    public void ApplyAndRestart()
    {
        if (_pending is null) return;
        Log.Record("update.apply", Log.Details(("version", _pending.TargetFullRelease.Version.ToString())));
        Manager.ApplyUpdatesAndRestart(_pending);
    }
}
