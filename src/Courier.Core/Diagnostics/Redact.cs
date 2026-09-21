namespace Courier.Core.Diagnostics;

/// <summary>Turns the things worth logging into forms that are still recognisable but
/// are not somebody's contact details.
///
/// A log is written so it can be sent to whoever is helping, which makes it a copy of
/// the directory unless it is deliberately not one. Names never go in at all. An
/// address goes in only far enough to tell one from another.</summary>
public static class Redact
{
    /// <summary>"a.ashgrove@example.com" becomes "a…e@e…m". Enough to match against a
    /// person on screen, not enough to write to them.</summary>
    public static string Address(string? value)
    {
        var raw = (value ?? "").Trim();
        if (raw.Length == 0) return "";

        var at = raw.IndexOf('@');
        if (at > 0) return $"{Ends(raw[..at])}@{Ends(raw[(at + 1)..])}";

        var digits = raw.Where(char.IsAsciiDigit).ToArray();
        return digits.Length >= 4
            ? $"…{new string(digits[^2..])} ({digits.Length} digits)"
            : Ends(raw);
    }

    /// <summary>A file path with the user's home folder replaced, since it carries their
    /// name and they did not choose to share it.</summary>
    public static string Path(string? value)
    {
        var raw = (value ?? "").Trim();
        if (raw.Length == 0) return "";

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return home.Length > 0 ? raw.Replace(home, "~", StringComparison.Ordinal) : raw;
    }

    /// <summary>What somebody wrote is theirs. The log records that there was a message
    /// and how big it was, never a word of it.</summary>
    public static string Text(string? value) =>
        value is null ? "(none)" : $"({value.Length} characters)";

    /// <summary>A provider's failure, with any address it quoted back masked — those
    /// messages routinely repeat the number that failed.</summary>
    public static string Failure(string? message)
    {
        var text = (message ?? "").Trim();
        if (text.Length == 0) return "";

        var words = text.Split(' ');
        for (var i = 0; i < words.Length; i++)
        {
            var word = words[i].Trim('"', '“', '”', '.', ',', ';', ':', '(', ')');
            var digits = word.Count(char.IsAsciiDigit);
            if (word.Contains('@', StringComparison.Ordinal) || digits >= 7)
                words[i] = words[i].Replace(word, Address(word), StringComparison.Ordinal);
        }
        return string.Join(' ', words);
    }

    private static string Ends(string part) =>
        part.Length <= 2 ? new string('…', part.Length) : $"{part[0]}…{part[^1]}";
}
