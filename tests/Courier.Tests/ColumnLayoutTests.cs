using Courier.Core.Import;

namespace Courier.Tests;

public sealed class ColumnLayoutTests
{
    // The Organizations and Callings positions, which are the ones that break a
    // layout built from a fixed field order: phone and e-mail are the other way
    // round, and there is a Gender column with nothing behind it.
    private static ColumnLayout Layout() =>
        ColumnLayout.From([
            (54.1, LcrField.Name), (152.4, null), (215.6, LcrField.Age),
            (240.6, LcrField.Birthday), (282.6, LcrField.Phone),
            (343.2, LcrField.Email), (480.3, LcrField.Unit),
        ])!;

    [Fact]
    public void Reads_each_field_from_its_own_column()
    {
        var lines = PdfLines.Of(8.0,
            (55.6, 600, "Ashdown, Marigold"), (153.9, 600, "F"), (242.1, 600, "17 Jan"),
            (284.1, 600, "(435) 555-0100"), (344.7, 600, "m.ashdown@example.com"),
            (481.8, 600, "Manti 2nd Ward"));
        var layout = Layout();

        Assert.Equal("Ashdown, Marigold", layout.Cell(lines, LcrField.Name));
        Assert.Equal("17 Jan", layout.Cell(lines, LcrField.Birthday));
        Assert.Equal("(435) 555-0100", layout.Cell(lines, LcrField.Phone));
        Assert.Equal("m.ashdown@example.com", layout.Cell(lines, LcrField.Email));
        Assert.Equal("Manti 2nd Ward", layout.Cell(lines, LcrField.Unit));
    }

    [Fact]
    public void Discards_a_column_that_has_no_field_behind_it()
    {
        // Gender is detected only so that its edge exists. Without that edge the
        // "F" joins the name and every person reads "Ashdown, Marigold F".
        var lines = PdfLines.Of(8.0, (55.6, 600, "Ashdown, Marigold"), (153.9, 600, "F"));
        Assert.Equal("Ashdown, Marigold", Layout().Cell(lines, LcrField.Name));
    }

    [Fact]
    public void Leaves_a_field_empty_rather_than_borrowing_the_next_column()
    {
        var lines = PdfLines.Of(8.0,
            (55.6, 600, "Brackenbury, Tavish"), (153.9, 600, "M"), (242.1, 600, "3 Jul"),
            (481.8, 600, "Manti 6th Ward"));
        var layout = Layout();

        Assert.Equal("", layout.Cell(lines, LcrField.Phone));
        Assert.Equal("", layout.Cell(lines, LcrField.Email));
        Assert.Equal("Manti 6th Ward", layout.Cell(lines, LcrField.Unit));
    }

    [Fact]
    public void Rejoins_a_cell_that_wraps_over_several_lines()
    {
        // A wrapped name sits 4.5pt either side of the row, so its two lines are
        // separate TextLines that both belong to the Name column.
        var lines = PdfLines.Of(8.0,
            (55.6, 604.5, "Featherstonehaugh,"), (153.9, 600, "F"), (55.6, 595.5, "Wilhelmina"));
        Assert.Equal("Featherstonehaugh, Wilhelmina", Layout().Cell(lines, LcrField.Name));
    }

    [Fact]
    public void Answers_for_a_field_the_layout_does_not_have()
    {
        var layout = Layout();
        Assert.False(layout.Has(LcrField.Address));
        Assert.True(layout.Has(LcrField.Age));
        Assert.Equal("", layout.Cell(PdfLines.Of(8.0, (55.6, 600, "Ashdown, Marigold")), LcrField.Address));
    }

    [Fact]
    public void Refuses_columns_that_do_not_run_left_to_right()
    {
        // A heading matched somewhere it does not belong shows up as an out-of-order
        // position. Rejecting the layout is what stops four fields folding into one.
        Assert.Null(ColumnLayout.From([
            (225.6, LcrField.Name), (152.4, null), (480.3, LcrField.Unit)]));
    }

    [Fact]
    public void Refuses_an_empty_layout() => Assert.Null(ColumnLayout.From([]));
}
