namespace Courier.Core.Import;

/// <summary>A report that is a table whose own headings give the column positions.
/// Both reports Courier reads today are of this shape, so both are instances of this
/// class rather than separate code. A report that is not this shape implements
/// <see cref="IReportFormat"/> directly.</summary>
public sealed class ColumnLayoutFormat(
    string name,
    IReadOnlyList<ColumnSpec> columns,
    double rowBreakFactor,
    ReportFields carries,
    IReadOnlyList<string> furniture) : IReportFormat
{
    /// <summary>A line must carry at least this many of the report's headings to
    /// contribute any of them. The Single Adults report's toolbar line reads "Group by
    /// Unit Edit Report", and matching its "Unit" puts the columns out of order and
    /// folds four fields into one.</summary>
    private const int MinimumHeadingsOnALine = 2;

    public string Name => name;
    public double RowBreakFactor => rowBreakFactor;
    public ReportFields Carries => carries;

    public ColumnLayout? Detect(IReadOnlyList<TextLine> group)
    {
        var found = new double?[columns.Count];

        foreach (var line in group)
        {
            var onThisLine = Match(line);
            if (onThisLine.Count < MinimumHeadingsOnALine) continue;
            foreach (var (column, x) in onThisLine)
                found[column] ??= x;
        }

        // Every column a format declares is required. A group yielding six of seven
        // is some other table, not this report's heading.
        if (found.Any(x => x is null)) return null;

        return ColumnLayout.From(
            found.Select((x, i) => (x!.Value, columns[i].Field)).ToList());
    }

    public bool IsFurniture(string lineText)
    {
        if (lineText.Length == 0) return true;

        // Both reports number their pages; only one of them writes "Page n of m".
        if (lineText.StartsWith("Page ", StringComparison.Ordinal)
            && lineText.Contains(" of ", StringComparison.Ordinal)) return true;

        return furniture.Any(f => lineText.Contains(f, StringComparison.Ordinal));
    }

    /// <summary>Which of this report's headings appear on one line, and where each
    /// one starts. Phrases are tried longest first so that a column named by several
    /// words is not stolen by a shorter phrase sharing its first word.</summary>
    private IReadOnlyList<(int Column, double X)> Match(TextLine line)
    {
        var words = line.Words();
        var hits = new List<(int, double)>();
        var claimed = new bool[words.Count];

        var candidates = columns
            .SelectMany((c, i) => c.Headings.Select(h => (Column: i, Words: h.Split(' '))))
            .OrderByDescending(c => c.Words.Length)
            .ToList();

        foreach (var (column, phrase) in candidates)
        {
            if (hits.Any(h => h.Item1 == column)) continue;

            for (var start = 0; start + phrase.Length <= words.Count; start++)
            {
                var matches = true;
                for (var w = 0; w < phrase.Length && matches; w++)
                    matches = !claimed[start + w]
                           && string.Equals(words[start + w].Text, phrase[w], StringComparison.Ordinal);
                if (!matches) continue;

                for (var w = 0; w < phrase.Length; w++) claimed[start + w] = true;
                hits.Add((column, words[start].X));
                break;
            }
        }

        return hits;
    }
}
