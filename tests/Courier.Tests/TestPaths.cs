namespace Courier.Tests;

internal static class TestPaths
{
    /// <summary>The repository root, found by walking up from the test assembly.
    /// Null rather than throwing, because xunit builds skip attributes during
    /// discovery and an exception there fails the whole run.</summary>
    public static string? RepoRoot
    {
        get
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Directory.Build.props")))
                dir = dir.Parent;
            return dir?.FullName;
        }
    }

    /// <summary>A real LCR export, if one has been placed in the gitignored folder.
    /// Null on a machine that has never seen the real directory.</summary>
    public static string? RealReport
    {
        get
        {
            if (RepoRoot is null) return null;
            var path = Path.Combine(RepoRoot, "tests", "Courier.Tests", "Fixtures", "private", "manti-singles.pdf");
            return File.Exists(path) ? path : null;
        }
    }
}

/// <summary>Marks a test that needs a real LCR export and skips it when none is present.</summary>
public sealed class RequiresRealReportAttribute : FactAttribute
{
    public RequiresRealReportAttribute()
    {
        if (TestPaths.RealReport is null)
            Skip = "No LCR export in tests/Courier.Tests/Fixtures/private/ — see the README there.";
    }
}
