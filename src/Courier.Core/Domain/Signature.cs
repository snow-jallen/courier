namespace Courier.Core.Domain;

/// <summary>Who the message is from.
///
/// Courier is one person's tool, not a stake's system: messages go out over an
/// individual's calling and must say so. A text arriving from an unfamiliar number is
/// ignored or reported; the same text signed "Jonathan Allen, Stake Singles
/// Representative" is answered.</summary>
public sealed record SenderIdentity(string Name, string Calling)
{
    public static readonly SenderIdentity Unknown = new("", "");

    public bool IsComplete => Name.Trim().Length > 0;

    /// <summary>"Jonathan Allen, Stake Singles Representative", or just the name when
    /// no calling has been filled in.</summary>
    public string Line =>
        Calling.Trim().Length > 0 && Name.Trim().Length > 0
            ? $"{Name.Trim()}, {Calling.Trim()}"
            : Name.Trim();
}

public static class Signature
{
    /// <summary>The body as the recipient will read it, signed.
    ///
    /// Composed once, in one place, so the character count on screen, the preview and
    /// what actually leaves are the same string. A signature already present — because
    /// the sender typed it themselves — is left alone rather than doubled.</summary>
    public static string Compose(string body, SenderIdentity sender)
    {
        var text = (body ?? "").TrimEnd();
        if (!sender.IsComplete) return text;

        var line = sender.Line;
        if (text.Length == 0) return $"— {line}";

        return AlreadySigned(text, sender) ? text : $"{text}\n— {line}";
    }

    /// <summary>True when the sender has written their own name at the end. Checks the
    /// last couple of lines rather than the whole message, so mentioning yourself in
    /// passing does not suppress the signature.</summary>
    private static bool AlreadySigned(string text, SenderIdentity sender)
    {
        var name = sender.Name.Trim();
        if (name.Length == 0) return false;

        var lines = text.Split('\n');
        var tail = string.Join(' ', lines.Skip(Math.Max(0, lines.Length - 2)));
        return tail.Contains(name, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>How many 160-character segments a text will be billed as. Segments drop
    /// to 153 characters once a message needs more than one, because each carries a
    /// header saying how they fit together.</summary>
    public static int TextSegments(string text) =>
        text.Length == 0 ? 0 : text.Length <= 160 ? 1 : (int)Math.Ceiling(text.Length / 153.0);
}
