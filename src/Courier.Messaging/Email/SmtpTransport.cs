using Courier.Messaging.Settings;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using MimeKit.Text;

namespace Courier.Messaging.Email;

/// <summary>The one place that talks SMTP. <see cref="EmailSender"/> holds this
/// interface rather than MailKit so that every rule about addresses, failures and
/// wording can be tested offline, with no account and no network.</summary>
public interface ISmtpTransport
{
    Task SendAsync(EmailSettings settings, string to, string subject, string body, CancellationToken ct);
}

/// <summary>Sends one message over SMTP and lets its failures out unchanged;
/// <see cref="EmailSender"/> is what turns them into something a user can read.</summary>
public sealed class SmtpTransport : ISmtpTransport
{
    public async Task SendAsync(
        EmailSettings settings, string to, string subject, string body, CancellationToken ct)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(
            settings.DisplayName.Length > 0 ? settings.DisplayName : settings.Address, settings.Address));
        message.To.Add(MailboxAddress.Parse(to));
        message.Subject = subject;
        message.Body = new TextPart(TextFormat.Plain) { Text = body };

        using var client = new SmtpClient
        {
            // MailKit checks certificate revocation by default, which means an OCSP or
            // CRL lookup during the handshake. Where that lookup cannot complete — a
            // network that blocks it, a captive portal, some antivirus — the certificate
            // is rejected and the connection fails with "could not make a secure
            // connection", which sounds like a broken server and is not. The certificate
            // is still verified; only the revocation lookup is skipped.
            CheckCertificateRevocation = false,
            Timeout = 30_000,
        };

        // 465 is TLS from the first byte; 587 and everything else negotiate it with
        // STARTTLS. Guessing wrong hangs rather than failing, so it is worth being explicit.
        var security = settings.Port == 465 ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls;

        try
        {
            await client.ConnectAsync(settings.Host, settings.Port, security, ct);
        }
        catch (Exception first) when (first is SslHandshakeException or System.Net.Sockets.SocketException
                                      && settings.Port != 465)
        {
            // Guest and institution networks commonly allow 465 and block 587. One
            // retry on the other port turns an evening of confusion into a pause.
            try
            {
                await client.ConnectAsync(settings.Host, 465, SecureSocketOptions.SslOnConnect, ct);
            }
            catch
            {
                throw first;
            }
        }

        // Google prints an app password in four groups of four and ignores the spaces
        // when you type it back; a pasted password keeps them.
        await client.AuthenticateAsync(settings.Address, Compact(settings.AppPassword), ct);

        await client.SendAsync(message, ct);
        await client.DisconnectAsync(true, ct);
    }

    private static string Compact(string password) =>
        string.Concat(password.Where(c => !char.IsWhiteSpace(c)));
}
