using System.Security.Cryptography;
using UglyToad.PdfPig;

namespace Courier.Core.Import;

public sealed record LcrReport(
    IReadOnlyList<LcrRow> Rows, int PageCount, string FileName, string Sha256, ReportSource Source);

public sealed class LcrReportException(string message) : Exception(message);

/// <summary>Reads a report exported from LCR.
///
/// Which report it is, is worked out by inspecting the file: each registered format is
/// offered the pages in turn, and the first to recognise one drives every page. See
/// ReportFormats for the two Courier knows, and docs/architecture.md for why reading
/// these PDFs takes geometry at all.</summary>
public static class LcrReportParser
{
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

        using var document = PdfDocument.Open(bytes);
        var pageCount = document.NumberOfPages;

        // Every page is read at most once, however many formats are offered it.
        var cache = new Dictionary<int, IReadOnlyList<TextLine>>();
        IReadOnlyList<TextLine> LinesOf(int number) =>
            cache.TryGetValue(number, out var cached)
                ? cached
                : cache[number] = PdfTableReader.ReadLines(document.GetPage(number));

        var format = Choose(pageCount, LinesOf)
            ?? throw new LcrReportException(Unrecognised(fileName));

        var rows = new List<LcrRow>();
        ColumnLayout? layout = null;

        for (var number = 1; number <= pageCount; number++)
        {
            var lines = LinesOf(number);

            // Below the heading, and only below it. The Organizations and Callings
            // report prints the stake's Single Adult callings above the members table
            // on page 1; read as people, those rows are stitched into the row below
            // and corrupt it.
            var floor = double.PositiveInfinity;
            foreach (var group in PdfTableReader.GroupIntoRows(lines, Threshold(lines, format)))
            {
                var found = format.Detect(group);
                if (found is null) continue;
                layout = found;
                floor = group.Min(l => l.Baseline);
                break;
            }

            // A page before the first heading has nothing to read. A page after it
            // with no heading of its own is a continuation, and keeps every line.
            if (layout is null) continue;

            var body = lines
                .Where(l => l.Baseline < floor)
                .Where(l => !format.IsFurniture(l.Text))
                .ToList();

            foreach (var group in PdfTableReader.GroupIntoRows(body, Threshold(body, format)))
            {
                var row = BuildRow(group, layout, number);
                if (row is not null) rows.Add(row);
            }
        }

        return new LcrReport(
            Stitch(rows), pageCount, fileName, sha, new ReportSource(format.Name, format.Carries));
    }

    private static double Threshold(IReadOnlyList<TextLine> lines, IReportFormat format) =>
        PdfTableReader.RowBreakThreshold(lines, format.RowBreakFactor);

    /// <summary>The first registered format to recognise a heading anywhere in the
    /// document. Null when none does.</summary>
    private static IReportFormat? Choose(int pageCount, Func<int, IReadOnlyList<TextLine>> linesOf)
    {
        for (var number = 1; number <= pageCount; number++)
        {
            var lines = linesOf(number);
            foreach (var format in ReportFormats.Known)
                foreach (var group in PdfTableReader.GroupIntoRows(lines, Threshold(lines, format)))
                    if (format.Detect(group) is not null) return format;
        }
        return null;
    }

    private static string Unrecognised(string fileName) =>
        $"'{fileName}' does not look like a report Courier can read. It knows the " +
        $"{string.Join(" report and the ", ReportFormats.Known.Select(f => f.Name))} report. " +
        "Export one of those from LCR as a PDF and open it here.";

    private static LcrRow? BuildRow(IReadOnlyList<TextLine> group, ColumnLayout layout, int page)
    {
        var row = new LcrRow(
            layout.Cell(group, LcrField.Name),
            layout.Cell(group, LcrField.Email),
            layout.Cell(group, LcrField.Phone),
            layout.Cell(group, LcrField.Unit),
            layout.Cell(group, LcrField.Age),
            layout.Cell(group, LcrField.Birthday),
            layout.Cell(group, LcrField.Address),
            page);

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
