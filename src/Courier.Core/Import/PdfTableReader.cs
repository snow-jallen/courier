using UglyToad.PdfPig.Content;

namespace Courier.Core.Import;

/// <summary>Turns a page's glyphs back into lines and rows.
///
/// The LCR report is a rendered HTML table with no rules or delimiters in its text,
/// so the only structure available is geometry. Two facts about it drive everything
/// here, both measured from real exports:
///
///  * Cells are vertically CENTRED, not top-aligned. A cell wrapping to three lines
///    sits at the row's centre +/- one line height, a two-line cell at +/- half of
///    one. Glyph baselines therefore land on a grid of half the line height, and a
///    row's first line does not line up with its neighbours' first lines.
///  * Rows are separated by more vertical space than the lines inside them, by a
///    ratio that differs from report to report. How much more is the format's
///    business; see IReportFormat.RowBreakFactor.
/// </summary>
public static class PdfTableReader
{
    /// <summary>Two baselines closer than this are the same line of text. PdfPig
    /// reports a true baseline per glyph, so descenders need no special handling.</summary>
    private const double BaselineTolerance = 1.5;

    /// <summary>Space glyphs are kept: they carry the report's own word breaks, which
    /// is more reliable than inferring every space from a horizontal gap.</summary>
    public static IReadOnlyList<TextLine> ReadLines(Page page)
    {
        var groups = new List<(double Sum, int Count, List<Letter> Letters)>();

        foreach (var letter in page.Letters.OrderByDescending(l => l.StartBaseLine.Y))
        {
            var y = letter.StartBaseLine.Y;
            var placed = false;
            for (var i = 0; i < groups.Count; i++)
            {
                var g = groups[i];
                if (Math.Abs(g.Sum / g.Count - y) > BaselineTolerance) continue;
                g.Letters.Add(letter);
                groups[i] = (g.Sum + y, g.Count + 1, g.Letters);
                placed = true;
                break;
            }
            if (!placed) groups.Add((y, 1, [letter]));
        }

        return groups
            .Select(g => new TextLine(g.Sum / g.Count, g.Letters))
            .OrderByDescending(l => l.Baseline)
            .ToList();
    }

    /// <summary>The vertical gap at which one row ends and the next begins.
    ///
    /// Derived from the size of the text on the page and the format's own factor.
    /// Scaling against the text size rather than against the other gaps on the page is
    /// what keeps this working where every row happens to be a single line — a case no
    /// relative rule can tell apart from one tall row. The factor itself belongs to the
    /// report, not to the page: 2.0 separates the Single Adults report's rows and would
    /// merge every row of the Organizations and Callings report, whose rows sit 1.68x
    /// its text size apart.</summary>
    public static double RowBreakThreshold(IReadOnlyList<TextLine> lines, double factor)
    {
        var sizes = lines.SelectMany(l => l.Letters)
            .Select(l => l.PointSize)
            .Where(s => s > 0)
            .OrderBy(s => s)
            .ToList();
        return sizes.Count == 0 ? double.MaxValue : sizes[sizes.Count / 2] * factor;
    }

    /// <summary>Groups lines into table rows on the vertical gap between them.</summary>
    public static IReadOnlyList<IReadOnlyList<TextLine>> GroupIntoRows(IReadOnlyList<TextLine> lines, double threshold)
    {
        var rows = new List<IReadOnlyList<TextLine>>();
        if (lines.Count == 0) return rows;

        var current = new List<TextLine> { lines[0] };
        for (var i = 1; i < lines.Count; i++)
        {
            if (lines[i - 1].Baseline - lines[i].Baseline > threshold)
            {
                rows.Add(current);
                current = [];
            }
            current.Add(lines[i]);
        }
        rows.Add(current);
        return rows;
    }
}
