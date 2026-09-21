using Courier.Core.Domain;
using Courier.Messaging;
using Courier.Messaging.Email;
using Courier.Messaging.Settings;
using MailKit.Net.Smtp;
using MailKit.Security;

namespace Courier.Tests;

public sealed class EmailSenderTests
{
    private sealed class FakeSmtp(Exception? throws = null) : ISmtpTransport
    {
        public List<(string To, string Subject, string Body)> Sent { get; } = [];

        public Task SendAsync(EmailSettings settings, string to, string subject, string body, CancellationToken ct)
        {
            if (throws is not null) return Task.FromException(throws);
            Sent.Add((to, subject, body));
            return Task.CompletedTask;
        }
    }

    private static readonly EmailSettings Configured = new()
    {
        Address = "manti.singles@gmail.com",
        AppPassword = "abcd efgh ijkl mnop",
    };

    private static OutgoingMessage Message =>
        new("Stake singles dinner", "Friday at 6:30 at the stake center.");

    private static EmailSender Sender(ISmtpTransport transport, EmailSettings? settings = null) =>
        new(transport, settings ?? Configured);

    [Fact]
    public async Task Sends_the_message_to_the_address_given()
    {
        var smtp = new FakeSmtp();
        var outcome = await Sender(smtp).SendAsync("verity@example.com", Message);

        Assert.Equal(SendStatus.Sent, outcome.Status);
        var sent = Assert.Single(smtp.Sent);
        Assert.Equal("verity@example.com", sent.To);
        Assert.Equal("Stake singles dinner", sent.Subject);
    }

    [Fact]
    public async Task Says_what_to_do_when_no_account_has_been_set_up()
    {
        var sender = Sender(new FakeSmtp(), new EmailSettings());
        Assert.False(sender.IsConfigured);

        var outcome = await sender.SendAsync("verity@example.com", Message);

        Assert.Equal(SendStatus.Failed, outcome.Status);
        Assert.Contains("Setup", outcome.Error!, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not an address")]
    [InlineData("ask her sister")]
    [InlineData("two@at@example.com")]
    [InlineData("nodomain@")]
    [InlineData("@example.com")]
    public async Task Refuses_something_that_is_not_an_email_address(string rubbish)
    {
        var smtp = new FakeSmtp();
        var outcome = await Sender(smtp).SendAsync(rubbish, Message);

        Assert.Equal(SendStatus.Failed, outcome.Status);
        Assert.Empty(smtp.Sent);
    }

    [Fact]
    public async Task An_account_password_where_an_app_password_belongs_says_exactly_that()
    {
        var smtp = new FakeSmtp(new AuthenticationException("535: 5.7.8 Username and Password not accepted"));
        var outcome = await Sender(smtp).SendAsync("verity@example.com", Message);

        Assert.Equal(SendStatus.Failed, outcome.Status);
        Assert.Contains("app password", outcome.Error!, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("2-Step Verification", outcome.Error!, StringComparison.Ordinal);

        // The status code is exactly what the user must never be handed.
        Assert.DoesNotContain("5.7.8", outcome.Error!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task No_connection_reads_as_no_connection()
    {
        var smtp = new FakeSmtp(new System.Net.Sockets.SocketException(60));
        var outcome = await Sender(smtp).SendAsync("verity@example.com", Message);

        Assert.Equal(SendStatus.Failed, outcome.Status);
        Assert.Contains("internet", outcome.Error!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Hitting_the_daily_cap_says_what_already_went_out()
    {
        var smtp = new FakeSmtp(new SmtpCommandException(
            SmtpErrorCode.MessageNotAccepted, (SmtpStatusCode)452, "4.7.0 too many messages"));

        var outcome = await Sender(smtp).SendAsync("verity@example.com", Message);

        Assert.Contains("Everything sent so far has gone out", outcome.Error!, StringComparison.Ordinal);
        Assert.Contains("500", outcome.Error!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_rejected_recipient_points_at_the_address_not_the_account()
    {
        var smtp = new FakeSmtp(new SmtpCommandException(
            SmtpErrorCode.RecipientNotAccepted, (SmtpStatusCode)550, "No such user"));

        var outcome = await Sender(smtp).SendAsync("wrong@example.com", Message);

        Assert.Contains("wrong@example.com", outcome.Error!, StringComparison.Ordinal);
        Assert.Contains("spelling", outcome.Error!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task The_test_button_sends_a_real_message_to_the_users_own_address()
    {
        var smtp = new FakeSmtp();
        var check = await Sender(smtp).TestAsync("");

        Assert.True(check.Ok);
        Assert.Equal("manti.singles@gmail.com", Assert.Single(smtp.Sent).To);
        Assert.Contains("manti.singles@gmail.com", check.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_test_button_reports_the_same_plain_language_when_it_fails()
    {
        var smtp = new FakeSmtp(new AuthenticationException("535"));
        var check = await Sender(smtp).TestAsync("");

        Assert.False(check.Ok);
        Assert.Contains("app password", check.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Stopping_a_send_is_not_recorded_as_a_delivery_failure()
    {
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        var smtp = new FakeSmtp(new OperationCanceledException());

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => Sender(smtp).SendAsync("verity@example.com", Message, cancelled.Token));
    }

    [Fact]
    public void Email_is_the_channel_it_reports()
    {
        Assert.Equal(Channel.Email, Sender(new FakeSmtp()).Channel);
    }
}
