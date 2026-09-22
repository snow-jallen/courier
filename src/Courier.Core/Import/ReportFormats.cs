namespace Courier.Core.Import;

/// <summary>The reports Courier can read, tried in this order.
///
/// Order is load-bearing, and Member List is why. It prints Name, Gender, Age, Birth
/// Date, Phone Number and Email — every one of which the Organizations and Callings
/// members table also prints, under the very same words. Its heading is a subset of
/// that report's, so offered an Organizations and Callings file first it would claim
/// it and read every ward as blank. It is registered last so that it never gets the
/// chance. Nothing else here overlaps: Single Adults requires an Address column,
/// which neither of the others has, and Organizations and Callings requires a
/// Current Unit column, which neither of the others has.
///
/// The rule is first match wins, and the order below is pinned by a test. A new
/// descriptor goes after every report whose heading it is a subset of.</summary>
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

    /// <summary>The Member List report, exported for a single unit. Text is 9.0pt; the
    /// lines of a wrapped cell sit 6.1pt either side of the row's own baseline and
    /// consecutive rows sit 19.0pt apart, so 1.4 puts the threshold at 12.6pt — 2.1x
    /// the widest gap inside a row and 0.7x the narrowest gap between two.
    ///
    /// It prints no unit column, because it is already a report about one unit: the
    /// ward is named once, in the page header above the table, where no column can
    /// reach it. Unit is therefore left unmapped and out of Carries, so importing a
    /// Member List says nothing about anyone's ward rather than blanking it.
    ///
    /// Age is mapped and left out of Carries, for a different reason than Organizations
    /// and Callings. That report never fills its Age column at all; this one does, but
    /// only on the rows where it also prints a full birth date with its year — 2 rows
    /// of 160 in the export this was measured against. Carrying it would blank 158
    /// recorded ages to honour 2. A value is still read, so it reaches the plan the day
    /// the report starts printing them.</summary>
    public static IReportFormat MemberList { get; } = new ColumnLayoutFormat(
        name: "Member List",
        columns:
        [
            new("Name", LcrField.Name),
            new("Gender", null),          // found for its edge; nothing stores it
            new("Age", LcrField.Age),
            new("Birth Date", LcrField.Birthday),
            new("Phone Number", LcrField.Phone),
            new("Email", LcrField.Email),
        ],
        rowBreakFactor: 1.4,
        carries: ReportFields.Birthday | ReportFields.Email | ReportFields.Phone,
        furniture: ["For Church Use Only", "Count:"]);

    public static IReadOnlyList<IReportFormat> Known { get; } =
        [SingleAdults, OrganizationsAndCallings, MemberList];
}
