namespace Courier.Core.Import;

/// <summary>A person already in the database, reduced to the fields an import can
/// touch. Preferred channel and locally-added contact details are deliberately
/// absent: an import must never be able to change them.</summary>
public sealed record ExistingPerson(
    Guid Id,
    string LastName,
    string FirstName,
    int? BirthMonth,
    int? BirthDay,
    string? Ward,
    int? Age,
    string? Address,
    string? Email,
    string? Phone,
    bool IsActive)
{
    /// <summary>True for somebody added by hand, who will never be in an export. Their
    /// absence from one says nothing, so an import must leave them alone.</summary>
    public bool AddedByHand { get; init; }
}

public sealed record FieldChange(string Field, string? From, string? To);

public sealed record PersonUpdate(
    ExistingPerson Existing, NormalizedPerson Incoming, IReadOnlyList<FieldChange> Changes);

public sealed record PersonReturn(ExistingPerson Existing, NormalizedPerson Incoming);

/// <summary>What an import would do, worked out before anything is written.</summary>
public sealed record ImportPlan(
    IReadOnlyList<NormalizedPerson> Added,
    IReadOnlyList<PersonUpdate> Updated,
    IReadOnlyList<ExistingPerson> Deactivated,
    IReadOnlyList<PersonReturn> Reactivated,
    int Unchanged,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<string> Notes)
{
    public int TotalInFile => Added.Count + Updated.Count + Reactivated.Count + Unchanged;
    public bool HasWarnings => Warnings.Count > 0;

    /// <summary>Notes are not warnings. Nothing is wrong; the user is being told
    /// something about the report they chose.</summary>
    public bool HasNotes => Notes.Count > 0;
}
