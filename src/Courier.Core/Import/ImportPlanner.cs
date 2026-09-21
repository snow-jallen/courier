namespace Courier.Core.Import;

/// <summary>Works out what a report would change, without changing anything.
///
/// LCR gives no stable identifier, so people are matched on name plus birthday,
/// then on name alone for anyone still unmatched. Birthday is the strongest signal
/// available: it is printed for nearly everyone and does not change.
/// </summary>
public static class ImportPlanner
{
    /// <summary>Deactivating more than this share of the directory in one import
    /// usually means a partial export, not an emptying ward.</summary>
    private const double DeactivationAlarmRatio = 0.10;

    public static ImportPlan Plan(IReadOnlyList<NormalizedPerson> incoming, IReadOnlyList<ExistingPerson> existing)
    {
        var unmatched = existing.ToList();
        var added = new List<NormalizedPerson>();
        var updated = new List<PersonUpdate>();
        var reactivated = new List<PersonReturn>();
        var unchanged = 0;

        var stillIncoming = new List<NormalizedPerson>();

        // Pass one: name and birthday together.
        foreach (var person in incoming)
        {
            var match = unmatched.FirstOrDefault(e =>
                SameName(e, person) && e.BirthMonth == person.BirthMonth && e.BirthDay == person.BirthDay);
            if (match is null) { stillIncoming.Add(person); continue; }
            unmatched.Remove(match);
            Record(match, person);
        }

        // Pass two: name alone, for a corrected or newly-filled birthday.
        foreach (var person in stillIncoming)
        {
            var match = unmatched.FirstOrDefault(e => SameName(e, person));
            if (match is null) { added.Add(person); continue; }
            unmatched.Remove(match);
            Record(match, person);
        }

        void Record(ExistingPerson match, NormalizedPerson person)
        {
            var changes = Diff(match, person);
            if (!match.IsActive) reactivated.Add(new PersonReturn(match, person));
            else if (changes.Count > 0) updated.Add(new PersonUpdate(match, person, changes));
            else unchanged++;
        }

        var deactivated = unmatched.Where(e => e.IsActive).ToList();

        var warnings = new List<string>();
        var activeBefore = existing.Count(e => e.IsActive);
        if (activeBefore > 0 && deactivated.Count > activeBefore * DeactivationAlarmRatio)
            warnings.Add(
                $"This import would mark {deactivated.Count} of {activeBefore} people inactive. " +
                "That usually means the export is incomplete. Check it covers every ward before applying.");
        if (incoming.Count == 0)
            warnings.Add("No people were found in this file.");
        foreach (var p in incoming.Where(p => p.Ward is null).Take(5))
            warnings.Add($"{p.SortName} has an unrecognised unit ('{p.RawUnit}') and will be filed without a ward.");

        return new ImportPlan(added, updated, deactivated, reactivated, unchanged, warnings);
    }

    private static bool SameName(ExistingPerson e, NormalizedPerson p) =>
        string.Equals(e.LastName, p.LastName, StringComparison.OrdinalIgnoreCase)
        && string.Equals(e.FirstName, p.FirstName, StringComparison.OrdinalIgnoreCase);

    private static IReadOnlyList<FieldChange> Diff(ExistingPerson e, NormalizedPerson p)
    {
        var changes = new List<FieldChange>();
        void Compare(string field, string? from, string? to)
        {
            if (!string.Equals(from ?? "", to ?? "", StringComparison.OrdinalIgnoreCase))
                changes.Add(new FieldChange(field, from, to));
        }

        Compare("Ward", e.Ward, p.Ward);
        Compare("Age", e.Age?.ToString(), p.Age?.ToString());
        Compare("Birthday", Birthday(e.BirthMonth, e.BirthDay), Birthday(p.BirthMonth, p.BirthDay));
        Compare("Address", e.Address, p.Address);
        Compare("Email", e.Email, p.Email);
        Compare("Phone", e.Phone, p.PhoneRaw);
        return changes;
    }

    private static string? Birthday(int? month, int? day) =>
        month is null || day is null ? null : $"{day} {Months[month.Value - 1]}";

    private static readonly string[] Months =
        ["Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec"];
}
