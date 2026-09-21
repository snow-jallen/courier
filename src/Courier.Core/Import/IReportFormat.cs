namespace Courier.Core.Import;

/// <summary>One column of a report. <paramref name="Headings"/> are the phrases that
/// name it in the report's own heading row, tried longest first so that "Individual
/// E-mail" wins over a bare "Individual". A null <paramref name="Field"/> means the
/// column is found only so that its edge exists — whatever is printed in it is
/// discarded.</summary>
public sealed record ColumnSpec(IReadOnlyList<string> Headings, LcrField? Field)
{
    public ColumnSpec(string heading, LcrField? field) : this([heading], field) { }
}

/// <summary>One report Courier knows how to read.
///
/// Everything here is a fact about a particular report rather than about PDFs, which
/// is why it is a strategy: the vertical rhythm that separates its rows, the words
/// that name its columns, the lines that belong to the page rather than to a person,
/// and which fields it actually prints.</summary>
public interface IReportFormat
{
    /// <summary>What to call this report on screen, in LCR's own words.</summary>
    string Name { get; }

    /// <summary>A vertical gap wider than this many times the text size starts a new
    /// row. A property of the report's layout, not of the page: a relative rule
    /// cannot tell a page of single-line rows apart from one tall row.</summary>
    double RowBreakFactor { get; }

    /// <summary>The fields this report prints, and therefore the only fields an
    /// import from it may overwrite.</summary>
    ReportFields Carries { get; }

    /// <summary>The column positions, if this group of lines is this report's heading
    /// row. Null for any other group, which is every group on a body page. The caller
    /// does the grouping, at this format's own <see cref="RowBreakFactor"/>.</summary>
    ColumnLayout? Detect(IReadOnlyList<TextLine> group);

    /// <summary>True for a line belonging to the page rather than to a person.</summary>
    bool IsFurniture(string lineText);
}
