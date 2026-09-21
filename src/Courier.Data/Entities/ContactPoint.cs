namespace Courier.Data.Entities;

public enum ContactKind { Email = 1, Phone = 2 }

public enum ContactSource
{
    /// <summary>Came from an LCR export.</summary>
    Lcr = 1,

    /// <summary>Someone typed it into Courier. These are what the
    /// "to enter in LCR" report lists.</summary>
    Local = 2,
}

/// <summary>One way of reaching a person. A person can have several: the number LCR
/// prints, plus a mobile they gave you at an activity.</summary>
public class ContactPoint
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PersonId { get; set; }
    public Person? Person { get; set; }

    public ContactKind Kind { get; set; }
    public ContactSource Source { get; set; }

    /// <summary>As written by whoever supplied it.</summary>
    public required string Value { get; set; }

    /// <summary>E.164 for a phone, lower-cased for an e-mail. What sending and
    /// duplicate-checking use.</summary>
    public string? Normalized { get; set; }

    /// <summary>True when the area code had to be assumed because the report printed
    /// only seven digits. The app asks before using one of these.</summary>
    public bool AreaCodeAssumed { get; set; }

    /// <summary>"mobile", "home", "work" — free text, optional.</summary>
    public string? Label { get; set; }

    /// <summary>The one Courier uses when sending on this channel.</summary>
    public bool IsPreferred { get; set; }

    public DateOnly AddedOn { get; set; }

    /// <summary>Last import that still listed this value. Null for anything added
    /// locally that LCR has never had.</summary>
    public DateOnly? LastSeenInLcrOn { get; set; }

    /// <summary>Ticked off once someone has typed this into LCR by hand. Clears it
    /// from the "to enter in LCR" report without deleting anything.</summary>
    public DateOnly? EnteredInLcrOn { get; set; }

    /// <summary>Outstanding work for the "to enter in LCR" report: supplied locally,
    /// never seen in an export, not yet marked as entered.</summary>
    public bool NeedsEnteringInLcr =>
        Source == ContactSource.Local && LastSeenInLcrOn is null && EnteredInLcrOn is null;
}
