namespace Courier.Core.Import;

/// <summary>The reports Courier can read, tried in this order.
///
/// The two descriptors here cannot claim each other's file: Single Adults requires an
/// Address column, which Organizations and Callings has none of, and Organizations
/// and Callings requires Gender and "Email", where the older report writes "E-mail".
/// Order therefore does not matter today. The rule is nevertheless first match wins,
/// so that a future overlapping descriptor is a decision rather than a surprise.</summary>
public static class ReportFormats
{
    /// <summary>The Single Adults report. Text is 8.9pt; lines wrapped inside one row
    /// sit 12.8pt apart (1.44x) and consecutive rows sit 23.2pt apart (2.6x), so 2.0
    /// falls squarely between them.</summary>
    public static IReportFormat SingleAdults { get; } = new ColumnLayoutFormat(
        name: "Single Adults",
        columns:
        [
            // "Preferred Name" wraps onto two lines, and the line carrying "Name" is
            // also the only one carrying "Phone".
            new(["Preferred Name", "Preferred", "Name"], LcrField.Name),
            new(["Individual E-mail"], LcrField.Email),
            new(["Phone"], LcrField.Phone),
            new(["Unit"], LcrField.Unit),
            new(["Age"], LcrField.Age),
            new(["Birthday"], LcrField.Birthday),
            new(["Address"], LcrField.Address),
        ],
        rowBreakFactor: 2.0,
        carries: ReportFields.All,
        furniture:
        [
            "lcr.churchofjesuschrist.org", "Single Adults", "Description", "Group by Unit",
            "Count:", "Preferred", "Individual", "Street 1", "(1 Jan)",
        ]);

    /// <summary>The Single Adult Members section of the Organizations and Callings
    /// report. Text is 8.0pt; the lines of a wrapped name sit 4.5pt apart (0.56x) and
    /// consecutive rows sit 13.4pt apart (1.68x), so 1.0 puts the threshold at 8.0pt —
    /// 1.8x the widest gap inside a row and 0.6x the narrowest gap between two.
    ///
    /// It prints an Age column and never fills it: empty on all 428 rows of the only
    /// export available. Age is mapped so that a value would be read if one ever
    /// appeared, and left out of Carries so that the emptiness cannot wipe an age
    /// recorded from the report that does print them.</summary>
    public static IReportFormat OrganizationsAndCallings { get; } = new ColumnLayoutFormat(
        name: "Organizations and Callings",
        columns:
        [
            new("Name", LcrField.Name),
            new("Gender", null),          // found for its edge; nothing stores it
            new("Age", LcrField.Age),
            new("Birth Date", LcrField.Birthday),
            new("Phone Number", LcrField.Phone),
            new("Email", LcrField.Email),
            new("Current Unit", LcrField.Unit),
        ],
        rowBreakFactor: 1.0,
        carries: ReportFields.Unit | ReportFields.Birthday | ReportFields.Email | ReportFields.Phone,
        furniture: ["For Church Use Only", "Count:"]);

    public static IReadOnlyList<IReportFormat> Known { get; } = [SingleAdults, OrganizationsAndCallings];
}
