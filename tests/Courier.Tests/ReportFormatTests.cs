using Courier.Core.Import;

namespace Courier.Tests;

public sealed class ReportFormatTests
{
    /// <summary>The Single Adults heading, wrapped across three lines exactly as the
    /// report wraps it, at the x positions a real export uses.</summary>
    private static IReadOnlyList<TextLine> SingleAdultsHeading() => PdfLines.Of(8.9,
        (41, 622, "Preferred"), (279, 622, "Individual"), (446, 622, "Birthday"), (515, 622, "Address -"),
        (114, 615, "Individual E-mail"), (355, 615, "Unit"), (402, 615, "Age"),
        (41, 608, "Name"), (279, 608, "Phone"), (446, 608, "(1 Jan)"), (515, 608, "Street 1"));

    /// <summary>The Organizations and Callings members heading, which is one line.</summary>
    private static IReadOnlyList<TextLine> MembersHeading() => PdfLines.Of(8.0,
        (54.1, 600, "Name"), (152.4, 600, "Gender"), (215.6, 600, "Age"),
        (240.6, 600, "Birth Date"), (282.6, 600, "Phone Number"), (343.2, 600, "Email"),
        (480.3, 600, "Current Unit"));

    /// <summary>The Member List heading, which is one line and has no unit column:
    /// the report covers one ward and names it above the table.</summary>
    private static IReadOnlyList<TextLine> MemberListHeading() => PdfLines.Of(9.0,
        (41.8, 600, "Name"), (153.9, 600, "Gender"), (225.2, 600, "Age"),
        (274.7, 600, "Birth Date"), (346.0, 600, "Phone Number"), (430.9, 600, "Email"));

    /// <summary>The callings heading printed above the members table on page 1. It
    /// carries a Name and a Current Unit of its own, at quite different positions.</summary>
    private static IReadOnlyList<TextLine> CallingsHeading() => PdfLines.Of(8.0,
        (35.1, 600, "Calling"), (225.6, 600, "Name"), (321.7, 600, "Sustained"),
        (408.5, 600, "Set Apart"), (483.5, 600, "Current Unit"));

    [Fact]
    public void Single_adults_recognises_its_own_heading()
    {
        var layout = ReportFormats.SingleAdults.Detect(SingleAdultsHeading());
        Assert.NotNull(layout);
        Assert.True(layout.Has(LcrField.Address));
        Assert.True(layout.Has(LcrField.Email));
    }

    [Fact]
    public void Organizations_and_callings_recognises_its_own_heading()
    {
        var layout = ReportFormats.OrganizationsAndCallings.Detect(MembersHeading());
        Assert.NotNull(layout);
        Assert.True(layout.Has(LcrField.Birthday));
        Assert.False(layout.Has(LcrField.Address));
    }

    [Fact]
    public void Member_list_recognises_its_own_heading()
    {
        var layout = ReportFormats.MemberList.Detect(MemberListHeading());
        Assert.NotNull(layout);
        Assert.True(layout.Has(LcrField.Birthday));
        Assert.True(layout.Has(LcrField.Email));
        Assert.False(layout.Has(LcrField.Unit));
        Assert.False(layout.Has(LcrField.Address));
    }

    [Fact]
    public void Neither_format_claims_the_other_s_heading()
    {
        Assert.Null(ReportFormats.SingleAdults.Detect(MembersHeading()));
        Assert.Null(ReportFormats.OrganizationsAndCallings.Detect(SingleAdultsHeading()));
    }

    [Fact]
    public void Nothing_claims_the_member_list_heading_but_the_member_list()
    {
        // It has no Address column, so Single Adults cannot want it, and no unit
        // column of any kind, so Organizations and Callings cannot either.
        Assert.Null(ReportFormats.SingleAdults.Detect(MemberListHeading()));
        Assert.Null(ReportFormats.OrganizationsAndCallings.Detect(MemberListHeading()));
    }

    [Fact]
    public void The_member_list_would_claim_an_organizations_and_callings_heading()
    {
        // Every column it declares, that report prints too, under the same words. This
        // is not a bug to fix in the descriptor — a subset is a subset — it is the
        // reason Member List is registered last, and the reason the order below is
        // asserted rather than assumed. Offered such a file first, it would read every
        // ward as blank.
        Assert.NotNull(ReportFormats.MemberList.Detect(MembersHeading()));

        var order = ReportFormats.Known.ToList();
        Assert.True(
            order.IndexOf(ReportFormats.OrganizationsAndCallings) < order.IndexOf(ReportFormats.MemberList),
            "Member List is registered before Organizations and Callings, and would claim its files");
    }

    [Fact]
    public void The_callings_heading_is_not_the_members_heading()
    {
        // It carries two of the seven headings the members table needs. Two is enough
        // for the line to contribute, and nowhere near enough to be the heading.
        Assert.Null(ReportFormats.OrganizationsAndCallings.Detect(CallingsHeading()));
        Assert.Null(ReportFormats.SingleAdults.Detect(CallingsHeading()));
    }

    [Fact]
    public void A_line_carrying_one_heading_gives_nothing_away()
    {
        // The Single Adults report's toolbar reads "Group by Unit Edit Report".
        // Matching its Unit would fold four fields into one.
        var toolbar = PdfLines.Of(8.9, (41, 700, "Group by Unit Edit Report"));
        Assert.Null(ReportFormats.SingleAdults.Detect(toolbar));
    }

    [Fact]
    public void The_formats_are_registered_and_named_the_way_lcr_names_them()
    {
        // The order is the trying order, and it is load-bearing: see the test above.
        Assert.Equal(
            ["Single Adults", "Organizations and Callings", "Member List"],
            ReportFormats.Known.Select(f => f.Name));
    }

    [Fact]
    public void Each_format_declares_what_it_prints()
    {
        Assert.Equal(ReportFields.All, ReportFormats.SingleAdults.Carries);

        var callings = ReportFormats.OrganizationsAndCallings.Carries;
        Assert.Equal(ReportFields.Unit | ReportFields.Birthday | ReportFields.Email | ReportFields.Phone, callings);

        // No unit column and no address column at all, and an Age column filled on so
        // few rows that carrying it would blank far more ages than it set.
        var memberList = ReportFormats.MemberList.Carries;
        Assert.Equal(ReportFields.Birthday | ReportFields.Email | ReportFields.Phone, memberList);
    }

    [Fact]
    public void Page_furniture_is_recognised_per_report()
    {
        Assert.True(ReportFormats.SingleAdults.IsFurniture("Page 3 of 29"));
        Assert.True(ReportFormats.SingleAdults.IsFurniture("https://lcr.churchofjesuschrist.org/mlt/report"));
        Assert.False(ReportFormats.SingleAdults.IsFurniture("Ashby, Miriam"));

        Assert.True(ReportFormats.OrganizationsAndCallings.IsFurniture(
            "21 Sep 2026 For Church Use Only © 2026 by Intellectual Reserve, Inc. All rights reserved. 1"));
        Assert.True(ReportFormats.OrganizationsAndCallings.IsFurniture("Count: 428"));
        Assert.False(ReportFormats.OrganizationsAndCallings.IsFurniture("Ashby, Miriam"));

        Assert.True(ReportFormats.MemberList.IsFurniture(
            "21 Sep 2026 For Church Use Only © 2026 by Intellectual Reserve, Inc. All rights reserved. 1"));
        Assert.True(ReportFormats.MemberList.IsFurniture("Count: 160"));
        Assert.False(ReportFormats.MemberList.IsFurniture("Ashby, Miriam"));
    }
}
