using Courier.Core.Import;

namespace Courier.Tests;

public sealed class CallingsReportParserTests
{
    private static LcrReport Report()
    {
        using var stream = new MemoryStream(SyntheticCallingsReport.Build());
        return LcrReportParser.Parse(stream, "print.pdf");
    }

    [Fact]
    public void Recognises_the_report_it_is()
    {
        var report = Report();
        Assert.Equal("Organizations and Callings", report.Source.FormatName);
        Assert.Equal(2, report.PageCount);
    }

    [Fact]
    public void Reads_one_row_per_person_across_both_pages()
    {
        // The report's own footer says Count: 7. Reading exactly that many back is
        // the strongest check available that no row was dropped or invented.
        var rows = Report().Rows;
        Assert.Equal(7, rows.Count);
        Assert.Equal(
            ["Ashdown, Marigold", "Quilley, Barnaby", "Crowther, Dell",
             "Featherstonehaugh, Wilhelmina", "Zelmore, Briony",
             "Wraithwell, Mordecai", "Yelverton, Katriona"],
            rows.Select(r => r.Name));
    }

    [Fact]
    public void Leaves_out_the_callings_table_printed_above_the_members()
    {
        // "Pilkington, Hattie" is a stake single adult representative, printed in a
        // different table with different columns and no contact details.
        Assert.DoesNotContain(Report().Rows, r => r.Name.Contains("Pilkington", StringComparison.Ordinal));
    }

    [Fact]
    public void Keeps_each_field_in_its_own_column()
    {
        var row = Report().Rows[0];
        Assert.Equal("Ashdown, Marigold", row.Name);   // the Gender glyph does not join it
        Assert.Equal("17 Jan", row.Birthday);
        Assert.Equal("(435) 555-0100", row.Phone);
        Assert.Equal("m.ashdown@example.com", row.Email);
        Assert.Equal("Manti 2nd Ward", row.Unit);
    }

    [Fact]
    public void Reads_nothing_into_the_fields_this_report_does_not_print()
    {
        Assert.All(Report().Rows, r =>
        {
            Assert.Equal("", r.Age);       // the column is printed and never filled
            Assert.Equal("", r.Address);   // there is no column at all
        });
    }

    [Fact]
    public void Rejoins_a_name_wrapped_over_two_lines()
    {
        var row = Report().Rows[3];
        Assert.Equal("Featherstonehaugh, Wilhelmina", row.Name);
        Assert.Equal("3 Jul", row.Birthday);
        Assert.Equal("Manti 10th Ward", row.Unit);
    }

    [Fact]
    public void Leaves_a_missing_field_empty_rather_than_borrowing_the_next_column()
    {
        var rows = Report().Rows;
        Assert.Equal("", rows[1].Email);
        Assert.Equal("555-0127", rows[1].Phone);
        Assert.Equal("", rows[2].Phone);
        Assert.Equal("d.crowther@example.com", rows[2].Email);
    }

    [Fact]
    public void Says_it_does_not_print_addresses_or_ages()
    {
        var carries = Report().Source;
        Assert.False(carries.Carry(ReportFields.Address));
        Assert.False(carries.Carry(ReportFields.Age));
        Assert.True(carries.Carry(ReportFields.Birthday));
        Assert.True(carries.Carry(ReportFields.Email));
        Assert.True(carries.Carry(ReportFields.Phone));
        Assert.True(carries.Carry(ReportFields.Unit));
    }

    [Fact]
    public void Normalises_into_people_the_rest_of_the_app_can_use()
    {
        var people = Report().Rows.Select(r => LcrNormalizer.Normalize(r)).ToList();

        Assert.All(people, p => Assert.NotNull(p.Ward));
        Assert.All(people, p => Assert.NotNull(p.BirthMonth));
        Assert.All(people.Where(p => p.PhoneE164 is not null),
            p => Assert.Matches(@"^\+1\d{10}$", p.PhoneE164!));
        Assert.All(people, p => Assert.Null(p.Address));
        Assert.All(people, p => Assert.Null(p.Age));
    }
}
