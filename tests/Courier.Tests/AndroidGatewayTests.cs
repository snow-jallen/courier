using System.Net;
using System.Text;
using System.Text.Json;
using Courier.Core.Domain;
using Courier.Messaging;
using Courier.Messaging.Android;
using Courier.Messaging.Settings;

namespace Courier.Tests;

public sealed class AndroidGatewayTests
{
    /// <summary>Captures the request Courier actually puts on the wire. The phone is the
    /// one thing that cannot be tested here, so the shape of what it would receive is
    /// asserted instead — down to the field names the gateway app expects.</summary>
    private sealed class StubPhone(HttpStatusCode status = HttpStatusCode.Accepted, string body = """{"id":"abc123","state":"Pending"}""")
        : HttpMessageHandler
    {
        public HttpRequestMessage? Received { get; private set; }
        public string? ReceivedBody { get; private set; }
        public Exception? Throws { get; init; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (Throws is not null) throw Throws;
            Received = request;
            ReceivedBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(status) { Content = new StringContent(body) };
        }
    }

    private static readonly AndroidGatewaySettings Configured = new()
    {
        BaseUrl = "http://192.168.1.44:8080",
        Username = "sms",
        Password = "secret",
    };

    private static AndroidGatewayTextSender Sender(StubPhone phone, AndroidGatewaySettings? settings = null) =>
        new(new HttpClient(phone), settings ?? Configured);

    private static OutgoingMessage Message(string body = "Dinner Friday at 6:30.") => new("", body);

    [Fact]
    public async Task Asks_the_phone_to_send_in_the_shape_the_gateway_app_expects()
    {
        var phone = new StubPhone();
        var outcome = await Sender(phone).SendAsync("+14355550101", Message());

        Assert.Equal(SendStatus.Sent, outcome.Status);
        Assert.Equal("abc123", outcome.ProviderMessageId);

        Assert.Equal(HttpMethod.Post, phone.Received!.Method);
        Assert.Equal("http://192.168.1.44:8080/message", phone.Received.RequestUri!.ToString());

        var sent = JsonDocument.Parse(phone.ReceivedBody!).RootElement;
        Assert.Equal("Dinner Friday at 6:30.", sent.GetProperty("textMessage").GetProperty("text").GetString());
        Assert.Equal("+14355550101", sent.GetProperty("phoneNumbers")[0].GetString());
    }

    [Fact]
    public async Task Signs_in_to_the_phone_with_the_credentials_the_app_shows()
    {
        var phone = new StubPhone();
        await Sender(phone).SendAsync("+14355550101", Message());

        var auth = phone.Received!.Headers.Authorization!;
        Assert.Equal("Basic", auth.Scheme);
        Assert.Equal("sms:secret", Encoding.UTF8.GetString(Convert.FromBase64String(auth.Parameter!)));
    }

    [Fact]
    public async Task A_trailing_slash_on_the_address_does_not_produce_a_double_one()
    {
        var phone = new StubPhone();
        await Sender(phone, Configured with { BaseUrl = "http://192.168.1.44:8080/" })
            .SendAsync("+14355550101", Message());

        Assert.Equal("http://192.168.1.44:8080/message", phone.Received!.RequestUri!.ToString());
    }

    [Fact]
    public async Task A_phone_that_is_asleep_or_elsewhere_says_what_to_check()
    {
        var phone = new StubPhone { Throws = new HttpRequestException("connection refused") };
        var outcome = await Sender(phone).SendAsync("+14355550101", Message());

        Assert.Equal(SendStatus.Failed, outcome.Status);
        Assert.Contains("same Wi-Fi", outcome.Error!, StringComparison.Ordinal);
        Assert.Contains("its address changes", outcome.Error!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Wrong_credentials_point_at_where_the_app_shows_them()
    {
        var phone = new StubPhone(HttpStatusCode.Unauthorized, "");
        var outcome = await Sender(phone).SendAsync("+14355550101", Message());

        Assert.Contains("did not accept the username and password", outcome.Error!, StringComparison.Ordinal);
        Assert.DoesNotContain("401", outcome.Error!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_address_pointing_at_the_wrong_thing_says_so()
    {
        var phone = new StubPhone(HttpStatusCode.NotFound, "");
        var outcome = await Sender(phone).SendAsync("+14355550101", Message());

        Assert.Contains("not the SMS Gateway app", outcome.Error!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Nothing_is_sent_before_the_phone_has_been_set_up()
    {
        var phone = new StubPhone();
        var sender = Sender(phone, new AndroidGatewaySettings());

        Assert.False(sender.IsConfigured);
        var outcome = await sender.SendAsync("+14355550101", Message());

        Assert.Contains("Local Server on", outcome.Error!, StringComparison.Ordinal);
        Assert.Null(phone.Received);
    }

    [Fact]
    public async Task A_number_that_cannot_be_dialled_never_reaches_the_phone()
    {
        var phone = new StubPhone();
        var outcome = await Sender(phone).SendAsync("835-4213", Message());

        Assert.Equal(SendStatus.Failed, outcome.Status);
        Assert.Null(phone.Received);
    }

    [Fact]
    public async Task The_test_button_sends_through_the_phone_to_the_users_own_number()
    {
        var phone = new StubPhone();
        var check = await Sender(phone).TestAsync("+14355550164");

        Assert.True(check.Ok);
        var sent = JsonDocument.Parse(phone.ReceivedBody!).RootElement;
        Assert.Equal("+14355550164", sent.GetProperty("phoneNumbers")[0].GetString());
    }

    [Fact]
    public void It_is_a_text_sender_like_any_other()
    {
        Assert.Equal(Channel.Text, Sender(new StubPhone()).Channel);
    }
}
