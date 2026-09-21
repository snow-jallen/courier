using System.Text.Json;
using Courier.App;
using Courier.Core.Diagnostics;

namespace Courier.Tests;

public sealed class RedactTests
{
    [Theory]
    [InlineData("a.ashgrove@example.com", "a…e@e…m")]
    [InlineData("v@x.io", "…@x…o")]
    public void An_email_is_masked_down_to_something_only_recognisable(string address, string expected)
    {
        Assert.Equal(expected, Redact.Address(address));
    }

    [Fact]
    public void A_phone_keeps_only_enough_to_tell_two_apart()
    {
        var masked = Redact.Address("+14355550142");
        Assert.Equal("…42 (11 digits)", masked);
        Assert.DoesNotContain("4355550", masked, StringComparison.Ordinal);
    }

    [Fact]
    public void A_providers_complaint_has_the_number_it_quoted_back_taken_out()
    {
        var masked = Redact.Failure("Twilio does not recognise +14355550142 as a phone number.");

        Assert.DoesNotContain("+14355550142", masked, StringComparison.Ordinal);
        Assert.Contains("does not recognise", masked, StringComparison.Ordinal);
    }

    [Fact]
    public void So_does_one_that_quotes_an_address()
    {
        var masked = Redact.Failure("Gmail would not deliver to verity@example.com, check the spelling.");
        Assert.DoesNotContain("verity@example.com", masked, StringComparison.Ordinal);
        Assert.Contains("check the spelling", masked, StringComparison.Ordinal);
    }

    [Fact]
    public void A_path_loses_the_users_home_folder_and_therefore_their_name()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        Assert.StartsWith("~", Redact.Path(Path.Combine(home, "Documents", "Courier", "contacts.db")),
            StringComparison.Ordinal);
    }

    [Fact]
    public void What_somebody_wrote_is_recorded_only_as_a_length()
    {
        Assert.Equal("(25 characters)", Redact.Text("Dinner is Friday at 6:30."));
        Assert.Equal("(none)", Redact.Text(null));
    }
}

public sealed class ActivityLogTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), $"courier-log-{Guid.NewGuid():N}");

    private List<JsonElement> Lines(JsonlActivityLog log) =>
        File.ReadAllLines(log.TodaysFile)
            .Where(l => l.Trim().Length > 0)
            .Select(l => JsonDocument.Parse(l).RootElement.Clone())
            .ToList();

    [Fact]
    public void A_session_opens_with_what_it_is_running_on()
    {
        var log = new JsonlActivityLog(_folder);

        var first = Lines(log)[0];
        Assert.Equal("session.start", first.GetProperty("action").GetString());
        Assert.NotEmpty(first.GetProperty("details").GetProperty("os").GetString()!);
        Assert.NotEmpty(first.GetProperty("session").GetString()!);
    }

    [Fact]
    public void Each_line_stands_alone_so_the_file_can_be_read_a_line_at_a_time()
    {
        var log = new JsonlActivityLog(_folder);
        log.Record("screen.open", Log.Details(("screen", "send")));
        log.Record("send.start", Log.Details(("chosen", 41), ("email", 22)));

        var lines = Lines(log);
        Assert.Equal("screen.open", lines[1].GetProperty("action").GetString());
        Assert.Equal(41, lines[2].GetProperty("details").GetProperty("chosen").GetInt32());
        Assert.All(lines, l => Assert.True(l.TryGetProperty("at", out _)));
    }

    [Fact]
    public void A_failure_keeps_the_inner_exception_which_is_the_one_that_explains_it()
    {
        var log = new JsonlActivityLog(_folder);
        var error = new InvalidOperationException("outer", new IOException("the disk is full"));

        log.Failure("import.apply", error);

        var line = Lines(log)[^1];
        var chain = line.GetProperty("error").EnumerateArray().ToList();
        Assert.Equal(2, chain.Count);
        Assert.Equal("the disk is full", chain[1].GetProperty("message").GetString());
        Assert.Equal("error", line.GetProperty("level").GetString());
    }

    [Fact]
    public void A_logged_failure_is_masked_like_everything_else()
    {
        var log = new JsonlActivityLog(_folder);
        log.Failure("send.stopped", new Exception("could not reach +14355550142"));

        Assert.DoesNotContain("4355550142", File.ReadAllText(log.TodaysFile), StringComparison.Ordinal);
    }

    [Fact]
    public void Logging_never_takes_the_app_down()
    {
        var log = new JsonlActivityLog(_folder);
        Directory.Delete(_folder, recursive: true);

        // The folder is gone underneath it; this must not throw.
        log.Record("screen.open", Log.Details(("screen", "people")));
        log.Failure("send.stopped", new Exception("boom"));
    }

    [Fact]
    public void Old_files_are_cleared_out_so_the_folder_does_not_grow_for_ever()
    {
        Directory.CreateDirectory(_folder);
        var stale = Path.Combine(_folder, "courier-2020-01-01.jsonl");
        File.WriteAllText(stale, "{}");
        File.SetLastWriteTime(stale, DateTime.Now.AddDays(-60));

        _ = new JsonlActivityLog(_folder, keepDays: 30);

        Assert.False(File.Exists(stale));
    }

    [Fact]
    public void The_recent_log_can_be_read_back_for_pasting_to_somebody()
    {
        var log = new JsonlActivityLog(_folder);
        log.Record("screen.open", Log.Details(("screen", "setup")));

        var recent = log.Recent();
        Assert.Contains("session.start", recent, StringComparison.Ordinal);
        Assert.Contains("screen.open", recent, StringComparison.Ordinal);
    }

    [Fact]
    public void Nothing_is_written_until_the_app_says_where()
    {
        // The default sink swallows everything, so a library call outside the app
        // cannot litter the user's disk.
        Log.Record("anything");
        Log.Failure("anything", new Exception("boom"));
        Assert.False(Directory.Exists(_folder));
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_folder)) Directory.Delete(_folder, recursive: true); }
        catch (IOException) { }
    }
}
