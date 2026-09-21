using Courier.Core.Domain;

namespace Courier.Data.Entities;

public enum PersonSource
{
    /// <summary>Came from an LCR export.</summary>
    Lcr = 1,

    /// <summary>Added by hand. Never appears in an export, so an import must not treat
    /// their absence from one as having left.</summary>
    Local = 2,
}

public class Person
{
    public Guid Id { get; set; } = Guid.NewGuid();

    // --- Identity as LCR prints it -------------------------------------------------
    public required string LastName { get; set; }
    public required string FirstName { get; set; }

    /// <summary>The "Preferred Name" cell verbatim, e.g. "Ashgrove, Adelaide".</summary>
    public required string DisplayName { get; set; }

    // --- Fields an import owns. Overwritten on every import. ------------------------
    public string? Ward { get; set; }
    public int? Age { get; set; }
    public int? BirthMonth { get; set; }
    public int? BirthDay { get; set; }
    public string? Address { get; set; }

    /// <summary>The e-mail and phone as the last import found them. Contact details
    /// added locally live in <see cref="ContactPoints"/> and are never written here,
    /// so an import can replace these without touching anything a person typed in.</summary>
    public string? LcrEmail { get; set; }
    public string? LcrPhone { get; set; }

    // --- Fields Courier owns. An import never touches these. ------------------------
    public Channel PreferredChannel { get; set; } = Channel.None;
    public string? Notes { get; set; }

    /// <summary>Where this person came from. An import may only deactivate people it
    /// put there itself.</summary>
    public PersonSource Source { get; set; } = PersonSource.Lcr;

    // --- Soft delete ----------------------------------------------------------------
    /// <summary>False once someone stops appearing in the export. Rows are never
    /// deleted: history, preferences and past deliveries all hang off this person.</summary>
    public bool IsActive { get; set; } = true;
    public DateOnly? DeactivatedOn { get; set; }

    // --- Provenance -----------------------------------------------------------------
    public DateOnly FirstSeenOn { get; set; }
    public DateOnly LastSeenOn { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public List<ContactPoint> ContactPoints { get; set; } = [];
}
