using System.Security;
using Courier.Core.Domain;
using Courier.Messaging.Settings;

namespace Courier.Messaging.Twilio;

/// <summary>The call the user records once, which then plays to everyone who prefers
/// a phone call.</summary>
public sealed record RecordingCall(string CallSid, string Message);

/// <summary>Rings people and plays the message the user recorded in their own voice.
///
/// There is no text-to-speech and nothing is hosted anywhere. Courier rings the user,
/// records them, and Twilio keeps that recording; the broadcast then plays it back by
/// its Twilio address. Both halves pass their TwiML inline when the call is created,
/// which is what lets a desktop app with no public address of its own place calls at
/// all — the usual arrangement needs a web server for Twilio to fetch instructions
/// from.</summary>
public sealed class VoiceSender(ITwilioGateway gateway, TwilioSettings settings) : IMessageSender
{
    public Channel Channel => Channel.Voice;

    /// <summary>Configured means an account *and* something to play. A call that
    /// connects to silence is worse than one that never goes out.</summary>
    public bool IsConfigured => settings.IsComplete && settings.VoiceRecordingUrl.Length > 0;

    public bool HasRecording => settings.VoiceRecordingUrl.Length > 0;

    public async Task<SendOutcome> SendAsync(
        string address, OutgoingMessage message, CancellationToken cancellation = default)
    {
        if (!settings.IsComplete)
            return SendOutcome.Failed(
                "Courier has no Twilio account to call from yet. Add your Account SID, Auth Token and Twilio number on the Setup screen.");

        var recording = message.VoiceRecordingUrl is { Length: > 0 } fromMessage
            ? fromMessage
            : settings.VoiceRecordingUrl;

        if (recording.Length == 0)
            return SendOutcome.Failed(
                "You haven't recorded your message yet. On the Send screen choose Record my message, and Courier will ring you so you can speak it.");

        if (!PhoneNumbers.IsDiallable(address))
            return SendOutcome.Failed(PhoneNumbers.Complaint(address));

        try
        {
            var sid = await gateway.StartCallAsync(settings.FromNumber, address.Trim(), Play(recording), cancellation);
            return SendOutcome.Sent(sid);
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception failure)
        {
            return SendOutcome.Failed(TwilioProblem.Explain(failure, address, settings));
        }
    }

    public async Task<CredentialCheck> TestAsync(string address, CancellationToken cancellation = default)
    {
        if (!settings.IsComplete)
            return CredentialCheck.Broken("Fill in your Twilio details first, then try again.");

        var destination = string.IsNullOrWhiteSpace(address) ? settings.TestNumber : address.Trim();
        if (string.IsNullOrWhiteSpace(destination))
            return CredentialCheck.Broken("Add your own mobile number on the Setup screen so Courier has somewhere to call.");

        if (!HasRecording)
            return CredentialCheck.Broken("Record your message first, then Courier can ring you and play it back.");

        var outcome = await SendAsync(destination, new OutgoingMessage("", ""), cancellation);

        return outcome.Status == SendStatus.Sent
            ? CredentialCheck.Working($"Calling {destination} now and playing your recording back to you.")
            : CredentialCheck.Broken(outcome.Error ?? "Courier could not place the test call.");
    }

    /// <summary>Rings the user so they can speak the message. They hang up when done;
    /// the recording is then fetched with <see cref="CollectRecordingAsync"/>.</summary>
    public async Task<RecordingCall?> StartRecordingAsync(CancellationToken cancellation = default)
    {
        if (!settings.IsComplete || string.IsNullOrWhiteSpace(settings.TestNumber)) return null;

        try
        {
            var sid = await gateway.StartCallAsync(
                settings.FromNumber, settings.TestNumber, RecordPrompt, cancellation);
            return new RecordingCall(sid,
                $"Courier is ringing {settings.TestNumber}. Speak your message after the beep, then hang up.");
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception failure)
        {
            return new RecordingCall("", TwilioProblem.Explain(failure, settings.TestNumber, settings));
        }
    }

    /// <summary>The address of what the user just recorded, once they have hung up.
    /// Null while the call is still in progress.</summary>
    public async Task<string?> CollectRecordingAsync(string callSid, CancellationToken cancellation = default)
    {
        if (string.IsNullOrWhiteSpace(callSid)) return null;
        try
        {
            return await gateway.LatestRecordingUrlAsync(callSid, cancellation);
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return null;
        }
    }

    private const string RecordPrompt =
        "<Response>" +
        "<Say voice=\"Polly.Joanna\">Speak your message after the beep. Hang up when you are finished.</Say>" +
        "<Record maxLength=\"120\" playBeep=\"true\" trim=\"trim-silence\"/>" +
        "</Response>";

    private static string Play(string recordingUrl) =>
        $"<Response><Play>{SecurityElement.Escape(recordingUrl)}</Play></Response>";
}
