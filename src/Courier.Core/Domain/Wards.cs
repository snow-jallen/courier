namespace Courier.Core.Domain;

/// <summary>The units in the Manti Utah Stake. The LCR report wraps this column
/// across up to three lines, so a fragment such as "Manti 3rd" or "5th" has to be
/// snapped back to the full name. There is no 7th ward.</summary>
public static class Wards
{
    public static readonly IReadOnlyList<string> All =
    [
        "Manti 1st Ward", "Manti 2nd Ward", "Manti 3rd Ward", "Manti 4th Ward",
        "Manti 5th Ward", "Manti 6th Ward", "Manti 8th Ward", "Manti 9th Ward",
        "Manti 10th Ward", "Sterling Ward",
    ];

    /// <summary>Snaps a possibly-truncated unit string onto a known ward.
    /// Returns null when the fragment is missing or matches more than one ward,
    /// so the caller can surface it for review rather than guess.</summary>
    public static string? Snap(string? raw)
    {
        var s = Normalize(raw);
        if (s.Length == 0) return null;

        foreach (var w in All)
            if (Normalize(w) == s) return w;

        // "Manti 3rd", "3rd Ward", "10th", and leaked neighbouring lines such as
        // "Ward Manti 8th Ward" all reduce to a single ordinal.
        var ordinal = FindOrdinal(s);
        if (ordinal is not null)
        {
            var hits = All.Where(w => FindOrdinal(Normalize(w)) == ordinal).ToList();
            if (hits.Count == 1) return hits[0];
        }

        if (s.Contains("sterling", StringComparison.Ordinal)) return "Sterling Ward";
        return null;
    }

    private static string Normalize(string? s) =>
        string.Join(' ', (s ?? "").ToLowerInvariant()
            .Split((char[])[' ', '\t', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries));

    private static string? FindOrdinal(string normalized)
    {
        string[] ordinals = ["10th", "1st", "2nd", "3rd", "4th", "5th", "6th", "8th", "9th"];
        var found = ordinals.Where(o => normalized.Contains(o, StringComparison.Ordinal)).ToList();
        return found.Count == 1 ? found[0] : null;
    }
}
