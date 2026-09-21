using System.Net.Sockets;
using Courier.Messaging.Settings;
using Twilio.Exceptions;

namespace Courier.Messaging.Twilio;

/// <summary>Turns Twilio's failures into the sentence the user sees. Twilio answers
/// with numbers — 21211, 20003 — which tell the people this app is for exactly
/// nothing. Every one of these says what happened and what they can do about it.</summary>
internal static class TwilioProblem
{
    public static string Explain(Exception failure, string address, TwilioSettings settings) => failure switch
    {
        ApiException api => ExplainApi(api, address, settings),

        SocketException or HttpRequestException or TimeoutException =>
            "Courier could not reach Twilio. Check that this computer is connected to the internet, then try again.",

        _ => $"Courier could not reach {address}, and does not recognise the reason. Twilio said: {failure.Message}",
    };

    private static string ExplainApi(ApiException api, string address, TwilioSettings settings) => api.Code switch
    {
        20003 =>
            "Twilio would not accept your account details. Check the Account SID and Auth Token on the Setup screen against the ones on your Twilio dashboard — the token is easy to copy short.",

        21211 or 21214 =>
            $"Twilio does not recognise {address} as a phone number. Check it on this person's page, or reach them another way.",

        21408 or 21215 =>
            $"Your Twilio account is not allowed to reach {address}. If that number is outside the United States, turn its region on in the Twilio console under Geo Permissions.",

        21606 or 21605 =>
            $"Twilio will not send from {settings.FromNumber}. Make sure the number on the Setup screen is one you bought in Twilio and that it can send messages.",

        21608 =>
            $"Your Twilio account is still a trial, which can only reach numbers you have verified. Either verify {address} in the Twilio console, or upgrade the account.",

        21610 =>
            $"{address} has replied STOP to your Twilio number, so Twilio will not deliver to them. They have to text START to that number before Courier can reach them again.",

        21612 or 30003 or 30005 =>
            $"{address} could not be reached — the number may be switched off, disconnected, or unable to receive messages. Try phoning instead.",

        21614 =>
            $"{address} is a landline, so it cannot receive a text. Set this person to a phone call instead.",

        20429 =>
            "Twilio is asking Courier to slow down. Everything sent so far has gone out; wait a minute and send the rest.",

        20404 =>
            "Twilio could not find that recording any more. Record your message again on the Send screen.",

        _ when api.Message.Contains("balance", StringComparison.OrdinalIgnoreCase) =>
            "Your Twilio account is out of credit. Add funds in the Twilio console, then send again — nothing was charged for this attempt.",

        _ => $"Twilio refused to reach {address}. It said: {api.Message}",
    };
}
