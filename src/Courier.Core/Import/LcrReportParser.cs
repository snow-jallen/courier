using System.Security.Cryptography;
using UglyToad.PdfPig;

namespace Courier.Core.Import;

public sealed record LcrReport(
    IReadOnlyList<LcrRow> Rows, int PageCount, string FileName, string Sha256);

public sealed class LcrReportException(string message) : Exception(message);

/// <summary>Reads the Single Adults report exported from LCR.</summary>
public static class LcrReportParser
{
    /// <summary>Lines belonging to the report's own furniture rather than to a person.</summary>
    private static readonly string[] Furniture =
    [
        "lcr.churchofjesuschrist.org", "Single Adults", "Description", "Group by Unit",
        "Count:", "Preferred", "Individual", "Street 1", "(1 Jan)",
    ];

    public static LcrReport Parse(string path)
    {
        using var stream = File.OpenRead(path);
        return Parse(stream, Path.GetFileName(path));
    }

    public static LcrReport Parse(Stream stream, string fileName)
    {
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        var bytes = buffer.ToArray();
        var sha = Convert.ToHexStringLower(SHA256.HashData(bytes));

        var rows = new List<LcrRow>();
        LcrColumns? columns = null;
        int pageCount;

        using (var document = PdfDocument.Open(bytes))
        {
            pageCount = document.NumberOfPages;
            foreach (var page in document.GetPages())
            {
                var lines = PdfTableReader.ReadLines(page);
                columns = LcrColumns.Detect(lines) ?? columns;
                if (columns is null) continue;

                var body = lines.Where(IsBodyLine).ToList();
                var step = PdfTableReader.RowBreakThreshold(body);
                foreach (var group in PdfTableReader.GroupIntoRows(body, step))
                {
                    var row = BuildRow(group, columns, page.Number);
                    if (row is not null) rows.Add(row);
                }
            }
        }

        if (columns is null)
            throw new LcrReportException(
                $"'{fileName}' does not look like the Single Adults report from LCR — " +
                "its column headings (Preferred Name, Individual E-mail, Unit, Age, Birthday) were not found.");

        return new LcrReport(Stitch(rows), pageCount, fileName, sha);
    }

    private static bool IsBodyLine(TextLine line)
    {
        var text = line.Text;
        if (text.Length == 0) return false;
        if (text.StartsWith("Page ", StringComparison.Ordinal) && text.Contains(" of ", StringComparison.Ordinal))
            return false;
        return !Furniture.Any(f => text.Contains(f, StringComparison.Ordinal));
    }

    private static LcrRow? BuildRow(IReadOnlyList<TextLine> group, LcrColumns columns, int page)
    {
        var edges = columns.Edges;
        var cells = new List<string>[7];
        for (var c = 0; c < 7; c++) cells[c] = [];

        foreach (var line in group)
            for (var c = 0; c < 7; c++)
            {
                var text = line.Cell(edges[c], edges[c + 1]);
                if (text.Length > 0) cells[c].Add(text);
            }

        string Join(int c) => string.Join(' ', cells[c]);

        var row = new LcrRow(Join(0), Join(1), Join(2), Join(3), Join(4), Join(5), Join(6), page);
        var empty = row is { Name.Length: 0, Email.Length: 0, Phone.Length: 0, Unit.Length: 0,
                             Age.Length: 0, Birthday.Length: 0, Address.Length: 0 };
        return empty ? null : row;
    }

    /// <summary>Re-joins a person split across two row groups. A long name can push a
    /// row's wrapped lines far enough apart to read as a break, leaving a fragment
    /// with no comma in the name; that fragment belongs to the row above it.</summary>
    private static IReadOnlyList<LcrRow> Stitch(IReadOnlyList<LcrRow> rows)
    {
        var stitched = new List<LcrRow>();
        foreach (var row in rows)
        {
            if (!row.LooksLikeAPerson && stitched.Count > 0)
            {
                var previous = stitched[^1];
                stitched[^1] = new LcrRow(
                    Merge(previous.Name, row.Name),
                    Merge(previous.Email, row.Email),
                    Merge(previous.Phone, row.Phone),
                    Merge(previous.Unit, row.Unit),
                    Merge(previous.Age, row.Age),
                    Merge(previous.Birthday, row.Birthday),
                    Merge(previous.Address, row.Address),
                    previous.Page);
                continue;
            }
            stitched.Add(row);
        }
        return stitched.Where(r => r.LooksLikeAPerson).ToList();
    }

    private static string Merge(string a, string b) =>
        a.Length == 0 ? b : b.Length == 0 ? a : $"{a} {b}";
}
