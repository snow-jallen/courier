namespace Courier.Core.Import;

/// <summary>Where each column of the Single Adults report starts, in PDF points,
/// read from the report's own column headings rather than hard-coded, so a
/// re-styled export does not silently shift every field by one column.</summary>
public sealed record LcrColumns(
    double Name, double Email, double Phone, double Unit, double Age, double Birthday, double Address)
{
    /// <summary>Cells are left-aligned on these x positions, but a glyph can start a
    /// fraction of a point to the left of its column. Without this slack the opening
    /// bracket of "(435) 555-0100" lands in the e-mail column.</summary>
    private const double Slack = 1.5;

    public IReadOnlyList<double> Edges =>
        [Name - Slack, Email - Slack, Phone - Slack, Unit - Slack,
         Age - Slack, Birthday - Slack, Address - Slack, double.PositiveInfinity];

    /// <summary>Reads the column positions off a page's heading row. Returns null
    /// when this page has no heading, which is normal for continuation pages.</summary>
    public static LcrColumns? Detect(IReadOnlyList<TextLine> lines)
    {
        var found = new Dictionary<string, double>(StringComparer.Ordinal);

        // Only lines carrying two or more headings count. The report's toolbar line
        // reads "Group by Unit Edit Report", and matching its "Unit" would put the
        // columns out of order and fold four fields into one.
        foreach (var line in lines)
        {
            var labels = LabelsOn(line);
            if (labels.Count < 2) continue;
            foreach (var (label, x) in labels)
                if (!found.ContainsKey(label)) found[label] = x;
        }

        string[] required = ["Name", "Email", "Phone", "Unit", "Age", "Birthday", "Address"];
        if (required.Any(r => !found.ContainsKey(r))) return null;

        var columns = new LcrColumns(
            found["Name"], found["Email"], found["Phone"], found["Unit"],
            found["Age"], found["Birthday"], found["Address"]);

        // The columns must run left to right. If they do not, a heading was matched
        // somewhere it does not belong and the layout is not one we understand.
        var positions = required.Select(r => found[r]).ToList();
        for (var i = 1; i < positions.Count; i++)
            if (positions[i] <= positions[i - 1]) return null;

        return columns;
    }

    private static IReadOnlyList<(string Label, double X)> LabelsOn(TextLine line)
    {
        var words = line.Words();
        var labels = new List<(string, double)>();
        for (var i = 0; i < words.Count; i++)
        {
            var (x, text) = words[i];
            switch (text)
            {
                // "Preferred Name" wraps onto two lines, and the line carrying
                // "Name" is also the only one carrying "Phone".
                case "Preferred":
                case "Name": labels.Add(("Name", x)); break;
                case "Phone": labels.Add(("Phone", x)); break;
                case "Unit": labels.Add(("Unit", x)); break;
                case "Age": labels.Add(("Age", x)); break;
                case "Birthday": labels.Add(("Birthday", x)); break;
                case "Address": labels.Add(("Address", x)); break;
                case "Individual" when i + 1 < words.Count
                        && words[i + 1].Text.StartsWith("E-mail", StringComparison.Ordinal):
                    labels.Add(("Email", x)); break;
            }
        }
        return labels;
    }
}
