using System.Diagnostics;
using System.Text;

namespace Courier.Messaging.Mac;

public sealed record AppleScriptResult(int ExitCode, string Output, string Error)
{
    public bool Ok => ExitCode == 0;
}

/// <summary>Runs an AppleScript. Behind an interface so everything above it — the
/// wording of each failure, the guard against running off a Mac — is testable on any
/// machine, with no Messages app and nobody receiving a text.</summary>
public interface IAppleScriptRunner
{
    bool IsAvailable { get; }
    Task<AppleScriptResult> RunAsync(string script, IReadOnlyList<string> arguments, CancellationToken ct);
}

public sealed class AppleScriptRunner : IAppleScriptRunner
{
    public bool IsAvailable => OperatingSystem.IsMacOS();

    public async Task<AppleScriptResult> RunAsync(
        string script, IReadOnlyList<string> arguments, CancellationToken ct)
    {
        // The script comes in on stdin and the message text goes in as an argument, so
        // nothing ever has to be escaped into AppleScript source. An apostrophe in
        // "Relief Society's dinner" would otherwise end the string and change the
        // program.
        var start = new ProcessStartInfo("/usr/bin/osascript")
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        start.ArgumentList.Add("-");
        foreach (var argument in arguments) start.ArgumentList.Add(argument);

        using var process = Process.Start(start)
            ?? throw new InvalidOperationException("osascript could not be started.");

        await process.StandardInput.WriteAsync(script.AsMemory(), ct);
        process.StandardInput.Close();

        var stdout = process.StandardOutput.ReadToEndAsync(ct);
        var stderr = process.StandardError.ReadToEndAsync(ct);
        await process.WaitForExitAsync(ct);

        return new AppleScriptResult(process.ExitCode, (await stdout).Trim(), (await stderr).Trim());
    }
}
