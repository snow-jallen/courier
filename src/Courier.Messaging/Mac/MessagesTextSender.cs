using Courier.Core.Domain;
using Courier.Messaging.Twilio;

namespace Courier.Messaging.Mac;

/// <summary>Sends texts through the Messages app on this Mac, the way Phone Link sends
/// them through an Android phone: the computer asks, the phone's own line delivers.
///
/// What this buys is the thing no messaging service can sell — the message genuinely
/// comes from the user's own number, and replies arrive in their own Messages app
/// rather than somewhere they would have to remember to check.
///
/// The cost is that a personal line is meant for person-to-person texting. Carriers
/// watch for bursts, so this is for a ward or a committee, not for all 427 people.</summary>
public sealed class MessagesTextSender(IAppleScriptRunner runner, TimeSpan? pause = null) : IMessageSender
{
    /// <summary>A gap between messages. Messages drops them if pushed, and a burst from
    /// a consumer line is what carrier spam filters are looking for.</summary>
    private readonly TimeSpan _pause = pause ?? TimeSpan.FromSeconds(2);

    /// <summary>Above this many recipients, a personal line is the wrong tool. Courier
    /// still sends, but the Send screen says so first.</summary>
    public const int ComfortableBatch = 50;

    public Channel Channel => Channel.Text;

    public bool IsConfigured => runner.IsAvailable;

    private const string Script = """
        on run argv
            set phoneNumber to item 1 of argv
            set messageText to item 2 of argv
            tell application "Messages"
                try
                    set targetService to 1st account whose service type = iMessage
                    send messageText to participant phoneNumber of targetService
                on error
                    set targetService to 1st account whose service type = SMS
                    send messageText to participant phoneNumber of targetService
                end try
            end tell
        end run
        """;

    public async Task<SendOutcome> SendAsync(
        string address, OutgoingMessage message, CancellationToken cancellation = default)
    {
        if (!runner.IsAvailable)
            return SendOutcome.Failed(
                "Sending from the Messages app only works on a Mac. On Windows or Linux, use Twilio instead — the Setup screen has both.");

        if (!PhoneNumbers.IsDiallable(address))
            return SendOutcome.Failed(PhoneNumbers.Complaint(address));

        try
        {
            var result = await runner.RunAsync(Script, [address.Trim(), message.Body], cancellation);
            if (!result.Ok) return SendOutcome.Failed(Explain(result.Error, address));

            // Let Messages settle before the next one.
            if (_pause > TimeSpan.Zero) await Task.Delay(_pause, cancellation);

            // Messages reports nothing back, so there is no id to record and no way to
            // know it reached the network. "Sent" here means "handed to Messages".
            return SendOutcome.Sent(null);
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception failure)
        {
            return SendOutcome.Failed(
                $"Courier could not reach the Messages app. {failure.Message}");
        }
    }

    public async Task<CredentialCheck> TestAsync(string address, CancellationToken cancellation = default)
    {
        if (!runner.IsAvailable)
            return CredentialCheck.Broken("This only works on a Mac. Use Twilio for texting on Windows or Linux.");

        if (string.IsNullOrWhiteSpace(address))
            return CredentialCheck.Broken("Add your own mobile number on the Setup screen so Courier has somewhere to send the test.");

        var outcome = await SendAsync(
            address,
            new OutgoingMessage("", "This is a test from Courier, sent through the Messages app on your Mac. Nobody else was sent anything."),
            cancellation);

        return outcome.Status == SendStatus.Sent
            ? CredentialCheck.Working($"Handed a test message to Messages for {address}. It should appear in your own conversation with yourself.")
            : CredentialCheck.Broken(outcome.Error ?? "Courier could not send through Messages.");
    }

    /// <summary>Turns osascript's complaints into something to act on. The first one is
    /// almost always what has happened the first time somebody presses the button.</summary>
    private static string Explain(string stderr, string address)
    {
        if (stderr.Contains("Not authorized", StringComparison.OrdinalIgnoreCase)
            || stderr.Contains("-1743", StringComparison.Ordinal))
            return "macOS has not let Courier control the Messages app yet. Open System Settings, go to Privacy & Security, then Automation, find Courier and switch on Messages. Then try again.";

        if (stderr.Contains("Application isn't running", StringComparison.OrdinalIgnoreCase)
            || stderr.Contains("-600", StringComparison.Ordinal))
            return "The Messages app is not running. Open Messages, make sure you are signed in, and try again.";

        if (stderr.Contains("Invalid handle", StringComparison.OrdinalIgnoreCase)
            || stderr.Contains("can't get participant", StringComparison.OrdinalIgnoreCase))
            return $"Messages would not accept {address}. If that person has no iMessage, your iPhone has to be set up to forward text messages to this Mac — on the phone, Settings, Apps, Messages, Text Message Forwarding.";

        return $"Messages refused to send to {address}. It said: {stderr}";
    }
}
