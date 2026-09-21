using System.Net.Sockets;
using Courier.Core.Domain;
using Courier.Messaging.Settings;
using MailKit;
using MailKit.Net.Smtp;
using MailKit.Security;

namespace Courier.Messaging.Email;

/// <summary>Sends to one person at a time, on purpose. Each message carries a single
/// address in To, so sending to the whole directory cannot show 427 people each
/// other's addresses however the batch above is written, and every person gets their
/// own result to record and their own failure to retry.</summary>
public sealed class EmailSender(ISmtpTransport transport, EmailSettings settings) : IMessageSender
{
    public Channel Channel => Channel.Email;

    public bool IsConfigured => settings.IsComplete;

    public async Task<SendOutcome> SendAsync(
        string address, OutgoingMessage message, CancellationToken cancellation = default)
    {
        if (!IsConfigured)
            return SendOutcome.Failed(
                "Courier has no e-mail account to send from yet. Add your Gmail address and app password on the Setup screen.");

        if (!LooksLikeAnEmailAddress(address))
            return SendOutcome.Failed(
                $"“{address}” is not an e-mail address Courier can send to. Check it for a typo on this person's page, or send to them another way.");

        try
        {
            await transport.SendAsync(settings, address.Trim(), message.Subject, message.Body, cancellation);
            return SendOutcome.Sent(null);
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            // The user stopped the send. That is not a delivery failure and must not be
            // recorded as one.
            throw;
        }
        catch (Exception failure)
        {
            return SendOutcome.Failed(Explain(failure, address));
        }
    }

    public async Task<CredentialCheck> TestAsync(string address, CancellationToken cancellation = default)
    {
        if (!IsConfigured)
            return CredentialCheck.Broken("Fill in your e-mail address and app password first, then try again.");

        // The Test button proves the account by using it: a real message to the user's
        // own address, so "Working" cannot mean anything but "this actually sends".
        var destination = string.IsNullOrWhiteSpace(address) ? settings.Address : address.Trim();

        var outcome = await SendAsync(
            destination,
            new OutgoingMessage(
                "Courier test message",
                "Courier can send e-mail from this account. Nobody else was sent anything."),
            cancellation);

        return outcome.Status == SendStatus.Sent
            ? CredentialCheck.Working($"Sent a test message to {destination}. It should arrive in a moment.")
            : CredentialCheck.Broken(outcome.Error ?? "Courier could not send the test message.");
    }

    /// <summary>Turns a mail library's failure into the sentence the user sees. Nothing
    /// here may reach the screen as a status code: "535 5.7.8" tells the people this app
    /// is for exactly nothing, and the answer they need — an app password — is one they
    /// can act on.</summary>
    private string Explain(Exception failure, string address) => failure switch
    {
        AuthenticationException or ServiceNotAuthenticatedException => PasswordAdvice,

        SmtpCommandException smtp => ExplainSmtp(smtp, address),

        SslHandshakeException =>
            $"Courier could not make a secure connection to {settings.Host}. If this computer is on a guest or church network, that network may be blocking mail; try again from another one.",

        SocketException or IOException or SmtpProtocolException or TimeoutException or OperationCanceledException =>
            $"Courier could not reach {settings.Host}. Check that this computer is connected to the internet, then try again.",

        _ =>
            $"Courier could not send to {address}, and does not recognise the reason. The mail server said: {failure.Message}",
    };

    private string ExplainSmtp(SmtpCommandException failure, string address)
    {
        var status = (int)failure.StatusCode;

        // 530, 534 and 535 are all "sign in again" in one wording or another, and on
        // Gmail they nearly always mean the account password was used instead of an app one.
        if (status is 530 or 534 or 535)
            return PasswordAdvice;

        if (status is 421 or 450 or 452 || IsTooMuchMail(failure.Message))
            return TooMuchMailAdvice;

        return failure.ErrorCode switch
        {
            SmtpErrorCode.RecipientNotAccepted =>
                $"{settings.Host} would not deliver to {address}. The address is probably wrong or no longer exists — check its spelling on this person's page, or send to them another way.",

            SmtpErrorCode.SenderNotAccepted =>
                $"{settings.Host} would not accept {settings.Address} as the sender. Make sure the address on the Setup screen is the one you actually sign in with.",

            _ =>
                $"{settings.Host} refused the message to {address}. It said: {failure.Message}",
        };
    }

    private bool IsGmail => settings.Host.Contains("gmail", StringComparison.OrdinalIgnoreCase);

    private string PasswordAdvice => IsGmail
        ? $"Google would not accept that password for {settings.Address}. Gmail needs a 16-character app password, not the password you sign in with. In your Google Account open Security, turn on 2-Step Verification if it is off, then choose App passwords, make one for Courier, and paste it into Setup."
        : $"{settings.Host} would not accept the address and password for {settings.Address}. Check both on the Setup screen — many mail providers need a separate app password rather than your sign-in one.";

    private string TooMuchMailAdvice => IsGmail
        ? "Gmail has stopped accepting messages from this account for today. A Gmail account can send to about 500 people a day. Everything sent so far has gone out; send the rest tomorrow."
        : $"{settings.Host} has stopped accepting messages from this account for now, because too many have been sent at once. Everything sent so far has gone out; send the rest later.";

    private static bool IsTooMuchMail(string serverSaid) =>
        serverSaid.Contains("5.4.5", StringComparison.Ordinal)
        || serverSaid.Contains("4.7.0", StringComparison.Ordinal)
        || serverSaid.Contains("sending limit", StringComparison.OrdinalIgnoreCase)
        || serverSaid.Contains("sending quota", StringComparison.OrdinalIgnoreCase)
        || serverSaid.Contains("rate limit", StringComparison.OrdinalIgnoreCase)
        || serverSaid.Contains("too many", StringComparison.OrdinalIgnoreCase);

    /// <summary>Deliberately loose. This is here to catch the blank cell, the phone
    /// number and the "ask her sister" that the directory holds in an e-mail column,
    /// before any of them turn into a send and a failure to explain — not to predict
    /// what a mail server will accept.</summary>
    private static bool LooksLikeAnEmailAddress(string? address)
    {
        if (string.IsNullOrWhiteSpace(address)) return false;

        var trimmed = address.Trim();
        if (trimmed.Any(char.IsWhiteSpace)) return false;

        var at = trimmed.IndexOf('@');
        if (at <= 0 || at != trimmed.LastIndexOf('@')) return false;

        var domain = trimmed[(at + 1)..];
        var dot = domain.IndexOf('.');
        return dot > 0 && dot < domain.Length - 1;
    }
}
