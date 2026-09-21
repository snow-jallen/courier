namespace Courier.Core.Domain;

/// <summary>Why a message cannot reach someone. The list above the Send button shows
/// these rather than quietly sending to fewer people than it counted.</summary>
public enum UnreachableReason
{
    /// <summary>Nothing is wrong; the message can be delivered.</summary>
    None = 0,

    /// <summary>Nobody has chosen how to contact this person yet.</summary>
    NoChannelChosen = 1,

    /// <summary>A channel is chosen, but there is no address or number to use with it.</summary>
    MissingAddress = 2,

    /// <summary>A channel is chosen that Courier cannot deliver on yet.</summary>
    ChannelNotSupported = 3,

    /// <summary>The person has fallen out of the export. The address may still work,
    /// which is exactly why this has to be said out loud rather than assumed.</summary>
    NotInDirectory = 4,
}

/// <summary>Whether a message would reach one person, on which channel and at which
/// address — and when it would not, why. A bool would lose the reason, and the reason
/// is the only part the user can act on.</summary>
public sealed record Reachability(Channel Channel, string? Address, UnreachableReason Reason)
{
    public bool CanReceive => Reason is UnreachableReason.None;
}

/// <summary>One person as the send list shows them and as the sender consumes them.
/// Deliberately the same record for both: the count above the Send button and the
/// addresses actually used come from one calculation, so the screen cannot promise
/// something the send does not keep.</summary>
public sealed record Recipient(
    Guid Id,
    string LastName,
    string FirstName,
    string DisplayName,
    string? Ward,
    int? Age,
    int? BirthMonth,
    int? BirthDay,
    Channel PreferredChannel,
    string? Email,
    string? Phone,
    bool IsActive)
{
    /// <summary>"Ashgrove, Adelaide", as LCR prints it and as the list is ordered.</summary>
    public string SortName => $"{LastName}, {FirstName}";

    /// <summary>"Adelaide Ashgrove", for sentences written to the user.</summary>
    public string FullName =>
        string.IsNullOrWhiteSpace(FirstName) ? LastName.Trim() : $"{FirstName.Trim()} {LastName.Trim()}";

    /// <summary>The address this channel would use, or null when there is none.
    /// WhatsApp answers with the phone number even though Courier cannot send on it
    /// yet: whether a channel can be delivered is <see cref="Reachability"/>'s
    /// question, not this one's.</summary>
    public string? AddressFor(Channel channel) => channel switch
    {
        Channel.Email => Blank(Email),
        Channel.Text or Channel.Voice or Channel.WhatsApp => Blank(Phone),
        _ => null,
    };

    /// <summary>What sending to this person would do, worked out before the Send
    /// button is pressed. Being out of the directory is checked first: the address
    /// may be perfectly good, and that is the case most easily sent by mistake.</summary>
    public Reachability Reachability
    {
        get
        {
            if (!IsActive)
                return new Reachability(PreferredChannel, null, UnreachableReason.NotInDirectory);
            if (PreferredChannel is Channel.None)
                return new Reachability(Channel.None, null, UnreachableReason.NoChannelChosen);
            if (!Channels.Deliverable.Contains(PreferredChannel))
                return new Reachability(PreferredChannel, null, UnreachableReason.ChannelNotSupported);

            var address = AddressFor(PreferredChannel);
            return address is null
                ? new Reachability(PreferredChannel, null, UnreachableReason.MissingAddress)
                : new Reachability(PreferredChannel, address, UnreachableReason.None);
        }
    }

    public bool CanReceive => Reachability.CanReceive;

    private static string? Blank(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}
