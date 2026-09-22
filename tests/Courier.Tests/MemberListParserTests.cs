using Courier.Core.Import;

namespace Courier.Tests;

public sealed class MemberListParserTests
{
    private static LcrReport Report()
    {
        using var stream = new MemoryStream(SyntheticMemberList.Build());
        return LcrReportParser.Parse(stream, "member-list.pdf");
    }

    [Fact]
    public void Recognises_the_report_it_is()
    {
        var report = Report();
        Assert.Equal("Member List", report.Source.FormatName);
        Assert.Equal(2, report.PageCount);
    }

    [Fact]
    public void Reads_one_row_per_person_across_both_pages()
    {
        // The report's own footer says Count: 7. Reading exactly that many back is the
        // strongest check available that no row was dropped or invented.
        var rows = Report().Rows;
        Assert.Equal(7, rows.Count);
        Assert.Equal(
            ["Ashdown, Marigold", "Quilley, Barnaby", "Crowther, Dell", "Winslade, Verity",
             "Featherstonehaugh, Wilhelmina", "Wraithwell, Mordecai", "Yelverton, Katriona"],
            rows.Select(r => r.Name));
    }

    [Fact]
    public void Leaves_out_the_title_and_the_unit_printed_above_the_table()
    {
        // "Member List" and "Manti 3rd Ward (12564)" sit above the heading on page 1.
        var rows = Report().Rows;
        Assert.DoesNotContain(rows, r => r.Name.Contains("Member", StringComparison.Ordinal));
        Assert.DoesNotContain(rows, r => r.Name.Contains("Manti", StringComparison.Ordinal));
    }

    [Fact]
    public void Keeps_each_field_in_its_own_column()
    {
        var row = Report().Rows[0];
        Assert.Equal("Ashdown, Marigold", row.Name);   // the Gender glyph does not join it
        Assert.Equal("17 Jan", row.Birthday);
        Assert.Equal("(435) 555-0100", row.Phone);
        Assert.Equal("m.ashdown@example.com", row.Email);
    }

    [Fact]
    public void Reads_nothing_into_the_fields_this_report_does_not_print()
    {
        // There is no Unit column and no Address column at all: the report covers one
        // ward, named once above the table where no column can reach it.
        Assert.All(Report().Rows, r =>
        {
            Assert.Equal("", r.Unit);
            Assert.Equal("", r.Address);
        });
    }

    [Fact]
    public void Rejoins_a_name_wrapped_over_two_lines()
    {
        var row = Report().Rows[4];
        Assert.Equal("Featherstonehaugh, Wilhelmina", row.Name);
        Assert.Equal("20 Aug", row.Birthday);
        Assert.Equal("(801) 555-0155", row.Phone);
        Assert.Equal("w.feather@example.com", row.Email);
    }

    [Fact]
    public void Rejoins_a_wrapped_e_mail_with_nothing_between_the_halves()
    {
        // A space here would make the address undeliverable, and it would look fine
        // on screen right up until the send failed.
        Assert.Equal("k.yelverton@averylongdomainname.example", Report().Rows[6].Email);
    }

    [Fact]
    public void Leaves_a_missing_field_empty_rather_than_borrowing_the_next_column()
    {
        var rows = Report().Rows;
        Assert.Equal("", rows[2].Email);
        Assert.Equal("555-0133", rows[2].Phone);
        Assert.Equal("", rows[3].Phone);
        Assert.Equal("v.winslade@example.com", rows[3].Email);
    }

    [Fact]
    public void Reads_the_full_birth_date_the_report_prints_on_some_rows()
    {
        // Most rows carry a day and a month. A few carry the year as well, with an age
        // beside them; both have to survive the read.
        var row = Report().Rows[1];
        Assert.Equal("9 Feb 1982", row.Birthday);
        Assert.Equal("44", row.Age);

        var (month, day) = LcrNormalizer.ParseBirthday(row.Birthday);
        Assert.Equal(2, month);
        Assert.Equal(9, day);
    }

    [Fact]
    public void The_age_it_reads_is_not_one_it_may_write()
    {
        // The column is real — some rows fill it — but only 2 of the 160 in the export
        // this was measured against. Carrying it would blank the rest.
        var report = Report();
        Assert.Contains(report.Rows, r => r.Age.Length > 0);
        Assert.False(report.Source.Carry(ReportFields.Age));
        Assert.False(report.Source.Carry(ReportFields.Unit));
        Assert.False(report.Source.Carry(ReportFields.Address));
        Assert.True(report.Source.Carry(ReportFields.Birthday));
        Assert.True(report.Source.Carry(ReportFields.Email));
        Assert.True(report.Source.Carry(ReportFields.Phone));
    }
}
