using Courier.Core.Import;

namespace Courier.Tests;

public sealed class ImportPlannerTests
{
    private static NormalizedPerson Incoming(
        string last, string first, string? ward = "Manti 2nd Ward",
        string? email = null, string? phone = null, int month = 3, int day = 4) =>
        new(last, first, $"{last}, {first}", ward, 40, month, day, "1 Main",
            email, phone, phone is null ? null : "+14355550100", false, ward ?? "");

    private static ExistingPerson Existing(
        string last, string first, string? ward = "Manti 2nd Ward",
        string? email = null, string? phone = null, bool active = true, int month = 3, int day = 4) =>
        new(Guid.NewGuid(), last, first, month, day, ward, 40, "1 Main", email, phone, active);

    [Fact]
    public void Adds_someone_who_was_not_there_before()
    {
        var plan = ImportPlanner.Plan([Incoming("Ashby", "Miriam")], []);
        Assert.Single(plan.Added);
        Assert.Empty(plan.Updated);
        Assert.Empty(plan.Deactivated);
    }

    [Fact]
    public void Reports_the_fields_that_changed_and_nothing_else()
    {
        var plan = ImportPlanner.Plan(
            [Incoming("Winslade", "Verity", phone: "(435) 555-0199")],
            [Existing("Winslade", "Verity", phone: "(435) 555-0142")]);

        var update = Assert.Single(plan.Updated);
        var change = Assert.Single(update.Changes);
        Assert.Equal("Phone", change.Field);
        Assert.Equal("(435) 555-0142", change.From);
        Assert.Equal("(435) 555-0199", change.To);
    }

    [Fact]
    public void Counts_an_unchanged_person_as_unchanged()
    {
        var plan = ImportPlanner.Plan(
            [Incoming("Quilley", "Barnaby", email: "r@example.com")],
            [Existing("Quilley", "Barnaby", email: "r@example.com")]);

        Assert.Equal(1, plan.Unchanged);
        Assert.Empty(plan.Updated);
    }

    [Fact]
    public void Deactivates_someone_the_export_no_longer_lists()
    {
        var plan = ImportPlanner.Plan([], [Existing("Peacock", "Alvin")]);
        Assert.Single(plan.Deactivated);
        Assert.Empty(plan.Added);
    }

    [Fact]
    public void Brings_back_someone_who_reappears_rather_than_adding_them_twice()
    {
        var plan = ImportPlanner.Plan(
            [Incoming("Peacock", "Alvin")],
            [Existing("Peacock", "Alvin", active: false)]);

        Assert.Single(plan.Reactivated);
        Assert.Empty(plan.Added);
        Assert.Empty(plan.Deactivated);
    }

    [Fact]
    public void Matches_on_the_name_when_a_birthday_gets_filled_in()
    {
        var incoming = Incoming("Tuttle", "Marvin", month: 5, day: 14);
        var existing = new ExistingPerson(
            Guid.NewGuid(), "Tuttle", "Marvin", null, null, "Manti 2nd Ward", 40, "1 Main", null, null, true);

        var plan = ImportPlanner.Plan([incoming], [existing]);

        Assert.Empty(plan.Added);
        Assert.Equal("Birthday", Assert.Single(Assert.Single(plan.Updated).Changes).Field);
    }

    [Fact]
    public void Tells_two_people_of_the_same_name_apart_by_birthday()
    {
        var plan = ImportPlanner.Plan(
            [Incoming("Denholm", "John", month: 1, day: 12), Incoming("Denholm", "John", month: 9, day: 3)],
            [Existing("Denholm", "John", month: 1, day: 12), Existing("Denholm", "John", month: 9, day: 3)]);

        Assert.Equal(2, plan.Unchanged);
        Assert.Empty(plan.Added);
        Assert.Empty(plan.Deactivated);
    }

    [Fact]
    public void Warns_when_an_import_would_empty_out_the_directory()
    {
        var existing = Enumerable.Range(0, 100).Select(i => Existing($"Name{i}", "Test")).ToList();
        var incoming = existing.Take(50).Select(e => Incoming(e.LastName, e.FirstName)).ToList();

        var plan = ImportPlanner.Plan(incoming, existing);

        Assert.True(plan.HasWarnings);
        Assert.Contains(plan.Warnings, w => w.Contains("50 of 100", StringComparison.Ordinal));
    }

    [Fact]
    public void Does_not_warn_about_an_ordinary_import()
    {
        var existing = Enumerable.Range(0, 100).Select(i => Existing($"Name{i}", "Test")).ToList();
        var incoming = existing.Take(98).Select(e => Incoming(e.LastName, e.FirstName)).ToList();

        Assert.False(ImportPlanner.Plan(incoming, existing).HasWarnings);
    }

    [Fact]
    public void Flags_a_person_whose_unit_could_not_be_read()
    {
        var plan = ImportPlanner.Plan([Incoming("Ashby", "Miriam", ward: null)], []);
        Assert.Contains(plan.Warnings, w => w.Contains("unrecognised unit", StringComparison.Ordinal));
    }
}
