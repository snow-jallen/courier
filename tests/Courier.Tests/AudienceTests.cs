using Courier.Core.Domain;

namespace Courier.Tests;

public sealed class AudienceTests
{
    private static Recipient Person(
        string last, string first, string? ward = "Manti 2nd Ward", int? age = 40,
        Channel channel = Channel.Email, string? email = "someone@example.com",
        string? phone = "+14355550100", int? month = 3, int? day = 4, bool active = true) =>
        new(Guid.NewGuid(), last, first, $"{last}, {first}", ward, age, month, day,
            channel, email, phone, active);

    private static IReadOnlyList<string> Names(IEnumerable<Recipient> people) =>
        people.Select(p => p.LastName).ToList();

    // ---- filtering ------------------------------------------------------------

    [Fact]
    public void Everyone_keeps_everyone_who_is_still_in_the_directory()
    {
        var people = new[] { Person("Ashby", "Miriam"), Person("Gone", "Alvin", active: false) };
        Assert.Equal(["Ashby"], Names(Audience.Select(people)));
    }

    [Fact]
    public void Search_looks_at_the_name_the_email_and_the_phone()
    {
        var people = new[]
        {
            Person("Ashby", "Miriam", email: "m.ashby@example.com"),
            Person("Quilley", "Barnaby", email: "barnaby@elsewhere.org"),
        };
        Assert.Equal(["Ashby"], Names(Audience.Select(people, new AudienceFilter { Search = "miriam" })));
        Assert.Equal(["Quilley"], Names(Audience.Select(people, new AudienceFilter { Search = "elsewhere" })));
    }

    [Fact]
    public void Search_finds_a_number_typed_the_way_the_report_prints_it()
    {
        var people = new[] { Person("Winslade", "Verity", phone: "+14355550199") };
        Assert.Single(Audience.Select(people, new AudienceFilter { Search = "555-0199" }));
        Assert.Single(Audience.Select(people, new AudienceFilter { Search = "(435) 555 0199" }));
    }

    [Fact]
    public void An_age_range_leaves_out_anyone_whose_age_was_never_printed()
    {
        var people = new[]
        {
            Person("Lathrop", "A", age: 30), Person("Old", "B", age: 70), Person("Unknown", "C", age: null),
        };
        var kept = Audience.Select(people, new AudienceFilter { MinAge = 25, MaxAge = 50 });
        Assert.Equal(["Lathrop"], Names(kept));
    }

    [Fact]
    public void An_age_bound_is_inclusive()
    {
        var people = new[] { Person("Edge", "A", age: 50) };
        Assert.Single(Audience.Select(people, new AudienceFilter { MaxAge = 50 }));
        Assert.Single(Audience.Select(people, new AudienceFilter { MinAge = 50 }));
    }

    [Fact]
    public void Any_channel_and_no_channel_chosen_are_different_questions()
    {
        var people = new[]
        {
            Person("Chose", "A", channel: Channel.Text),
            Person("Never", "B", channel: Channel.None),
        };

        Assert.Equal(2, Audience.Select(people, new AudienceFilter { Channel = ChannelFilter.Any }).Count);
        Assert.Equal(["Never"], Names(Audience.Select(people, new AudienceFilter { Channel = ChannelFilter.NoneChosen })));
        Assert.Equal(["Chose"], Names(Audience.Select(people, new AudienceFilter { Channel = ChannelFilter.Is(Channel.Text) })));
    }

    [Fact]
    public void A_default_filter_does_not_constrain_the_channel()
    {
        var people = new[] { Person("Never", "B", channel: Channel.None) };
        Assert.Single(Audience.Select(people, new AudienceFilter()));
    }

    [Fact]
    public void Criteria_combine_with_and()
    {
        var people = new[]
        {
            Person("Both", "A", ward: "Sterling Ward", age: 40),
            Person("WardOnly", "B", ward: "Sterling Ward", age: 80),
            Person("AgeOnly", "C", ward: "Manti 4th Ward", age: 40),
        };
        var kept = Audience.Select(people, new AudienceFilter { Ward = "Sterling Ward", MaxAge = 50 });
        Assert.Equal(["Both"], Names(kept));
    }

    [Fact]
    public void Birthday_filtering_is_by_month_because_no_year_is_printed()
    {
        var people = new[] { Person("Sep", "A", month: 9, day: 4), Person("Mar", "B", month: 3, day: 4) };
        Assert.Equal(["Sep"], Names(Audience.Select(people, new AudienceFilter { BirthdayMonth = 9 })));
    }

    [Fact]
    public void Inactive_people_can_be_asked_for_deliberately()
    {
        var people = new[] { Person("Here", "A"), Person("Gone", "B", active: false) };
        Assert.Equal(2, Audience.Select(people, new AudienceFilter { ActiveOnly = false }).Count);
    }

    // ---- sorting --------------------------------------------------------------

    [Fact]
    public void Wards_sort_the_way_people_say_them_not_alphabetically()
    {
        var people = new[]
        {
            Person("Tenth", "A", ward: "Manti 10th Ward"),
            Person("Second", "B", ward: "Manti 2nd Ward"),
            Person("Sterling", "C", ward: "Sterling Ward"),
            Person("First", "D", ward: "Manti 1st Ward"),
        };
        Assert.Equal(["First", "Second", "Tenth", "Sterling"],
            Names(Audience.Sort(people, AudienceSort.ByWard)));
    }

    [Fact]
    public void Birthdays_sort_by_month_then_day()
    {
        var people = new[]
        {
            Person("Late", "A", month: 12, day: 1),
            Person("Early", "B", month: 1, day: 30),
            Person("Middle", "C", month: 1, day: 31),
        };
        Assert.Equal(["Early", "Middle", "Late"], Names(Audience.Sort(people, AudienceSort.ByBirthday)));
    }

    [Fact]
    public void Unknown_values_stay_at_the_bottom_whichever_way_the_sort_runs()
    {
        var people = new[]
        {
            Person("Unknown", "A", age: null),
            Person("Lathrop", "B", age: 30),
            Person("Old", "C", age: 80),
        };
        Assert.Equal(["Lathrop", "Old", "Unknown"], Names(Audience.Sort(people, AudienceSort.ByAge)));
        Assert.Equal(["Old", "Lathrop", "Unknown"], Names(Audience.Sort(people, AudienceSort.ByAge.Reversed())));
    }

    [Fact]
    public void People_with_the_same_sort_value_stay_in_name_order()
    {
        var people = new[]
        {
            Person("Wilson", "A", age: 40), Person("Ashgrove", "B", age: 40), Person("Møller", "C", age: 40),
        };
        Assert.Equal(["Ashgrove", "Møller", "Wilson"], Names(Audience.Sort(people, AudienceSort.ByAge)));
    }

    // ---- reachability ---------------------------------------------------------

    [Fact]
    public void Someone_who_prefers_email_without_an_email_cannot_be_reached()
    {
        var person = Person("Ashby", "Miriam", channel: Channel.Email, email: null);
        Assert.False(person.CanReceive);
        Assert.Equal(UnreachableReason.MissingAddress, person.Reachability.Reason);
    }

    [Fact]
    public void Someone_with_no_channel_chosen_says_so_rather_than_guessing_one()
    {
        var person = Person("Yardley", "Carma", channel: Channel.None);
        Assert.Equal(UnreachableReason.NoChannelChosen, person.Reachability.Reason);
        Assert.Null(person.Reachability.Address);
    }

    [Fact]
    public void A_channel_courier_cannot_send_on_yet_is_called_out()
    {
        var person = Person("Future", "A", channel: Channel.WhatsApp);
        Assert.Equal(UnreachableReason.ChannelNotSupported, person.Reachability.Reason);
    }

    [Fact]
    public void Text_and_voice_both_reach_a_person_at_their_phone()
    {
        Assert.Equal("+14355550100", Person("A", "B", channel: Channel.Text).Reachability.Address);
        Assert.Equal("+14355550100", Person("A", "B", channel: Channel.Voice).Reachability.Address);
    }

    // ---- the summary above the Send button -------------------------------------

    [Fact]
    public void The_summary_counts_each_channel_and_names_who_gets_nothing()
    {
        var people = new[]
        {
            Person("Mail1", "A", channel: Channel.Email),
            Person("Mail2", "B", channel: Channel.Email),
            Person("Text1", "C", channel: Channel.Text),
            Person("NoEmail", "D", channel: Channel.Email, email: null),
            Person("NoChannel", "E", channel: Channel.None),
        };

        var summary = Audience.Summarise(people);

        Assert.Equal(5, summary.Chosen);
        Assert.Equal(3, summary.WillReceive);
        Assert.Equal(2, summary.Count(Channel.Email));
        Assert.Equal(1, summary.Count(Channel.Text));
        Assert.Equal(0, summary.Count(Channel.Voice));
        Assert.Equal(["NoEmail", "NoChannel"], summary.Unreachable.Select(u => u.Person.LastName));
    }
}
