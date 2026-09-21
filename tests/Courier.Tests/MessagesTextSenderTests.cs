using Courier.Core.Domain;
using Courier.Messaging;
using Courier.Messaging.Mac;

namespace Courier.Tests;

public sealed class MessagesTextSenderTests
{
    private sealed class FakeOsascript(int exitCode = 0, string error = "", bool available = true)
        : IAppleScriptRunner
    {
        public List<(string Script, IReadOnlyList<string> Arguments)> Runs { get; } = [];
        public bool IsAvailable => available;

        public Task<AppleScriptResult> RunAsync(string script, IReadOnlyList<string> arguments, CancellationToken ct)
        {
            Runs.Add((script, arguments));
            return Task.FromResult(new AppleScriptResult(exitCode, "", error));
        }
    }

    private static MessagesTextSender Sender(IAppleScriptRunner runner) =>
        new(runner, pause: TimeSpan.Zero);

    private static OutgoingMessage Message(string body = "Dinner Friday at 6:30.") => new("", body);

    [Fact]
    public async Task Hands_the_message_to_the_messages_app()
    {
        var osascript = new FakeOsascript();
        var outcome = await Sender(osascript).SendAsync("+14355550101", Message());

        Assert.Equal(SendStatus.Sent, outcome.Status);
        var run = Assert.Single(osascript.Runs);
        Assert.Equal(["+14355550101", "Dinner Friday at 6:30."], run.Arguments);
    }

    [Theory]
    [InlineData("Relief Society's dinner is \"on\" Friday")]
    [InlineData("Line one\nLine two \\ backslash")]
    [InlineData("Ends with a quote \"")]
    public async Task Punctuation_that_would_break_a_script_is_passed_as_an_argument_instead(string body)
    {
        var osascript = new FakeOsascript();
        await Sender(osascript).SendAsync("+14355550101", Message(body));

        // The text never enters the AppleScript source, so there is nothing to escape
        // and no way for an apostrophe to end a string and change the program.
        var run = Assert.Single(osascript.Runs);
        Assert.Equal(body, run.Arguments[1]);
        Assert.DoesNotContain(body, run.Script, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Off_a_mac_it_says_so_and_points_at_the_alternative()
    {
        var outcome = await Sender(new FakeOsascript(available: false)).SendAsync("+14355550101", Message());

        Assert.Equal(SendStatus.Failed, outcome.Status);
        Assert.Contains("only works on a Mac", outcome.Error!, StringComparison.Ordinal);
        Assert.Contains("Twilio", outcome.Error!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_number_that_cannot_be_dialled_never_reaches_messages()
    {
        var osascript = new FakeOsascript();
        var outcome = await Sender(osascript).SendAsync("835-4213", Message());

        Assert.Equal(SendStatus.Failed, outcome.Status);
        Assert.Empty(osascript.Runs);
    }

    [Fact]
    public async Task The_first_failure_most_people_hit_explains_the_permission()
    {
        var osascript = new FakeOsascript(1, "execution error: Not authorized to send Apple events to Messages. (-1743)");
        var outcome = await Sender(osascript).SendAsync("+14355550101", Message());

        Assert.Contains("Privacy & Security", outcome.Error!, StringComparison.Ordinal);
        Assert.Contains("Automation", outcome.Error!, StringComparison.Ordinal);
        Assert.DoesNotContain("-1743", outcome.Error!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Messages_being_closed_is_reported_as_messages_being_closed()
    {
        var osascript = new FakeOsascript(1, "Application isn't running. (-600)");
        var outcome = await Sender(osascript).SendAsync("+14355550101", Message());

        Assert.Contains("Messages app is not running", outcome.Error!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_number_messages_will_not_take_points_at_text_message_forwarding()
    {
        var osascript = new FakeOsascript(1, "Invalid handle");
        var outcome = await Sender(osascript).SendAsync("+14355550101", Message());

        Assert.Contains("Text Message Forwarding", outcome.Error!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_test_button_sends_through_messages_to_the_users_own_number()
    {
        var osascript = new FakeOsascript();
        var check = await Sender(osascript).TestAsync("+14355550164");

        Assert.True(check.Ok);
        Assert.Equal("+14355550164", Assert.Single(osascript.Runs).Arguments[0]);
    }

    [Fact]
    public void It_is_a_text_sender_like_any_other()
    {
        var sender = Sender(new FakeOsascript());
        Assert.Equal(Channel.Text, sender.Channel);
        Assert.True(sender.IsConfigured);
        Assert.False(Sender(new FakeOsascript(available: false)).IsConfigured);
    }
}
