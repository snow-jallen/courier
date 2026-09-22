namespace Courier.Tests;

/// <summary>Guards the one thing that decides whether a user can tell you which
/// Courier they are running.
///
/// The release workflow builds with <c>-p:Version=1.0.&lt;run number&gt;</c>. That
/// property only supplies the *default* for AssemblyVersion and FileVersion, so a
/// project file that sets either of them outright wins instead, and every release
/// reports the pinned number no matter what was built. It did exactly that: the app
/// told every user it was 1.0.0 from the first release through 1.0.6, on every
/// platform, while the installer said otherwise.
///
/// Nothing at run time can catch this — the number is baked in at compile time and is
/// perfectly self-consistent once it is wrong. So the check has to be on the project
/// file itself.</summary>
public sealed class ReleaseVersionTests
{
    [Fact]
    public void The_app_does_not_pin_a_version_the_release_is_meant_to_supply()
    {
        var csproj = Path.Combine(TestPaths.RepoRoot!, "src", "Courier.App", "Courier.App.csproj");
        var project = File.ReadAllText(csproj);

        Assert.DoesNotContain("<AssemblyVersion>", project, StringComparison.Ordinal);
        Assert.DoesNotContain("<FileVersion>", project, StringComparison.Ordinal);
    }

    /// <summary>A build with no version passed still needs a sensible one, or a
    /// developer running from source sees a blank where the version should be.</summary>
    [Fact]
    public void A_build_with_no_version_passed_still_has_one()
    {
        var csproj = Path.Combine(TestPaths.RepoRoot!, "src", "Courier.App", "Courier.App.csproj");
        var project = File.ReadAllText(csproj);

        Assert.Contains("<Version>", project, StringComparison.Ordinal);
    }
}
