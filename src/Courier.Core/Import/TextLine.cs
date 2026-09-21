using System.Text;
using UglyToad.PdfPig.Content;

namespace Courier.Core.Import;

/// <summary>One run of glyphs sharing a text baseline, ordered left to right.</summary>
public sealed class TextLine
{
    /// <summary>Widest horizontal gap, in points, that still counts as the same word.
    /// Measured between advance boxes, so glyphs inside a word are touching.</summary>
    private const double WordGap = 1.2;

    /// <summary>Gap wide enough that a space belongs between two glyphs. A backstop:
    /// the report writes real space characters, which are kept and emitted as-is.</summary>
    private const double SpaceGap = 1.2;

    public double Baseline { get; }
    public IReadOnlyList<Letter> Letters { get; }

    public TextLine(double baseline, IEnumerable<Letter> letters)
    {
        Baseline = baseline;
        Letters = letters.OrderBy(l => l.StartBaseLine.X).ToList();
    }

    public string Text => Cell(double.NegativeInfinity, double.PositiveInfinity);

    /// <summary>The text of this line falling within a column's horizontal span.
    /// A glyph belongs to the column its left edge starts in.</summary>
    public string Cell(double left, double right)
    {
        var sb = new StringBuilder();
        double? previousRight = null;
        foreach (var l in Letters)
        {
            var x = l.StartBaseLine.X;
            if (x < left || x >= right) continue;
            if (previousRight is { } p && x - p > SpaceGap) sb.Append(' ');
            sb.Append(l.Value);
            previousRight = l.EndBaseLine.X;
        }
        return Collapse(sb.ToString());
    }

    /// <summary>Whitespace-separated words with the x each one starts at, used to
    /// locate the report's column headings.</summary>
    public IReadOnlyList<(double X, string Text)> Words()
    {
        var words = new List<(double, string)>();
        var sb = new StringBuilder();
        double start = 0;
        double? previousRight = null;

        void Flush()
        {
            if (sb.Length > 0) words.Add((start, sb.ToString()));
            sb.Clear();
        }

        foreach (var l in Letters)
        {
            var x = l.StartBaseLine.X;
            var blank = string.IsNullOrWhiteSpace(l.Value);
            if (blank || (previousRight is { } p && x - p > WordGap)) Flush();
            if (!blank)
            {
                if (sb.Length == 0) start = x;
                sb.Append(l.Value);
            }
            previousRight = l.EndBaseLine.X;
        }
        Flush();
        return words;
    }

    internal static string Collapse(string s) =>
        string.Join(' ', s.Split((char[])[' ', '\t', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries));
}
