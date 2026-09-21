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

        using var client = new SmtpClient();

        // 465 is TLS from the first byte; 587 and everything else negotiate it with
        // STARTTLS. Guessing wrong hangs rather than failing, so it is worth being explicit.
        var security = settings.Port == 465 ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls;

        await client.ConnectAsync(settings.Host, settings.Port, security, ct);

        // Google prints an app password in four groups of four and ignores the spaces
        // when you type it back; a pasted password keeps them.
        await client.AuthenticateAsync(settings.Address, Compact(settings.AppPassword), ct);

        await client.SendAsync(message, ct);
        await client.DisconnectAsync(true, ct);
    }

    private static string Compact(string password) =>
        string.Concat(password.Where(c => !char.IsWhiteSpace(c)));
}
