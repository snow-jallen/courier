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
    public void Refuses_a_pdf_that_is_not_the_report()
    {
        var notTheReport = new PdfNotAReport();
        var error = Assert.Throws<LcrReportException>(
            () => LcrReportParser.Parse(new MemoryStream(notTheReport.Bytes), "holiday-photos.pdf"));
        Assert.Contains("does not look like the Single Adults report", error.Message, StringComparison.Ordinal);
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
