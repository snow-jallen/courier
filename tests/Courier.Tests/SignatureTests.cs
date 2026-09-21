using Courier.Core.Domain;

namespace Courier.Tests;

public sealed class SignatureTests
{
    private static readonly SenderIdentity Jonathan = new("Jonathan Allen", "Stake Singles Representative");

    [Fact]
    public void Every_message_says_who_it_is_from_and_what_their_calling_is()
    {
        var text = Signature.Compose("Dinner is Friday at 6:30.", Jonathan);
        Assert.Equal("Dinner is Friday at 6:30.\n— Jonathan Allen, Stake Singles Representative", text);
    }

    [Fact]
    public void A_sender_with_no_calling_is_still_named()
    {
        var text = Signature.Compose("Dinner is Friday.", new SenderIdentity("Jonathan Allen", ""));
        Assert.EndsWith("— Jonathan Allen", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Nothing_is_appended_when_nobody_has_said_who_they_are()
    {
        Assert.Equal("Dinner is Friday.", Signature.Compose("Dinner is Friday.", SenderIdentity.Unknown));
    }

    [Fact]
    public void Signing_off_by_hand_is_not_doubled()
    {
        var typed = "Dinner is Friday at 6:30.\nThanks, Jonathan Allen";
        Assert.Equal(typed, Signature.Compose(typed, Jonathan));
    }

    [Fact]
    public void Mentioning_yourself_in_passing_still_gets_a_signature()
    {
        var body = "Jonathan Allen is bringing the tables. Doors open at six.\nBring a side if you can.\nSee you there.";
        Assert.EndsWith("— Jonathan Allen, Stake Singles Representative",
            Signature.Compose(body, Jonathan), StringComparison.Ordinal);
    }

    [Fact]
    public void Trailing_blank_lines_do_not_push_the_signature_away()
    {
        Assert.Equal("Dinner is Friday.\n— Jonathan Allen, Stake Singles Representative",
            Signature.Compose("Dinner is Friday.\n\n\n", Jonathan));
    }

    [Theory]
    [InlineData("", 0)]
    [InlineData("short", 1)]
    [InlineData(160, 1)]
    [InlineData(161, 2)]
    [InlineData(306, 2)]
    [InlineData(307, 3)]
    public void Segments_are_counted_the_way_the_carrier_bills_them(object input, int expected)
    {
        var text = input is int length ? new string('x', length) : (string)input;
        Assert.Equal(expected, Signature.TextSegments(text));
    }
}
