namespace Courier.Core.Import;

/// <summary>Where each column of a report starts, in PDF points, read from the
/// report's own headings rather than hard-coded, so a re-styled export does not
/// silently shift every field by one column.
///
/// Columns arrive in the order the format declares them, which is the order they
/// appear across the page. A layout whose positions do not run strictly left to right
/// is rejected: it means a heading was matched somewhere it does not belong, and the
/// two reports each contain a word that appears as a heading in more than one place.
///
/// A column may have no field behind it. The Organizations and Callings report prints
/// Gender between the name and the age; the edge has to exist or the "F" joins the
/// name, and nothing stores what is in it.</summary>
public sealed class ColumnLayout
{
    /// <summary>Cells are left-aligned on these x positions, but a glyph can start a
    /// fraction of a point to the left of its column. Without this slack the opening
    /// bracket of "(435) 555-0100" lands in the column before the phone.</summary>
    private const double Slack = 1.5;

    private readonly IReadOnlyList<(double X, LcrField? Field)> _columns;

    private ColumnLayout(IReadOnlyList<(double X, LcrField? Field)> columns) => _columns = columns;

    /// <summary>Null when there are no columns, or when their positions do not
    /// increase left to right.</summary>
    public static ColumnLayout? From(IReadOnlyList<(double X, LcrField? Field)> columns)
    {
        if (columns.Count == 0) return null;
        for (var i = 1; i < columns.Count; i++)
            if (columns[i].X <= columns[i - 1].X) return null;
        return new ColumnLayout(columns);
    }

    public bool Has(LcrField field) => _columns.Any(c => c.Field == field);

    /// <summary>One field's text, gathered from every line of a row group, so a cell
    /// that wraps over several lines comes back as one string.</summary>
    public string Cell(IReadOnlyList<TextLine> group, LcrField field)
    {
        var index = -1;
        for (var i = 0; i < _columns.Count; i++)
            if (_columns[i].Field == field) { index = i; break; }
        if (index < 0) return "";

        var left = _columns[index].X - Slack;
        var right = index + 1 < _columns.Count
            ? _columns[index + 1].X - Slack
            : double.PositiveInfinity;

        var parts = new List<string>();
        foreach (var line in group)
        {
            var text = line.Cell(left, right);
            if (text.Length > 0) parts.Add(text);
        }
        return string.Join(' ', parts);
    }
}
