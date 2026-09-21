using System.Globalization;
using System.Text.RegularExpressions;
using Courier.Core.Domain;

namespace Courier.Core.Import;

/// <summary>A report row cleaned into the shapes the database stores.</summary>
public sealed record NormalizedPerson(
    string LastName,
    string FirstName,
    string DisplayName,
    string? Ward,
    int? Age,
    int? BirthMonth,
    int? BirthDay,
    string? Address,
    string? Email,
    string? PhoneRaw,
    string? PhoneE164,
    bool AreaCodeAssumed,
    string RawUnit)
{
    public string SortName => $"{LastName}, {FirstName}";
}

public static partial class LcrNormalizer
{
    /// <summary>Sanpete County. The report prints some numbers without an area code;
    /// those are flagged so the app can ask rather than quietly dial the wrong state.</summary>
    public const string DefaultAreaCode = "435";

    public static NormalizedPerson Normalize(LcrRow row, string defaultAreaCode = DefaultAreaCode)
    {
        var parts = row.Name.Split(',', 2, StringSplitOptions.TrimEntries);
        var last = parts.Length > 0 ? parts[0] : row.Name.Trim();
        var first = parts.Length > 1 ? parts[1] : "";

        var (month, day) = ParseBirthday(row.Birthday);
        var (raw, e164, assumed) = ParsePhone(row.Phone, defaultAreaCode);

        return new NormalizedPerson(
            LastName: last,
            FirstName: first,
            DisplayName: row.Name.Trim(),
            Ward: Wards.Snap(row.Unit),
            Age: int.TryParse(row.Age, NumberStyles.None, CultureInfo.InvariantCulture, out var age) ? age : null,
            BirthMonth: month,
            BirthDay: day,
            Address: Blank(row.Address),
            Email: Blank(row.Email),
            PhoneRaw: raw,
            PhoneE164: e164,
            AreaCodeAssumed: assumed,
            RawUnit: row.Unit);
    }

    /// <summary>The report prints a day and a month but never a year.</summary>
    public static (int? Month, int? Day) ParseBirthday(string? value)
    {
        var m = BirthdayPattern().Match(value ?? "");
        if (!m.Success) return (null, null);

        var months = new[] { "jan", "feb", "mar", "apr", "may", "jun", "jul", "aug", "sep", "oct", "nov", "dec" };
        var month = Array.IndexOf(months, m.Groups[2].Value.ToLowerInvariant()) + 1;
        if (month == 0) return (null, null);

        var day = int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
        return day is >= 1 and <= 31 ? (month, day) : (null, null);
    }

    /// <summary>Returns the number as printed, an E.164 form when one can be built,
    /// and whether an area code had to be supplied.</summary>
    public static (string? Raw, string? E164, bool AreaCodeAssumed) ParsePhone(string? value, string defaultAreaCode = DefaultAreaCode)
    {
        var raw = Blank(value);
        if (raw is null) return (null, null, false);

        var digits = new string(raw.Where(char.IsAsciiDigit).ToArray());
        return digits.Length switch
        {
            10 => (raw, $"+1{digits}", false),
            11 when digits[0] == '1' => (raw, $"+{digits}", false),
            7 => (raw, $"+1{defaultAreaCode}{digits}", true),
            _ => (raw, null, false),
        };
    }

    private static string? Blank(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    [GeneratedRegex(@"^\s*(\d{1,2})\s+([A-Za-z]{3})\s*$")]
    private static partial Regex BirthdayPattern();
}
