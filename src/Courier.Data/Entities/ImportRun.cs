namespace Courier.Data.Entities;

/// <summary>One applied import, kept so every change can be traced to a file.</summary>
public class ImportRun
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTimeOffset ImportedAt { get; set; } = DateTimeOffset.UtcNow;

    public required string FileName { get; set; }

    /// <summary>SHA-256 of the PDF, so importing the same file twice is obvious.</summary>
    public required string Sha256 { get; set; }

    public int PageCount { get; set; }
    public int RowCount { get; set; }
    public int AddedCount { get; set; }
    public int UpdatedCount { get; set; }
    public int DeactivatedCount { get; set; }
    public int ReactivatedCount { get; set; }
    public int UnchangedCount { get; set; }

    public List<PersonChange> Changes { get; set; } = [];
}

public enum ChangeKind { Added = 1, Updated = 2, Deactivated = 3, Reactivated = 4 }

/// <summary>A single field-level change, which is what the import review screen shows.</summary>
public class PersonChange
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ImportRunId { get; set; }
    public ImportRun? ImportRun { get; set; }

    public Guid PersonId { get; set; }
    public Person? Person { get; set; }

    public ChangeKind Kind { get; set; }
    public string? Field { get; set; }
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
}
