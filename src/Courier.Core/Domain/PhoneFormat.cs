namespace Courier.Core.Domain;

/// <summary>Phone numbers are stored in the form a service will dial — +14355550100 —
/// and shown in the form people read them in. Nobody in a directory thinks of their
/// neighbour's number as a plus and eleven digits.</summary>
public static class PhoneFormat
{
    /// <summary>"(435) 555-0100". Anything that is not a plain North American number
    /// comes back as it went in, since guessing at an unfamiliar shape reads worse than
    /// leaving it alone.</summary>
    public static string ForDisplay(string? number)
    {
        var raw = (number ?? "").Trim();
        if (raw.Length == 0) return "";

        var digits = new string(raw.Where(char.IsAsciiDigit).ToArray());
        if (digits.Length == 11 && digits[0] == '1') digits = digits[1..];
        if (digits.Length != 10) return raw;

        return $"({digits[..3]}) {digits[3..6]}-{digits[6..]}";
    }
}
