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

    public bool IsConfigured => settings.IsComplete;

    public async Task<SendOutcome> SendAsync(
        string address, OutgoingMessage message, CancellationToken cancellation = default)
    {
        if (!settings.IsComplete)
            return SendOutcome.Failed(
                "Courier has no Twilio account to call from yet. Add your Account SID, Auth Token and Twilio number on the Setup screen.");

        var twiml = TwimlFor(message);
        if (twiml is null)
            return SendOutcome.Failed(
                "There is nothing for this call to play. On the Send screen either record yourself reading the message, or switch on reading it aloud.");

        if (!PhoneNumbers.IsDiallable(address))
            return SendOutcome.Failed(PhoneNumbers.Complaint(address));

        try
        {
            var sid = await gateway.StartCallAsync(settings.FromNumber, address.Trim(), twiml, cancellation);
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

        var outcome = await SendAsync(
            destination,
            new OutgoingMessage("", "This is a test from Courier. Calling works. Nobody else was called.", SpeakAloud: true),
            cancellation);

        return outcome.Status == SendStatus.Sent
            ? CredentialCheck.Working($"Calling {destination} now. Answer it to hear the test.")
            : CredentialCheck.Broken(outcome.Error ?? "Courier could not place the test call.");
    }

    /// <summary>Rings the user and plays exactly what everyone else would hear, so the
    /// call can be checked before it goes to 300 people.</summary>
    public Task<SendOutcome> PreviewAsync(
        string address, OutgoingMessage message, CancellationToken cancellation = default) =>
        SendAsync(address, message, cancellation);

    /// <summary>A recording if one was made, otherwise the message read aloud, otherwise
    /// nothing — a call that connects to silence is worse than one that never goes out.</summary>
    private static string? TwimlFor(OutgoingMessage message)
    {
        if (message.VoiceRecordingUrl is { Length: > 0 } recording) return Play(recording);
        if (message.SpeakAloud && message.Body.Trim().Length > 0) return Speak(message.Body);
        return null;
    }

    private static string Speak(string body) =>
        $"<Response><Say voice=\"Polly.Joanna\">{SecurityElement.Escape(body)}</Say></Response>";

    /// <summary>Rings the user so they can read this message aloud. They hang up when
    /// done; the recording is then fetched with <see cref="CollectRecordingAsync"/>.
    ///
    /// The script is read to them first, because nobody can improvise the wording of an
    /// announcement they wrote ten minutes ago and have not looked at since.</summary>
    public async Task<RecordingCall?> StartRecordingAsync(
        string script = "", CancellationToken cancellation = default)
    {
        if (!settings.IsComplete || string.IsNullOrWhiteSpace(settings.TestNumber)) return null;

        try
        {
            var sid = await gateway.StartCallAsync(
                settings.FromNumber, settings.TestNumber, RecordPrompt(script), cancellation);
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

    private static string RecordPrompt(string script)
    {
        var read = script.Trim().Length > 0
            ? $"<Say voice=\"Polly.Joanna\">Your message reads: {SecurityElement.Escape(script)}</Say>"
            : "";

        return "<Response>"
             + read
             + "<Say voice=\"Polly.Joanna\">Read your message after the beep. Hang up when you are finished.</Say>"
             + "<Record maxLength=\"180\" playBeep=\"true\" trim=\"trim-silence\"/>"
             + "</Response>";
    }

    private static string Play(string recordingUrl) =>
        $"<Response><Play>{SecurityElement.Escape(recordingUrl)}</Play></Response>";
}
