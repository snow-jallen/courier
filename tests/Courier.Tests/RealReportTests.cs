using System.Text.RegularExpressions;
using Courier.Core.Domain;
using Courier.Core.Import;
using Xunit.Abstractions;

namespace Courier.Tests;

/// <summary>Measures the parser against a real export. These are the numbers that
/// decide whether an import can be trusted, so they are asserted, not just printed.</summary>
public sealed class RealReportTests(ITestOutputHelper output)
{
    [RequiresRealReport]
    public void Reads_every_person_from_the_report()
    {
        var report = LcrReportParser.Parse(TestPaths.RealReport!);

        var phone = new Regex(@"^(\(\d{3}\)\s*)?\d{3}-\d{4}$");
        var email = new Regex(@"^[^@\s]+@[^@\s]+\.[A-Za-z]{2,}$");
        var birthday = new Regex(@"^\d{1,2} [A-Z][a-z]{2}$");
        var age = new Regex(@"^\d{1,3}$");

        int Bad(Func<LcrRow, string> field, Regex shape) =>
            report.Rows.Count(r => field(r).Length > 0 && !shape.IsMatch(field(r)));

        output.WriteLine($"pages       {report.PageCount}");
        output.WriteLine($"people      {report.Rows.Count}");
        output.WriteLine($"bad phone   {Bad(r => r.Phone, phone)}");
        output.WriteLine($"bad email   {Bad(r => r.Email, email)}");
        output.WriteLine($"bad bday    {Bad(r => r.Birthday, birthday)}");
        output.WriteLine($"bad age     {Bad(r => r.Age, age)}");
        output.WriteLine($"no email    {report.Rows.Count(r => r.Email.Length == 0)}");
        output.WriteLine($"no phone    {report.Rows.Count(r => r.Phone.Length == 0)}");
        foreach (var r in report.Rows.Where(r => r.Phone.Length > 0 && !phone.IsMatch(r.Phone)).Take(5))
            output.WriteLine($"  PHONE {r.Name} = '{r.Phone}'");
        foreach (var r in report.Rows.Where(r => r.Email.Length > 0 && !email.IsMatch(r.Email)).Take(5))
            output.WriteLine($"  EMAIL {r.Name} = '{r.Email}'");

        Assert.Equal(29, report.PageCount);

        // The report prints "Count: 427" in its own footer. Reading exactly that many
        // people back is the strongest check available that no row was dropped.
        Assert.Equal(427, report.Rows.Count);
        Assert.Equal(0, Bad(r => r.Phone, phone));
        Assert.Equal(0, Bad(r => r.Age, age));
        Assert.Equal(0, Bad(r => r.Birthday, birthday));
        Assert.Equal(0, Bad(r => r.Email, email));
    }

    [RequiresRealReport]
    public void Every_unit_snaps_onto_a_known_ward()
    {
        var report = LcrReportParser.Parse(TestPaths.RealReport!);
        var unsnapped = report.Rows.Where(r => Wards.Snap(r.Unit) is null).ToList();

        foreach (var r in unsnapped.Take(10)) output.WriteLine($"  UNIT {r.Name} = '{r.Unit}'");
        output.WriteLine($"distinct raw units: {report.Rows.Select(r => r.Unit).Distinct().Count()}");

        Assert.Empty(unsnapped);
    }

    [RequiresRealReport]
    public void Every_person_has_a_surname_and_a_given_name()
    {
        var report = LcrReportParser.Parse(TestPaths.RealReport!);
        var malformed = report.Rows
            .Where(r => r.Name.Split(',', StringSplitOptions.TrimEntries).Length != 2
                     || r.Name.Split(',', StringSplitOptions.TrimEntries).Any(p => p.Length == 0))
            .ToList();
        foreach (var r in malformed.Take(10)) output.WriteLine($"  NAME '{r.Name}'");
        Assert.Empty(malformed);
    }
}

public sealed class ColumnDetectionTests(ITestOutputHelper output)
{
    [RequiresRealReport]
    public void Finds_the_column_positions_from_the_headings()
    {
        using var document = UglyToad.PdfPig.PdfDocument.Open(TestPaths.RealReport!);
        var lines = PdfTableReader.ReadLines(document.GetPage(1));
        var columns = LcrColumns.Detect(lines);
        output.WriteLine($"columns: {columns}");
        output.WriteLine($"row break at: {PdfTableReader.RowBreakThreshold(lines):0.00}pt");
        Assert.NotNull(columns);
    }
}
