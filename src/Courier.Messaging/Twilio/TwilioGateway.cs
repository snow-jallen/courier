using Courier.Messaging.Settings;
using Twilio.Clients;
using Twilio.Rest.Api.V2010.Account;
using Twilio.Types;

namespace Courier.Messaging.Twilio;

/// <summary>The one place that talks to Twilio. The senders hold this interface rather
/// than the SDK so that every rule about numbers, failures and wording can be tested
/// offline, with no account and no network.</summary>
public interface ITwilioGateway
{
    /// <summary>Sends a text. When <paramref name="messagingServiceSid"/> is given the
    /// message goes through that service, which is how it reaches RCS-capable phones as
    /// RCS and everyone else as SMS, from the one request.</summary>
    Task<string> SendTextAsync(string from, string? messagingServiceSid, string to, string body, CancellationToken ct);

    /// <summary>Places a call that runs the TwiML given, and answers with its call id.
    /// Passing the TwiML inline is what lets Courier work with no web server and no
    /// public address of its own.</summary>
    Task<string> StartCallAsync(string from, string to, string twiml, CancellationToken ct);

    /// <summary>The recording left by a call, once the caller has hung up. Null while
    /// the call is still going or if nothing was recorded.</summary>
    Task<string?> LatestRecordingUrlAsync(string callSid, CancellationToken ct);
}

public sealed class TwilioGateway(TwilioSettings settings) : ITwilioGateway
{
    private ITwilioRestClient Client => new TwilioRestClient(settings.AccountSid, settings.AuthToken);

    public async Task<string> SendTextAsync(
        string from, string? messagingServiceSid, string to, string body, CancellationToken ct)
    {
        var options = new CreateMessageOptions(new PhoneNumber(to)) { Body = body };

        // A messaging service and an explicit From are mutually exclusive: the service
        // chooses the sender, and choosing it for the service disables the RCS routing
        // that is the whole point of using one.
        if (!string.IsNullOrWhiteSpace(messagingServiceSid)) options.MessagingServiceSid = messagingServiceSid;
        else options.From = new PhoneNumber(from);

        var message = await MessageResource.CreateAsync(options, Client);
        return message.Sid;
    }

    public async Task<string> StartCallAsync(string from, string to, string twiml, CancellationToken ct)
    {
        var call = await CallResource.CreateAsync(
            new CreateCallOptions(new PhoneNumber(to), new PhoneNumber(from))
            {
                Twiml = new global::Twilio.Types.Twiml(twiml),
            },
            Client);
        return call.Sid;
    }

    public async Task<string?> LatestRecordingUrlAsync(string callSid, CancellationToken ct)
    {
        var recordings = await RecordingResource.ReadAsync(
            new ReadRecordingOptions { CallSid = callSid, Limit = 1 }, Client);

        var recording = recordings.FirstOrDefault();
        if (recording is null) return null;

        // Twilio serves the recording at this address with .mp3 appended, and <Play>
        // fetches it from the same account that made it.
        return $"https://api.twilio.com{recording.Uri?.Replace(".json", ".mp3", StringComparison.Ordinal)}";
    }
}
