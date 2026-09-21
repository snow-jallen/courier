using Courier.Core.Domain;
using Courier.Core.Import;

namespace Courier.Tests;

public sealed class NormalizerTests
{
    [Theory]
    [InlineData("(435) 555-0142", "+14355550142", false)]
    [InlineData("435-555-0142", "+14355550142", false)]
    [InlineData("1 (435) 555-0142", "+14355550142", false)]
    [InlineData("555-0143", "+14355550143", true)]
    public void Turns_a_printed_number_into_one_that_can_be_dialled(string printed, string expected, bool assumed)
    {
        var (raw, e164, areaCodeAssumed) = LcrNormalizer.ParsePhone(printed);
        Assert.Equal(printed, raw);
        Assert.Equal(expected, e164);
        Assert.Equal(assumed, areaCodeAssumed);
    }

    [Fact]
    public void Keeps_an_unusable_number_rather_than_discarding_it()
    {
        var (raw, e164, _) = LcrNormalizer.ParsePhone("call the house");
        Assert.Equal("call the house", raw);
        Assert.Null(e164);
    }

    [Theory]
    [InlineData("17 Jan", 1, 17)]
    [InlineData("6 May", 5, 6)]
    [InlineData("31 Dec", 12, 31)]
    public void Reads_the_day_and_month_the_report_prints(string printed, int month, int day)
    {
        Assert.Equal((month, day), LcrNormalizer.ParseBirthday(printed));
    }

    [Theory]
    [InlineData("")]
    [InlineData("32 Jan")]
    [InlineData("17 Xyz")]
    public void Refuses_a_birthday_it_cannot_read(string printed)
    {
        Assert.Equal((null, null), LcrNormalizer.ParseBirthday(printed));
    }

    [Theory]
    [InlineData("Manti 5th Ward", "Manti 5th Ward")]
    [InlineData("5th", "Manti 5th Ward")]
    [InlineData("Manti 3rd", "Manti 3rd Ward")]
    [InlineData("Ward Manti 8th Ward", "Manti 8th Ward")]
    [InlineData("10th", "Manti 10th Ward")]
    [InlineData("Sterling", "Sterling Ward")]
    public void Snaps_a_wrapped_unit_back_onto_its_ward(string fragment, string expected)
    {
        Assert.Equal(expected, Wards.Snap(fragment));
    }

    [Theory]
    [InlineData("")]
    [InlineData("Ward")]
    [InlineData("Manti")]
    [InlineData("Provo 7th Ward")]
    public void Leaves_an_unrecognisable_unit_alone_rather_than_guessing(string fragment)
    {
        Assert.Null(Wards.Snap(fragment));
    }

    [Fact]
    public void Splits_the_printed_name_into_surname_and_given_name()
    {
        var person = LcrNormalizer.Normalize(
            new LcrRow("Marchbank, Imogen Rose", "", "", "Manti 2nd Ward", "85", "16 Dec", "", 1));
        Assert.Equal("Marchbank", person.LastName);
        Assert.Equal("Imogen Rose", person.FirstName);
        Assert.Equal("Marchbank, Imogen Rose", person.DisplayName);
    }
}

public sealed class PhoneFormatTests
{
    [Theory]
    [InlineData("+14355550100", "(435) 555-0100")]
    [InlineData("14355550100", "(435) 555-0100")]
    [InlineData("4355550100", "(435) 555-0100")]
    [InlineData("435-555-0100", "(435) 555-0100")]
    [InlineData("(435) 555-0100", "(435) 555-0100")]
    public void Numbers_are_shown_the_way_people_read_them(string stored, string shown)
    {
        Assert.Equal(shown, PhoneFormat.ForDisplay(stored));
    }

    [Theory]
    [InlineData("", "")]
    [InlineData(null, "")]
    [InlineData("+447700900123", "+447700900123")]
    [InlineData("call the house", "call the house")]
    [InlineData("555-0100", "555-0100")]
    public void Anything_else_is_left_exactly_as_it_is(string? stored, string shown)
    {
        Assert.Equal(shown, PhoneFormat.ForDisplay(stored));
    }
}
