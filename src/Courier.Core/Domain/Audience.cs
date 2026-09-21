namespace Courier.Core.Domain;

/// <summary>What pressing Send would actually do: how many go out on each channel,
/// and who would get nothing. Shown above the button, so the count on the button and
/// the addresses used by the send come from this one calculation.</summary>
public sealed record AudienceSummary(
    IReadOnlyList<Recipient> Reachable,
    IReadOnlyList<(Recipient Person, Reachability Why)> Unreachable,
    IReadOnlyDictionary<Channel, int> ByChannel)
{
    public int Chosen => Reachable.Count + Unreachable.Count;
    public int WillReceive => Reachable.Count;
    public int Count(Channel channel) => ByChannel.TryGetValue(channel, out var n) ? n : 0;
}

public static class Audience
{
    /// <summary>Narrows the directory and orders it.</summary>
    public static IReadOnlyList<Recipient> Select(
        IEnumerable<Recipient> people, AudienceFilter? filter = null, AudienceSort? sort = null)
        => Sort(people.Where((filter ?? AudienceFilter.Everyone).Matches), sort ?? AudienceSort.ByName);

    /// <summary>Orders a list, keeping people whose sort value is unknown at the
    /// bottom in both directions — "missing" is neither a small value nor a large one,
    /// and someone looking for the oldest person should not have to scroll past
    /// everyone whose age the report never printed. Those stragglers are ordered by
    /// name so the bottom of the list is still something you can read.</summary>
    public static IReadOnlyList<Recipient> Sort(IEnumerable<Recipient> people, AudienceSort sort)
    {
        var all = people.ToList();
        var known = all.Where(p => KeyOf(p, sort.Key) is not null);
        var unknown = all.Where(p => KeyOf(p, sort.Key) is null).OrderBy(p => p.SortName, Names);

        var ordered = sort.Descending
            ? known.OrderByDescending(p => KeyOf(p, sort.Key)!.Value).ThenBy(p => p.SortName, Names)
            : known.OrderBy(p => KeyOf(p, sort.Key)!.Value).ThenBy(p => p.SortName, Names);

        return [.. ordered, .. unknown];
    }

    /// <summary>What sending to these people would do. <paramref name="via"/> overrides
    /// everyone's preference for this one send; null honours what each person chose.</summary>
    public static AudienceSummary Summarise(IEnumerable<Recipient> chosen, Channel? via = null)
    {
        var reachable = new List<Recipient>();
        var unreachable = new List<(Recipient, Reachability)>();
        var byChannel = new Dictionary<Channel, int>();

        foreach (var person in chosen)
        {
            var reach = via is { } channel ? person.ReachabilityVia(channel) : person.Reachability;
            if (!reach.CanReceive) { unreachable.Add((person, reach)); continue; }
            reachable.Add(person);
            byChannel[reach.Channel] = byChannel.GetValueOrDefault(reach.Channel) + 1;
        }

        return new AudienceSummary(reachable, unreachable, byChannel);
    }

    private static readonly StringComparer Names = StringComparer.OrdinalIgnoreCase;

    /// <summary>A comparable value for the sort, or null when this person has none.</summary>
    private static long? KeyOf(Recipient person, AudienceSortKey key) => key switch
    {
        // Name always sorts; the ordering itself is done by the string comparer.
        AudienceSortKey.Name => 0,

        // The canonical ward list is already in the order people say them in, so its
        // index is the natural order — "Manti 2nd" before "Manti 10th", which no
        // lexicographic comparison gives you.
        AudienceSortKey.Ward => person.Ward is null ? null : WardRank(person.Ward),

        AudienceSortKey.Age => person.Age,

        // No year is printed, so a birthday is a month and a day and nothing else.
        AudienceSortKey.Birthday => person.BirthMonth is int m && person.BirthDay is int d
            ? m * 100 + d
            : null,

        AudienceSortKey.Channel => person.PreferredChannel is Channel.None
            ? null
            : ChannelRank(person.PreferredChannel),

        _ => null,
    };

    private static long? WardRank(string ward)
    {
        var index = Wards.All.ToList().FindIndex(w => string.Equals(w, ward, StringComparison.OrdinalIgnoreCase));
        return index < 0 ? null : index;
    }

    private static long ChannelRank(Channel channel)
    {
        var index = Channels.Deliverable.ToList().IndexOf(channel);
        return index < 0 ? Channels.Deliverable.Count : index;
    }
}
