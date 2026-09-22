using Courier.Core.Import;

namespace Courier.Tests;

public sealed class LcrReportParserTests
{
    private static IReadOnlyList<LcrRow> Rows()
    {
        using var stream = new MemoryStream(SyntheticReport.Build());
        return LcrReportParser.Parse(stream, "synthetic.pdf").Rows;
    }

    [Fact]
    public void Reads_one_row_per_person_and_no_page_furniture()
    {
        var rows = Rows();
        Assert.Equal(3, rows.Count);
        Assert.Equal(["Ashby, Miriam", "Quilley, Barnaby", "Crowther, Dell"], rows.Select(r => r.Name));
    }

    [Fact]
    public void Rejoins_a_cell_that_wraps_across_three_lines()
    {
        // "Manti", "2nd" and "Ward" are on three different baselines, one of which is
        // shared with the e-mail and two of which are shared with nothing else.
        Assert.Equal("Manti 2nd Ward", Rows()[0].Unit);
    }

    [Fact]
    public void Keeps_each_field_in_its_own_column()
    {
        var row = Rows()[0];
        Assert.Equal("m.ashby@example.com", row.Email);
        Assert.Equal("(435) 555-0111", row.Phone);
        Assert.Equal("86", row.Age);
        Assert.Equal("17 Jan", row.Birthday);
        Assert.Equal("812 North 700 East", row.Address);
    }

    [Fact]
    public void Leaves_a_missing_field_empty_rather_than_borrowing_the_next_column()
    {
        var row = Rows()[2];
        Assert.Equal("", row.Email);
        Assert.Equal("555-0133", row.Phone);
        Assert.Equal("Manti 4th Ward", row.Unit);
        Assert.Equal("", row.Address);
    }

    [Fact]
    public void Reads_a_row_laid_out_at_the_real_reports_own_geometry()
    {
        // Contrast with the fixture above: this one uses the real Single Adults
        // export's own absolute numbers (8.9pt text, 12.8pt wraps, 23.2pt row gap)
        // rather than values merely chosen to contrast with each other, so a bad
        // edit to RowBreakFactor shows up here even with no real export on hand.
        using var stream = new MemoryStream(SyntheticReport.BuildAtRealGeometry());
        var report = LcrReportParser.Parse(stream, "synthetic-real-geometry.pdf");

        var row = Assert.Single(report.Rows);
        Assert.Equal("Ashby, Miriam", row.Name);
        Assert.Equal("m.ashby@example.com", row.Email);
        Assert.Equal("(435) 555-0111", row.Phone);
        Assert.Equal("Manti", row.Unit);
        Assert.Equal("40", row.Age);
        Assert.Equal("6 May", row.Birthday);
        Assert.Equal("12 Main", row.Address);
    }

    [Fact]
    public void Refuses_a_pdf_that_is_not_a_report_it_knows()
    {
        var notTheReport = new PdfNotAReport();
        var error = Assert.Throws<LcrReportException>(
            () => LcrReportParser.Parse(new MemoryStream(notTheReport.Bytes), "holiday-photos.pdf"));

        Assert.Contains("holiday-photos.pdf", error.Message, StringComparison.Ordinal);
        Assert.Contains("Single Adults", error.Message, StringComparison.Ordinal);
        Assert.Contains("Organizations and Callings", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Says_which_report_it_read()
    {
        using var stream = new MemoryStream(SyntheticReport.Build());
        var report = LcrReportParser.Parse(stream, "synthetic.pdf");

        Assert.Equal("Single Adults", report.Source.FormatName);
        Assert.Equal(ReportFields.All, report.Source.Carries);
    }

    private sealed class PdfNotAReport
    {
        public byte[] Bytes { get; }
        public PdfNotAReport()
        {
            var builder = new UglyToad.PdfPig.Writer.PdfDocumentBuilder();
            var page = builder.AddPage(612, 792);
            var font = builder.AddStandard14Font(UglyToad.PdfPig.Fonts.Standard14Fonts.Standard14Font.Helvetica);
            page.AddText("Ward Christmas Party", 12, new UglyToad.PdfPig.Core.PdfPoint(72, 700), font);
            Bytes = builder.Build();
        }
    }
}
