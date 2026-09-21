using Courier.Core.Domain;
using Courier.Messaging.Settings;

namespace Courier.Messaging.Twilio;

public sealed class TextSender(ITwilioGateway gateway, TwilioSettings settings) : IMessageSender
{
    public Channel Channel => Channel.Text;

    public bool IsConfigured => settings.IsComplete;

    public async Task<SendOutcome> SendAsync(
        string address, OutgoingMessage message, CancellationToken cancellation = default)
    {
        if (!IsConfigured)
            return SendOutcome.Failed(
                "Courier has no Twilio account to text from yet. Add your Account SID, Auth Token and Twilio number on the Setup screen.");

        if (!PhoneNumbers.IsDiallable(address))
            return SendOutcome.Failed(PhoneNumbers.Complaint(address));

        try
        {
            var sid = await gateway.SendTextAsync(
                settings.FromNumber,
                settings.SendsRichText ? settings.MessagingServiceSid.Trim() : null,
                address.Trim(),
                message.Body,
                cancellation);
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
        if (!IsConfigured)
            return CredentialCheck.Broken("Fill in your Twilio details first, then try again.");

        var destination = string.IsNullOrWhiteSpace(address) ? settings.TestNumber : address.Trim();
        if (string.IsNullOrWhiteSpace(destination))
            return CredentialCheck.Broken("Add your own mobile number on the Setup screen so Courier has somewhere to send the test.");

        var outcome = await SendAsync(
            destination,
            new OutgoingMessage("", settings.SendsRichText
                ? "This is a test from Courier. Texting works, and this went out through your messaging service, so phones that support RCS will show it as RCS. Nobody else was sent anything."
                : "This is a test from Courier. Texting works. Nobody else was sent anything."),
            cancellation);

        return outcome.Status == SendStatus.Sent
            ? CredentialCheck.Working($"Sent a test text to {destination}. It should arrive in a moment.")
            : CredentialCheck.Broken(outcome.Error ?? "Courier could not send the test text.");
    }
}

/// <summary>Numbers reach Twilio in E.164 — a plus, a country code, then digits.
/// Everything that comes out of the report has already been put in that form, so
/// anything else here is a value the report never held a number for.</summary>
internal static class PhoneNumbers
{
    public static bool IsDiallable(string? number)
    {
        var trimmed = number?.Trim();
        if (string.IsNullOrEmpty(trimmed) || trimmed[0] != '+') return false;

        var digits = trimmed[1..];
        return digits.Length is >= 8 and <= 15 && digits.All(char.IsAsciiDigit);
    }

    public static string Complaint(string? number) =>
        string.IsNullOrWhiteSpace(number)
            ? "There is no phone number for this person. Add one on their page, or reach them by e-mail."
            : $"“{number}” is not a phone number Courier can dial. Check it on this person's page — numbers from LCR that were printed without an area code need one adding.";
}
