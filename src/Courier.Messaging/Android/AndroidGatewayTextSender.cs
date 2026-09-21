using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Courier.Core.Domain;
using Courier.Messaging.Settings;
using Courier.Messaging.Twilio;

namespace Courier.Messaging.Android;

/// <summary>Sends texts by asking a gateway app on the user's own Android phone, the
/// way Phone Link asks the phone rather than sending anything itself.
///
/// Local-server mode only: Courier talks to the phone over the home network and the
/// numbers never reach anybody else's server. The same trade as the Mac route — the
/// message genuinely comes from the user's own number and replies arrive in their own
/// Messages app, but a personal line is for person-to-person texting, so this is for a
/// ward or a committee rather than all 427 people.</summary>
public sealed class AndroidGatewayTextSender(HttpClient http, AndroidGatewaySettings settings) : IMessageSender
{
    public Channel Channel => Channel.Text;

    public bool IsConfigured => settings.IsComplete;

    public async Task<SendOutcome> SendAsync(
        string address, OutgoingMessage message, CancellationToken cancellation = default)
    {
        if (!IsConfigured)
            return SendOutcome.Failed(
                "Courier does not know where your phone is yet. Open the SMS Gateway app, turn Local Server on, and copy its address and sign-in details onto the Setup screen.");

        if (!PhoneNumbers.IsDiallable(address))
            return SendOutcome.Failed(PhoneNumbers.Complaint(address));

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, Endpoint)
            {
                Content = JsonContent.Create(new
                {
                    textMessage = new { text = message.Body },
                    phoneNumbers = new[] { address.Trim() },
                }),
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", Credentials);

            using var response = await http.SendAsync(request, cancellation);
            var body = await response.Content.ReadAsStringAsync(cancellation);

            return response.IsSuccessStatusCode
                ? SendOutcome.Sent(IdFrom(body))
                : SendOutcome.Failed(Explain(response.StatusCode, body, address));
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception failure) when (failure is HttpRequestException or TimeoutException or OperationCanceledException)
        {
            return SendOutcome.Failed(
                $"Courier could not reach your phone at {settings.BaseUrl}. Check the phone is awake, on the same Wi-Fi as this computer, and that the SMS Gateway app still shows Local Server as on — its address changes when it rejoins the network.");
        }
        catch (Exception failure)
        {
            return SendOutcome.Failed($"Courier could not reach your phone. {failure.Message}");
        }
    }

    public async Task<CredentialCheck> TestAsync(string address, CancellationToken cancellation = default)
    {
        if (!IsConfigured)
            return CredentialCheck.Broken("Fill in your phone's address and sign-in details first, then try again.");

        if (string.IsNullOrWhiteSpace(address))
            return CredentialCheck.Broken("Add your own mobile number on the Setup screen so Courier has somewhere to send the test.");

        var outcome = await SendAsync(
            address,
            new OutgoingMessage("", "This is a test from Courier, sent through the gateway app on your own phone. Nobody else was sent anything."),
            cancellation);

        return outcome.Status == SendStatus.Sent
            ? CredentialCheck.Working($"Your phone accepted a test message for {address}. It should arrive in a moment.")
            : CredentialCheck.Broken(outcome.Error ?? "Your phone would not send the test message.");
    }

    private string Endpoint => $"{settings.BaseUrl.TrimEnd('/')}/message";

    private string Credentials =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes($"{settings.Username}:{settings.Password}"));

    private static string? IdFrom(string body)
    {
        try
        {
            return JsonDocument.Parse(body).RootElement.TryGetProperty("id", out var id)
                ? id.GetString()
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string Explain(HttpStatusCode status, string body, string address) => status switch
    {
        HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden =>
            "Your phone did not accept the username and password. The SMS Gateway app shows them under Local Server — it changes them if you reinstall it.",

        HttpStatusCode.NotFound =>
            "That address reached something, but not the SMS Gateway app. Check the address on the Setup screen matches what the app shows, including the port.",

        HttpStatusCode.BadRequest =>
            $"Your phone refused to send to {address}. It said: {Trim(body)}",

        _ => $"Your phone would not send to {address}. It said: {(int)status} {Trim(body)}",
    };

    private static string Trim(string body) =>
        body.Length <= 200 ? body.Trim() : body[..200].Trim() + "…";
}
