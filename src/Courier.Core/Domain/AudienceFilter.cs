namespace Courier.Core.Domain;

/// <summary>Which preferred channel to keep.
///
/// A <c>Channel?</c> cannot say this. It would have to spell "any channel" as null and
/// "nobody has chosen one" as <see cref="Channel.None"/> — two different questions that
/// look alike at the call site, and whose default is the wrong one of the two. So the
/// question gets its own type, whose <c>default</c> is the harmless answer.</summary>
public readonly record struct ChannelFilter
{
    private ChannelFilter(Channel channel)
    {
        Constrains = true;
        Channel = channel;
    }

    /// <summary>False when this filter keeps everyone, whatever their channel.</summary>
    public bool Constrains { get; }

    /// <summary>Only meaningful when <see cref="Constrains"/> is true.</summary>
    public Channel Channel { get; }

    public static ChannelFilter Any => default;

    public static ChannelFilter Is(Channel channel) => new(channel);

    /// <summary>People nobody has chosen a channel for yet — the list the user works
    /// through before a send, not a way of saying "no constraint".</summary>
    public static ChannelFilter NoneChosen => new(Channel.None);

    public bool Matches(Channel channel) => !Constrains || Channel == channel;
}

/// <summary>What the user has narrowed the directory down to. Every criterion is
/// optional and they all combine with AND; an absent one constrains nothing.
///
/// A criterion is a requirement, so an unknown value never satisfies one: someone
/// whose age the report never printed is not "45 and over", and is left out whenever
/// an age bound is set. The same goes for a missing birthday or ward.</summary>
public sealed record AudienceFilter
{
    /// <summary>No constraint beyond the standing one that people who have left the
    /// directory are not messaged.</summary>
    public static readonly AudienceFilter Everyone = new();

    /// <summary>Matched against the name, e-mail and phone, case-insensitively and
    /// anywhere in the value.</summary>
    public string? Search { get; init; }

    public string? Ward { get; init; }

    public ChannelFilter Channel { get; init; } = ChannelFilter.Any;

    /// <summary>Inclusive.</summary>
    public int? MinAge { get; init; }

    /// <summary>Inclusive.</summary>
    public int? MaxAge { get; init; }

    /// <summary>1-12. Birthdays have no year, so a month is as narrow as this gets.</summary>
    public int? BirthdayMonth { get; init; }

    /// <summary>On by default: someone who has fallen out of the export is still in
    /// the database, and sending to them is nearly always a mistake.</summary>
    public bool ActiveOnly { get; init; } = true;

    public bool Matches(Recipient person)
    {
        if (ActiveOnly && !person.IsActive) return false;
        if (!Channel.Matches(person.PreferredChannel)) return false;
        if (Ward is not null && !string.Equals(person.Ward, Ward, StringComparison.OrdinalIgnoreCase)) return false;
        if ((MinAge is not null || MaxAge is not null) && person.Age is null) return false;
        if (MinAge is int min && person.Age < min) return false;
        if (MaxAge is int max && person.Age > max) return false;
        if (BirthdayMonth is not null && person.BirthMonth != BirthdayMonth) return false;
        return MatchesSearch(person);
    }

    private bool MatchesSearch(Recipient person)
    {
        var term = Search?.Trim();
        if (string.IsNullOrEmpty(term)) return true;

        if (Has(person.DisplayName, term) || Has(person.SortName, term) || Has(person.FullName, term)
            || Has(person.Email, term) || Has(person.Phone, term))
            return true;

        // "555-0142" and "(435) 555 0142" both have to find +14355550142, because the
        // user is reading the number off the report, not off the database.
        var wanted = Digits(term);
        return wanted.Length >= 3 && Digits(person.Phone).Contains(wanted, StringComparison.Ordinal);
    }

    private static bool Has(string? value, string term) =>
        value is not null && value.Contains(term, StringComparison.OrdinalIgnoreCase);

    private static string Digits(string? value) =>
        new((value ?? "").Where(char.IsAsciiDigit).ToArray());
}

public enum AudienceSortKey
{
    Name = 0,
    Ward = 1,
    Age = 2,
    Birthday = 3,
    Channel = 4,
}

/// <summary>How the list is ordered. Unknowns — no ward, no age, no birthday, no
/// channel chosen — always sort to the bottom, in both directions: "missing" is not a
/// small value or a large one, and someone looking for the oldest person should not
/// have to scroll past everyone whose age was never printed. Only the known values
/// turn round when <see cref="Descending"/> is set.</summary>
public sealed record AudienceSort(AudienceSortKey Key, bool Descending = false)
{
    public static readonly AudienceSort ByName = new(AudienceSortKey.Name);
    public static readonly AudienceSort ByWard = new(AudienceSortKey.Ward);
    public static readonly AudienceSort ByAge = new(AudienceSortKey.Age);
    public static readonly AudienceSort ByBirthday = new(AudienceSortKey.Birthday);
    public static readonly AudienceSort ByChannel = new(AudienceSortKey.Channel);

    public AudienceSort Reversed() => this with { Descending = !Descending };
}
