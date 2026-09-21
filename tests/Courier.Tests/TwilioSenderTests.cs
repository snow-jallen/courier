using Courier.Core.Domain;
using Courier.Messaging;
using Courier.Messaging.Settings;
using Courier.Messaging.Twilio;
using Twilio.Exceptions;

namespace Courier.Tests;

public sealed class TwilioSenderTests
{
    private sealed class FakeTwilio(Exception? throws = null) : ITwilioGateway
    {
        public List<(string From, string? Service, string To, string Body)> Texts { get; } = [];
        public List<(string From, string To, string Twiml)> Calls { get; } = [];
        public string? RecordingUrl { get; set; }

        public Task<string> SendTextAsync(
            string from, string? messagingServiceSid, string to, string body, CancellationToken ct)
        {
            if (throws is not null) return Task.FromException<string>(throws);
            Texts.Add((from, messagingServiceSid, to, body));
            return Task.FromResult("SM0123456789abcdef");
        }

        public Task<string> StartCallAsync(string from, string to, string twiml, CancellationToken ct)
        {
            if (throws is not null) return Task.FromException<string>(throws);
            Calls.Add((from, to, twiml));
            return Task.FromResult("CA0123456789abcdef");
        }

        public Task<string?> LatestRecordingUrlAsync(string callSid, CancellationToken ct) =>
            Task.FromResult(RecordingUrl);
    }

    private static readonly TwilioSettings Configured = new()
    {
        AccountSid = "AC0123456789abcdef0123456789abcdef",
        AuthToken = "a-token",
        FromNumber = "+14355550188",
        TestNumber = "+14355550164",
    };

    private static TwilioSettings WithRecording => Configured with
    {
        VoiceRecordingUrl = "https://api.twilio.com/2010-04-01/Accounts/AC0/Recordings/RE0.mp3",
    };

    private static TwilioSettings WithRichText => Configured with
    {
        MessagingServiceSid = "MG0123456789abcdef0123456789abcdef",
    };

    private static OutgoingMessage Message => new("Subject ignored", "Dinner Friday at 6:30.");

    private static ApiException Api(int code, string message = "Twilio said no") =>
        new(code, 400, message, moreInfo: "", details: null, exception: null);

    // ---- texting ---------------------------------------------------------------

    [Fact]
    public async Task Sends_a_text_from_the_twilio_number()
    {
        var twilio = new FakeTwilio();
        var outcome = await new TextSender(twilio, Configured).SendAsync("+14355550101", Message);

        Assert.Equal(SendStatus.Sent, outcome.Status);
        Assert.Equal("SM0123456789abcdef", outcome.ProviderMessageId);
        var sent = Assert.Single(twilio.Texts);
        Assert.Equal("+14355550188", sent.From);
        Assert.Null(sent.Service);
        Assert.Equal("+14355550101", sent.To);
        Assert.Equal("Dinner Friday at 6:30.", sent.Body);
    }

    [Fact]
    public async Task A_messaging_service_carries_the_text_so_it_can_arrive_as_rcs()
    {
        var twilio = new FakeTwilio();
        Assert.True(WithRichText.SendsRichText);

        await new TextSender(twilio, WithRichText).SendAsync("+14355550101", Message);

        // Twilio routes through the service, delivering RCS where the phone supports it
        // and SMS everywhere else, from the one request.
        var sent = Assert.Single(twilio.Texts);
        Assert.Equal("MG0123456789abcdef0123456789abcdef", sent.Service);
    }

    [Fact]
    public async Task Without_a_messaging_service_texts_still_go_out_as_plain_sms()
    {
        var twilio = new FakeTwilio();
        Assert.False(Configured.SendsRichText);

        await new TextSender(twilio, Configured).SendAsync("+14355550101", Message);

        Assert.Null(Assert.Single(twilio.Texts).Service);
    }

    [Fact]
    public void A_messaging_service_is_enough_to_send_with_even_without_a_number()
    {
        var serviceOnly = new TwilioSettings
        {
            AccountSid = "AC0", AuthToken = "t", MessagingServiceSid = "MG0", TestNumber = "+14355550164",
        };
        Assert.True(new TextSender(new FakeTwilio(), serviceOnly).IsConfigured);
    }

    [Fact]
    public async Task Says_what_to_do_when_twilio_is_not_set_up()
    {
        var sender = new TextSender(new FakeTwilio(), new TwilioSettings());
        Assert.False(sender.IsConfigured);

        var outcome = await sender.SendAsync("+14355550101", Message);
        Assert.Contains("Setup screen", outcome.Error!, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("")]
    [InlineData("555-0144")]
    [InlineData("(435) 555-0101")]
    [InlineData("call the house")]
    public async Task Refuses_a_number_that_is_not_ready_to_dial(string notE164)
    {
        var twilio = new FakeTwilio();
        var outcome = await new TextSender(twilio, Configured).SendAsync(notE164, Message);

        Assert.Equal(SendStatus.Failed, outcome.Status);
        Assert.Empty(twilio.Texts);
    }

    [Fact]
    public async Task A_missing_number_asks_for_one_rather_than_blaming_the_user()
    {
        var outcome = await new TextSender(new FakeTwilio(), Configured).SendAsync("", Message);
        Assert.Contains("no phone number for this person", outcome.Error!, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(20003, "Account SID")]
    [InlineData(21211, "does not recognise")]
    [InlineData(21608, "trial")]
    [InlineData(21610, "STOP")]
    [InlineData(21614, "landline")]
    [InlineData(20429, "slow down")]
    public async Task Each_twilio_failure_becomes_something_a_person_can_act_on(int code, string expected)
    {
        var sender = new TextSender(new FakeTwilio(Api(code)), Configured);
        var outcome = await sender.SendAsync("+14355550101", Message);

        Assert.Equal(SendStatus.Failed, outcome.Status);
        Assert.Contains(expected, outcome.Error!, StringComparison.OrdinalIgnoreCase);

        // The number itself is what the user must never be handed.
        Assert.DoesNotContain(code.ToString(), outcome.Error!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Running_out_of_credit_says_so_in_those_words()
    {
        var sender = new TextSender(new FakeTwilio(Api(999, "Account has insufficient balance")), Configured);
        var outcome = await sender.SendAsync("+14355550101", Message);
        Assert.Contains("out of credit", outcome.Error!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task No_connection_reads_as_no_connection()
    {
        var sender = new TextSender(new FakeTwilio(new HttpRequestException("no route")), Configured);
        var outcome = await sender.SendAsync("+14355550101", Message);
        Assert.Contains("internet", outcome.Error!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task The_test_button_texts_the_users_own_number()
    {
        var twilio = new FakeTwilio();
        var check = await new TextSender(twilio, Configured).TestAsync("");

        Assert.True(check.Ok);
        Assert.Equal("+14355550164", Assert.Single(twilio.Texts).To);
    }

    // ---- calling ---------------------------------------------------------------

    [Fact]
    public async Task A_call_plays_the_recording_and_nothing_else()
    {
        var twilio = new FakeTwilio();
        var outcome = await new VoiceSender(twilio, WithRecording).SendAsync("+14355550101", Message);

        Assert.Equal(SendStatus.Sent, outcome.Status);
        var call = Assert.Single(twilio.Calls);
        Assert.Contains("<Play>", call.Twiml, StringComparison.Ordinal);
        Assert.Contains(WithRecording.VoiceRecordingUrl, call.Twiml, StringComparison.Ordinal);

        // Reading the message aloud was explicitly not wanted.
        Assert.DoesNotContain("<Say>", call.Twiml, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Calling_before_anything_is_recorded_explains_how_to_record()
    {
        var twilio = new FakeTwilio();
        var sender = new VoiceSender(twilio, Configured);

        Assert.False(sender.IsConfigured);
        Assert.False(sender.HasRecording);

        var outcome = await sender.SendAsync("+14355550101", Message);

        Assert.Equal(SendStatus.Failed, outcome.Status);
        Assert.Contains("Record my message", outcome.Error!, StringComparison.Ordinal);
        Assert.Empty(twilio.Calls);
    }

    [Fact]
    public async Task Recording_rings_the_user_and_asks_them_to_speak()
    {
        var twilio = new FakeTwilio();
        var session = await new VoiceSender(twilio, Configured).StartRecordingAsync();

        Assert.NotNull(session);
        Assert.Equal("CA0123456789abcdef", session.CallSid);
        var call = Assert.Single(twilio.Calls);
        Assert.Equal("+14355550164", call.To);
        Assert.Contains("<Record", call.Twiml, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_recording_is_collected_once_the_user_hangs_up()
    {
        var twilio = new FakeTwilio { RecordingUrl = "https://api.twilio.com/RE1.mp3" };
        var sender = new VoiceSender(twilio, Configured);

        Assert.Equal("https://api.twilio.com/RE1.mp3", await sender.CollectRecordingAsync("CA1"));
    }

    [Fact]
    public async Task Nothing_comes_back_while_the_call_is_still_going()
    {
        var sender = new VoiceSender(new FakeTwilio(), Configured);
        Assert.Null(await sender.CollectRecordingAsync("CA1"));
    }

    [Fact]
    public void Each_sender_reports_the_channel_it_serves()
    {
        Assert.Equal(Channel.Text, new TextSender(new FakeTwilio(), Configured).Channel);
        Assert.Equal(Channel.Voice, new VoiceSender(new FakeTwilio(), Configured).Channel);
    }
}
