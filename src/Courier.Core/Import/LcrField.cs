namespace Courier.Core.Import;

/// <summary>A field a report can print. Gender is deliberately absent: the
/// Organizations and Callings report prints it, but Courier has no use for it, so it
/// is found as a column boundary and its contents discarded. Name has no entry in
/// <see cref="ReportFields"/> because a report without names is not a report.</summary>
public enum LcrField { Name, Email, Phone, Unit, Age, Birthday, Address }

/// <summary>The fields a report prints, and therefore the only fields an import from
/// it may overwrite. A report with no Address column must not blank the addresses
/// already recorded — they came from a report that did print them.</summary>
[Flags]
public enum ReportFields
{
    None = 0,
    Unit = 1,
    Age = 2,
    Birthday = 4,
    Address = 8,
    Email = 16,
    Phone = 32,
    All = Unit | Age | Birthday | Address | Email | Phone,
}

/// <summary>Where a set of rows came from: what the report is called, in LCR's own
/// words, and what it prints. Both travel with the report into the plan and into the
/// write, because the screen and the database have to agree.</summary>
public sealed record ReportSource(string FormatName, ReportFields Carries)
{
    public bool Carry(ReportFields field) => (Carries & field) != 0;
}
