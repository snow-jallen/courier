namespace Courier.Core.Import;

/// <summary>One person exactly as the report printed them, before any cleaning.
/// Kept verbatim so an import can always be traced back to what the PDF said.</summary>
public sealed record LcrRow(
    string Name,
    string Email,
    string Phone,
    string Unit,
    string Age,
    string Birthday,
    string Address,
    int Page)
{
    public bool LooksLikeAPerson => Name.Contains(',', StringComparison.Ordinal);
}
