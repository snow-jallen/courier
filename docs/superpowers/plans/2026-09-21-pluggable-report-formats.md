# Pluggable LCR Report Formats Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Courier reads two LCR reports — the existing **Single Adults** report and the **Organizations and Callings** report — deciding which one a file is by inspecting it, with new formats added as a short declarative record.

**Architecture:** A report format becomes a strategy (`IReportFormat`) holding the facts that vary between reports: its heading phrases and column order, the vertical gap that separates its rows, the lines that are page furniture, and which fields it prints. `LcrReportParser` picks the first registered format that recognises the document and lets it drive every page. The geometry that does *not* vary — vertically centred cells, glyph-to-line grouping — stays in `PdfTableReader`, untouched. Because the second report prints no Address and never fills its Age column, the "an import owns these fields" rule narrows to "an import owns the fields its report prints", and that fact travels from the parser through the plan into the write.

**Tech Stack:** C# / .NET, PdfPig 0.1.16, EF Core + SQLite, Avalonia (MVVM, CommunityToolkit.Mvvm), xUnit.

**Spec:** `docs/superpowers/specs/2026-09-21-pluggable-report-formats-design.md`

## Global Constraints

- **Warnings are errors.** Every task must build clean.
- **`dotnet test` before every commit.** About 130 tests, a second or so. It must stay green at the end of every task.
- **No real LCR export, and nothing derived from one, may be committed.** Real exports live in `tests/Courier.Tests/Fixtures/private/`, which is gitignored. Committed fixtures are synthetic and built in code.
- **Nothing identifying goes in the log.** Names never; addresses only through `Redact.Address`; provider messages only through `Redact.Failure`; message bodies only as a length. A format name is not identifying and may be logged.
- **An import must never write preferred channel, notes, or hand-added contact details.** Tests enforce this; they must keep passing unchanged.
- **Enums are stored by name, never by number.** No entity changes in this plan, so no migration is expected. If you find yourself changing an entity, stop — that is out of scope.
- **Commit style:** small commits, one concern each, imperative subject with a `feat:`/`fix:`/`docs:`/`refactor:`/`test:` prefix; the body explains why. End every commit message with:

```
Co-Authored-By: <the model that actually wrote the commit> <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_0162Q5wUzGeN3fyawg57oWSC
```

The `Claude-Session` line is fixed and identical on every commit — it is what leads back to the conversation. The model name on `Co-Authored-By` is whatever your own attribution reminder gives you, because the line should name whoever did the work. Tasks on this plan run on different models, so `git log` will carry more than one name; that is accurate rather than a mistake, and it is not a review finding.

- **User-facing copy is for people without a technical background.** Errors say what went wrong and what to do about it. A provider or library error code must never reach the screen.
- **Views do not compute.** Any sentence shown to the user is composed in `Courier.Core`.

## Measurements this plan depends on

Taken from a real export of each report. Do not change these numbers without re-measuring.

| | Single Adults | Organizations and Callings |
|---|---|---|
| Text size | 8.9pt | 8.0pt |
| Gap between rows | 23.2pt (2.6x) | 13.4pt (1.68x) |
| Gap between wrapped lines in a row | 12.8pt (1.44x) | 4.5pt (0.56x) |
| `RowBreakFactor` | 2.0 | 1.0 |
| Heading x positions | Name 41, Email 114, Phone 279, Unit 355, Age 402, Birthday 446, Address 515 | Name 54.1, Gender 152.4, Age 215.6, Birth Date 240.6, Phone Number 282.6, Email 343.2, Current Unit 480.3 |
| Data x positions | same as heading | Name 55.6, Gender 153.9, Birthday 242.1, Phone 284.1, Email 344.7, Unit 481.8 |
| Pages / people | 29 / 427 | 9 / 428 |

## File structure

**Create, in `src/Courier.Core/Import/`:**

| File | Responsibility |
|---|---|
| `LcrField.cs` | `LcrField`, `ReportFields`, `ReportSource` — the vocabulary for "which field" and "which fields does this report print" |
| `ColumnLayout.cs` | Where each column starts on the page, and pulling one field's text out of a row group |
| `IReportFormat.cs` | The strategy interface and `ColumnSpec` |
| `ColumnLayoutFormat.cs` | The descriptor-driven implementation: heading-phrase matching and furniture |
| `ReportFormats.cs` | The registry, and the two descriptors |

**Create, in `tests/Courier.Tests/`:**

| File | Responsibility |
|---|---|
| `PdfLines.cs` | Builds a one-page PDF from `(x, y, text)` and reads it back as `TextLine`s |
| `ColumnLayoutTests.cs` | Column spans, boundary-only columns, the left-to-right guard |
| `ReportFormatTests.cs` | Each descriptor recognises its own heading and not the other's |
| `SyntheticCallingsReport.cs` | A two-page Organizations-and-Callings PDF, built in code |
| `CallingsReportParserTests.cs` | Parsing the new format end to end |

**Create, committed:** `tests/Courier.Tests/Fixtures/README.md`

**Modify:**

| File | Change |
|---|---|
| `src/Courier.Core/Import/PdfTableReader.cs` | `RowBreakThreshold` takes the factor; the constant goes |
| `src/Courier.Core/Import/LcrReportParser.cs` | Choose a format, then drive it per page |
| `src/Courier.Core/Import/ImportPlan.cs` | `ImportPlan` gains `Notes` |
| `src/Courier.Core/Import/ImportPlanner.cs` | `Plan` takes a `ReportSource`; `Diff` skips uncarried fields; composes `Notes` |
| `src/Courier.Data/ImportService.cs` | `PrepareAsync` passes the source; `ApplyFields` and `SyncLcrContactAsync` respect `Carries` |
| `src/Courier.App/ViewModels/ImportViewModel.cs` | Surfaces `Notes` |
| `src/Courier.App/Views/ImportView.axaml` | A notes strip, and the lede stops naming one report |
| `tests/Courier.Tests/LcrReportParserTests.cs` | The "not a report" message changed |
| `tests/Courier.Tests/RealReportTests.cs` | `ColumnDetectionTests` uses the new types; a real-callings-export set is added |
| `tests/Courier.Tests/ImportPlannerTests.cs`, `ImportServiceTests.cs`, `DirectoryServiceTests.cs` | `Plan` call sites |
| `tests/Courier.Tests/TestPaths.cs` | `RealCallingsReport` and `RequiresRealCallingsReportAttribute` |
| `docs/architecture.md` | "Reading the report" rewritten |
| `CLAUDE.md` | The import-ownership rule narrows by one clause |

**Delete:** `src/Courier.Core/Import/LcrColumns.cs`

---

### Task 1: The field vocabulary and `ColumnLayout`

Replaces `LcrColumns`'s fixed field order with a layout built from whatever columns a format declares, including columns that exist only to be a boundary.

**Files:**
- Create: `src/Courier.Core/Import/LcrField.cs`
- Create: `src/Courier.Core/Import/ColumnLayout.cs`
- Create: `tests/Courier.Tests/PdfLines.cs`
- Test: `tests/Courier.Tests/ColumnLayoutTests.cs`

**Interfaces:**
- Consumes: `TextLine` (`Baseline`, `Letters`, `Text`, `Cell(left, right)`, `Words()`), `PdfTableReader.ReadLines(Page)`.
- Produces:
  - `enum LcrField { Name, Email, Phone, Unit, Age, Birthday, Address }`
  - `[Flags] enum ReportFields { None, Unit, Age, Birthday, Address, Email, Phone, All }`
  - `sealed record ReportSource(string FormatName, ReportFields Carries)` with `bool Carry(ReportFields field)`
  - `sealed class ColumnLayout` with `static ColumnLayout? From(IReadOnlyList<(double X, LcrField? Field)> columns)`, `bool Has(LcrField field)`, `string Cell(IReadOnlyList<TextLine> group, LcrField field)`
  - `internal static class PdfLines` with `IReadOnlyList<TextLine> Of(double size, params (double X, double Y, string Text)[] words)`

- [ ] **Step 1: Write the test helper that turns positions into real `TextLine`s**

`ColumnLayout` works on glyph geometry, and a hand-built `Letter` is both awkward to construct and a poor stand-in. Build a real PDF and read it back, which is the path the parser takes.

Create `tests/Courier.Tests/PdfLines.cs`:

```csharp
using Courier.Core.Import;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;

namespace Courier.Tests;

/// <summary>Writes words at given positions into a one-page PDF and reads them back
/// as lines. Tests that need geometry get real glyph advance boxes this way, rather
/// than a hand-made stand-in that can agree with a broken reader.</summary>
internal static class PdfLines
{
    public static IReadOnlyList<TextLine> Of(double size, params (double X, double Y, string Text)[] words)
    {
        var builder = new PdfDocumentBuilder();
        var page = builder.AddPage(612, 792);
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);
        foreach (var (x, y, text) in words)
            if (text.Length > 0) page.AddText(text, size, new PdfPoint(x, y), font);

        using var document = PdfDocument.Open(builder.Build());
        return PdfTableReader.ReadLines(document.GetPage(1));
    }
}
```

- [ ] **Step 2: Write the failing tests**

Create `tests/Courier.Tests/ColumnLayoutTests.cs`:

```csharp
using Courier.Core.Import;

namespace Courier.Tests;

public sealed class ColumnLayoutTests
{
    // The Organizations and Callings positions, which are the ones that break a
    // layout built from a fixed field order: phone and e-mail are the other way
    // round, and there is a Gender column with nothing behind it.
    private static ColumnLayout Layout() =>
        ColumnLayout.From([
            (54.1, LcrField.Name), (152.4, null), (215.6, LcrField.Age),
            (240.6, LcrField.Birthday), (282.6, LcrField.Phone),
            (343.2, LcrField.Email), (480.3, LcrField.Unit),
        ])!;

    [Fact]
    public void Reads_each_field_from_its_own_column()
    {
        var lines = PdfLines.Of(8.0,
            (55.6, 600, "Ashdown, Marigold"), (153.9, 600, "F"), (242.1, 600, "17 Jan"),
            (284.1, 600, "(435) 555-0100"), (344.7, 600, "m.ashdown@example.com"),
            (481.8, 600, "Manti 2nd Ward"));
        var layout = Layout();

        Assert.Equal("Ashdown, Marigold", layout.Cell(lines, LcrField.Name));
        Assert.Equal("17 Jan", layout.Cell(lines, LcrField.Birthday));
        Assert.Equal("(435) 555-0100", layout.Cell(lines, LcrField.Phone));
        Assert.Equal("m.ashdown@example.com", layout.Cell(lines, LcrField.Email));
        Assert.Equal("Manti 2nd Ward", layout.Cell(lines, LcrField.Unit));
    }

    [Fact]
    public void Discards_a_column_that_has_no_field_behind_it()
    {
        // Gender is detected only so that its edge exists. Without that edge the
        // "F" joins the name and every person reads "Ashdown, Marigold F".
        var lines = PdfLines.Of(8.0, (55.6, 600, "Ashdown, Marigold"), (153.9, 600, "F"));
        Assert.Equal("Ashdown, Marigold", Layout().Cell(lines, LcrField.Name));
    }

    [Fact]
    public void Leaves_a_field_empty_rather_than_borrowing_the_next_column()
    {
        var lines = PdfLines.Of(8.0,
            (55.6, 600, "Brackenbury, Tavish"), (153.9, 600, "M"), (242.1, 600, "3 Jul"),
            (481.8, 600, "Manti 6th Ward"));
        var layout = Layout();

        Assert.Equal("", layout.Cell(lines, LcrField.Phone));
        Assert.Equal("", layout.Cell(lines, LcrField.Email));
        Assert.Equal("Manti 6th Ward", layout.Cell(lines, LcrField.Unit));
    }

    [Fact]
    public void Rejoins_a_cell_that_wraps_over_several_lines()
    {
        // A wrapped name sits 4.5pt either side of the row, so its two lines are
        // separate TextLines that both belong to the Name column.
        var lines = PdfLines.Of(8.0,
            (55.6, 604.5, "Featherstonehaugh,"), (153.9, 600, "F"), (55.6, 595.5, "Wilhelmina"));
        Assert.Equal("Featherstonehaugh, Wilhelmina", Layout().Cell(lines, LcrField.Name));
    }

    [Fact]
    public void Answers_for_a_field_the_layout_does_not_have()
    {
        var layout = Layout();
        Assert.False(layout.Has(LcrField.Address));
        Assert.True(layout.Has(LcrField.Age));
        Assert.Equal("", layout.Cell(PdfLines.Of(8.0, (55.6, 600, "Ashdown, Marigold")), LcrField.Address));
    }

    [Fact]
    public void Refuses_columns_that_do_not_run_left_to_right()
    {
        // A heading matched somewhere it does not belong shows up as an out-of-order
        // position. Rejecting the layout is what stops four fields folding into one.
        Assert.Null(ColumnLayout.From([
            (225.6, LcrField.Name), (152.4, null), (480.3, LcrField.Unit)]));
    }

    [Fact]
    public void Refuses_an_empty_layout() => Assert.Null(ColumnLayout.From([]));
}
```

- [ ] **Step 3: Run the tests to verify they fail**

Run: `dotnet test --filter FullyQualifiedName~ColumnLayoutTests`
Expected: FAIL to compile — `LcrField`, `ColumnLayout` and `PdfLines` do not exist.

- [ ] **Step 4: Write `LcrField.cs`**

```csharp
namespace Courier.Core.Import;

/// <summary>A field a report can print. Gender is deliberately absent: the
/// Organizations and Callings report prints it, but Courier has no use for it, so it
/// is found as a column boundary and its contents discarded. Name has no entry in
/// <see cref="ReportFields"/> because a report without names is not a report.</summary>
public enum LcrField { Name, Email, Phone, Unit, Age, Birthday, Address }

/// <summary>The fields a report prints, and therefore the only fields an import from
/// it may overwrite. A report with no Address column must not blank the addresses
/// already recorded — they came from a report that did print them.</summary>
[Flags]
public enum ReportFields
{
    None = 0,
    Unit = 1,
    Age = 2,
    Birthday = 4,
    Address = 8,
    Email = 16,
    Phone = 32,
    All = Unit | Age | Birthday | Address | Email | Phone,
}

/// <summary>Where a set of rows came from: what the report is called, in LCR's own
/// words, and what it prints. Both travel with the report into the plan and into the
/// write, because the screen and the database have to agree.</summary>
public sealed record ReportSource(string FormatName, ReportFields Carries)
{
    public bool Carry(ReportFields field) => (Carries & field) != 0;
}
```

- [ ] **Step 5: Write `ColumnLayout.cs`**

```csharp
namespace Courier.Core.Import;

/// <summary>Where each column of a report starts, in PDF points, read from the
/// report's own headings rather than hard-coded, so a re-styled export does not
/// silently shift every field by one column.
///
/// Columns arrive in the order the format declares them, which is the order they
/// appear across the page. A layout whose positions do not run strictly left to right
/// is rejected: it means a heading was matched somewhere it does not belong, and the
/// two reports each contain a word that appears as a heading in more than one place.
///
/// A column may have no field behind it. The Organizations and Callings report prints
/// Gender between the name and the age; the edge has to exist or the "F" joins the
/// name, and nothing stores what is in it.</summary>
public sealed class ColumnLayout
{
    /// <summary>Cells are left-aligned on these x positions, but a glyph can start a
    /// fraction of a point to the left of its column. Without this slack the opening
    /// bracket of "(435) 555-0100" lands in the column before the phone.</summary>
    private const double Slack = 1.5;

    private readonly IReadOnlyList<(double X, LcrField? Field)> _columns;

    private ColumnLayout(IReadOnlyList<(double X, LcrField? Field)> columns) => _columns = columns;

    /// <summary>Null when there are no columns, or when their positions do not
    /// increase left to right.</summary>
    public static ColumnLayout? From(IReadOnlyList<(double X, LcrField? Field)> columns)
    {
        if (columns.Count == 0) return null;
        for (var i = 1; i < columns.Count; i++)
            if (columns[i].X <= columns[i - 1].X) return null;
        return new ColumnLayout(columns);
    }

    public bool Has(LcrField field) => _columns.Any(c => c.Field == field);

    /// <summary>One field's text, gathered from every line of a row group, so a cell
    /// that wraps over several lines comes back as one string.</summary>
    public string Cell(IReadOnlyList<TextLine> group, LcrField field)
    {
        var index = -1;
        for (var i = 0; i < _columns.Count; i++)
            if (_columns[i].Field == field) { index = i; break; }
        if (index < 0) return "";

        var left = _columns[index].X - Slack;
        var right = index + 1 < _columns.Count
            ? _columns[index + 1].X - Slack
            : double.PositiveInfinity;

        var parts = new List<string>();
        foreach (var line in group)
        {
            var text = line.Cell(left, right);
            if (text.Length > 0) parts.Add(text);
        }
        return string.Join(' ', parts);
    }
}
```

- [ ] **Step 6: Run the tests to verify they pass**

Run: `dotnet test --filter FullyQualifiedName~ColumnLayoutTests`
Expected: 7 passed.

- [ ] **Step 7: Run the whole suite**

Run: `dotnet test`
Expected: all pass. Nothing else uses the new types yet.

- [ ] **Step 8: Commit**

```bash
git add src/Courier.Core/Import/LcrField.cs src/Courier.Core/Import/ColumnLayout.cs tests/Courier.Tests/PdfLines.cs tests/Courier.Tests/ColumnLayoutTests.cs
git commit -F - <<'MSG'
feat: a column layout that any report's headings can describe

LcrColumns builds its edges by walking a fixed field order, which only
works while there is one report. A second LCR report puts phone and
e-mail the other way round and prints a Gender column between the name
and the age, so the edges have to come from whatever columns a format
declares -- including a column that exists only so that its edge does,
whose contents are discarded.

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_0162Q5wUzGeN3fyawg57oWSC
MSG
```

---

### Task 2: The format strategy and the two descriptors

**Files:**
- Create: `src/Courier.Core/Import/IReportFormat.cs`
- Create: `src/Courier.Core/Import/ColumnLayoutFormat.cs`
- Create: `src/Courier.Core/Import/ReportFormats.cs`
- Test: `tests/Courier.Tests/ReportFormatTests.cs`

**Interfaces:**
- Consumes: `LcrField`, `ReportFields`, `ColumnLayout.From`, `TextLine.Words()`, `TextLine.Text`.
- Produces:
  - `sealed record ColumnSpec(IReadOnlyList<string> Headings, LcrField? Field)` with a `(string heading, LcrField? field)` convenience constructor
  - `interface IReportFormat { string Name; double RowBreakFactor; ReportFields Carries; ColumnLayout? Detect(IReadOnlyList<TextLine> group); bool IsFurniture(string lineText); }`
  - `sealed class ColumnLayoutFormat : IReportFormat` with constructor `(string name, IReadOnlyList<ColumnSpec> columns, double rowBreakFactor, ReportFields carries, IReadOnlyList<string> furniture)`
  - `static class ReportFormats` with `IReadOnlyList<IReportFormat> Known`, `IReportFormat SingleAdults`, `IReportFormat OrganizationsAndCallings`

- [ ] **Step 1: Write the failing tests**

Create `tests/Courier.Tests/ReportFormatTests.cs`:

```csharp
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
    public void Neither_format_claims_the_other_s_heading()
    {
        Assert.Null(ReportFormats.SingleAdults.Detect(MembersHeading()));
        Assert.Null(ReportFormats.OrganizationsAndCallings.Detect(SingleAdultsHeading()));
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
    public void The_two_formats_are_registered_and_named_the_way_lcr_names_them()
    {
        Assert.Equal(
            ["Single Adults", "Organizations and Callings"],
            ReportFormats.Known.Select(f => f.Name));
    }

    [Fact]
    public void Each_format_declares_what_it_prints()
    {
        Assert.Equal(ReportFields.All, ReportFormats.SingleAdults.Carries);

        var callings = ReportFormats.OrganizationsAndCallings.Carries;
        Assert.Equal(ReportFields.Unit | ReportFields.Birthday | ReportFields.Email | ReportFields.Phone, callings);
    }

    [Fact]
    public void Page_furniture_is_recognised_per_report()
    {
        Assert.True(ReportFormats.SingleAdults.IsFurniture("Page 3 of 29"));
        Assert.True(ReportFormats.SingleAdults.IsFurniture("https://lcr.churchofjesuschrist.org/mlt/report"));
        Assert.False(ReportFormats.SingleAdults.IsFurniture("Ashby, Miriam"));

        Assert.True(ReportFormats.OrganizationsAndCallings.IsFurniture(
            "21 Sep 2026 For Church Use Only \u00a9 2026 by Intellectual Reserve, Inc. All rights reserved. 1"));
        Assert.True(ReportFormats.OrganizationsAndCallings.IsFurniture("Count: 428"));
        Assert.False(ReportFormats.OrganizationsAndCallings.IsFurniture("Ashby, Miriam"));
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test --filter FullyQualifiedName~ReportFormatTests`
Expected: FAIL to compile — `ReportFormats` does not exist.

- [ ] **Step 3: Write `IReportFormat.cs`**

```csharp
namespace Courier.Core.Import;

/// <summary>One column of a report. <paramref name="Headings"/> are the phrases that
/// name it in the report's own heading row, tried longest first so that "Individual
/// E-mail" wins over a bare "Individual". A null <paramref name="Field"/> means the
/// column is found only so that its edge exists — whatever is printed in it is
/// discarded.</summary>
public sealed record ColumnSpec(IReadOnlyList<string> Headings, LcrField? Field)
{
    public ColumnSpec(string heading, LcrField? field) : this([heading], field) { }
}

/// <summary>One report Courier knows how to read.
///
/// Everything here is a fact about a particular report rather than about PDFs, which
/// is why it is a strategy: the vertical rhythm that separates its rows, the words
/// that name its columns, the lines that belong to the page rather than to a person,
/// and which fields it actually prints.</summary>
public interface IReportFormat
{
    /// <summary>What to call this report on screen, in LCR's own words.</summary>
    string Name { get; }

    /// <summary>A vertical gap wider than this many times the text size starts a new
    /// row. A property of the report's layout, not of the page: a relative rule
    /// cannot tell a page of single-line rows apart from one tall row.</summary>
    double RowBreakFactor { get; }

    /// <summary>The fields this report prints, and therefore the only fields an
    /// import from it may overwrite.</summary>
    ReportFields Carries { get; }

    /// <summary>The column positions, if this group of lines is this report's heading
    /// row. Null for any other group, which is every group on a body page. The caller
    /// does the grouping, at this format's own <see cref="RowBreakFactor"/>.</summary>
    ColumnLayout? Detect(IReadOnlyList<TextLine> group);

    /// <summary>True for a line belonging to the page rather than to a person.</summary>
    bool IsFurniture(string lineText);
}
```

- [ ] **Step 4: Write `ColumnLayoutFormat.cs`**

```csharp
namespace Courier.Core.Import;

/// <summary>A report that is a table whose own headings give the column positions.
/// Both reports Courier reads today are of this shape, so both are instances of this
/// class rather than separate code. A report that is not this shape implements
/// <see cref="IReportFormat"/> directly.</summary>
public sealed class ColumnLayoutFormat(
    string name,
    IReadOnlyList<ColumnSpec> columns,
    double rowBreakFactor,
    ReportFields carries,
    IReadOnlyList<string> furniture) : IReportFormat
{
    /// <summary>A line must carry at least this many of the report's headings to
    /// contribute any of them. The Single Adults report's toolbar line reads "Group by
    /// Unit Edit Report", and matching its "Unit" puts the columns out of order and
    /// folds four fields into one.</summary>
    private const int MinimumHeadingsOnALine = 2;

    public string Name => name;
    public double RowBreakFactor => rowBreakFactor;
    public ReportFields Carries => carries;

    public ColumnLayout? Detect(IReadOnlyList<TextLine> group)
    {
        var found = new double?[columns.Count];

        foreach (var line in group)
        {
            var onThisLine = Match(line);
            if (onThisLine.Count < MinimumHeadingsOnALine) continue;
            foreach (var (column, x) in onThisLine)
                found[column] ??= x;
        }

        // Every column a format declares is required. A group yielding six of seven
        // is some other table, not this report's heading.
        if (found.Any(x => x is null)) return null;

        return ColumnLayout.From(
            found.Select((x, i) => (x!.Value, columns[i].Field)).ToList());
    }

    public bool IsFurniture(string lineText)
    {
        if (lineText.Length == 0) return true;

        // Both reports number their pages; only one of them writes "Page n of m".
        if (lineText.StartsWith("Page ", StringComparison.Ordinal)
            && lineText.Contains(" of ", StringComparison.Ordinal)) return true;

        return furniture.Any(f => lineText.Contains(f, StringComparison.Ordinal));
    }

    /// <summary>Which of this report's headings appear on one line, and where each
    /// one starts. Phrases are tried longest first so that a column named by several
    /// words is not stolen by a shorter phrase sharing its first word.</summary>
    private IReadOnlyList<(int Column, double X)> Match(TextLine line)
    {
        var words = line.Words();
        var hits = new List<(int, double)>();
        var claimed = new bool[words.Count];

        var candidates = columns
            .SelectMany((c, i) => c.Headings.Select(h => (Column: i, Words: h.Split(' '))))
            .OrderByDescending(c => c.Words.Length)
            .ToList();

        foreach (var (column, phrase) in candidates)
        {
            if (hits.Any(h => h.Item1 == column)) continue;

            for (var start = 0; start + phrase.Length <= words.Count; start++)
            {
                var matches = true;
                for (var w = 0; w < phrase.Length && matches; w++)
                    matches = !claimed[start + w]
                           && string.Equals(words[start + w].Text, phrase[w], StringComparison.Ordinal);
                if (!matches) continue;

                for (var w = 0; w < phrase.Length; w++) claimed[start + w] = true;
                hits.Add((column, words[start].X));
                break;
            }
        }

        return hits;
    }
}
```

- [ ] **Step 5: Write `ReportFormats.cs`**

```csharp
namespace Courier.Core.Import;

/// <summary>The reports Courier can read, tried in this order.
///
/// The two descriptors here cannot claim each other's file: Single Adults requires an
/// Address column, which Organizations and Callings has none of, and Organizations
/// and Callings requires Gender and "Email", where the older report writes "E-mail".
/// Order therefore does not matter today. The rule is nevertheless first match wins,
/// so that a future overlapping descriptor is a decision rather than a surprise.</summary>
public static class ReportFormats
{
    /// <summary>The Single Adults report. Text is 8.9pt; lines wrapped inside one row
    /// sit 12.8pt apart (1.44x) and consecutive rows sit 23.2pt apart (2.6x), so 2.0
    /// falls squarely between them.</summary>
    public static IReportFormat SingleAdults { get; } = new ColumnLayoutFormat(
        name: "Single Adults",
        columns:
        [
            // "Preferred Name" wraps onto two lines, and the line carrying "Name" is
            // also the only one carrying "Phone".
            new(["Preferred Name", "Preferred", "Name"], LcrField.Name),
            new(["Individual E-mail"], LcrField.Email),
            new(["Phone"], LcrField.Phone),
            new(["Unit"], LcrField.Unit),
            new(["Age"], LcrField.Age),
            new(["Birthday"], LcrField.Birthday),
            new(["Address"], LcrField.Address),
        ],
        rowBreakFactor: 2.0,
        carries: ReportFields.All,
        furniture:
        [
            "lcr.churchofjesuschrist.org", "Single Adults", "Description", "Group by Unit",
            "Count:", "Preferred", "Individual", "Street 1", "(1 Jan)",
        ]);

    /// <summary>The Single Adult Members section of the Organizations and Callings
    /// report. Text is 8.0pt; the lines of a wrapped name sit 4.5pt apart (0.56x) and
    /// consecutive rows sit 13.4pt apart (1.68x), so 1.0 puts the threshold at 8.0pt —
    /// 1.8x the widest gap inside a row and 0.6x the narrowest gap between two.
    ///
    /// It prints an Age column and never fills it: empty on all 428 rows of the only
    /// export available. Age is mapped so that a value would be read if one ever
    /// appeared, and left out of Carries so that the emptiness cannot wipe an age
    /// recorded from the report that does print them.</summary>
    public static IReportFormat OrganizationsAndCallings { get; } = new ColumnLayoutFormat(
        name: "Organizations and Callings",
        columns:
        [
            new("Name", LcrField.Name),
            new("Gender", null),          // found for its edge; nothing stores it
            new("Age", LcrField.Age),
            new("Birth Date", LcrField.Birthday),
            new("Phone Number", LcrField.Phone),
            new("Email", LcrField.Email),
            new("Current Unit", LcrField.Unit),
        ],
        rowBreakFactor: 1.0,
        carries: ReportFields.Unit | ReportFields.Birthday | ReportFields.Email | ReportFields.Phone,
        furniture: ["For Church Use Only", "Count:"]);

    public static IReadOnlyList<IReportFormat> Known { get; } = [SingleAdults, OrganizationsAndCallings];
}
```

- [ ] **Step 6: Run the tests to verify they pass**

Run: `dotnet test --filter FullyQualifiedName~ReportFormatTests`
Expected: 8 passed.

If `Neither_format_claims_the_other_s_heading` fails on the Single Adults side, check that `Match` is not letting the bare phrase `"Name"` plus `"Unit"` inside `"Current Unit"` reach two hits — `Words()` splits on whitespace, so `"Current"` and `"Unit"` are separate words and the bare `"Unit"` phrase *does* match. That is fine: Single Adults then finds Name, Unit and Age but not Address, so `Detect` returns null on the missing-column check.

- [ ] **Step 7: Run the whole suite**

Run: `dotnet test`
Expected: all pass. Nothing calls the registry yet.

- [ ] **Step 8: Commit**

```bash
git add src/Courier.Core/Import/IReportFormat.cs src/Courier.Core/Import/ColumnLayoutFormat.cs src/Courier.Core/Import/ReportFormats.cs tests/Courier.Tests/ReportFormatTests.cs
git commit -F - <<'MSG'
feat: describe each LCR report as a format rather than as code

The facts that differ between the two reports -- the words that name
their columns, the order those columns come in, the vertical gap that
separates a row from the next, the lines that are page furniture, and
which fields they print -- are now a descriptor each. The geometry they
share stays in PdfTableReader.

A third report of the same shape is a short record; one of a different
shape implements IReportFormat and shares only the reader.

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_0162Q5wUzGeN3fyawg57oWSC
MSG
```

---

### Task 3: The parser chooses a format and lets it drive

**Files:**
- Modify: `src/Courier.Core/Import/PdfTableReader.cs:20-33` (the `RowBreakFactor` constant and `RowBreakThreshold`)
- Modify: `src/Courier.Core/Import/LcrReportParser.cs` (whole file)
- Delete: `src/Courier.Core/Import/LcrColumns.cs`
- Modify: `tests/Courier.Tests/LcrReportParserTests.cs:50-56` (the "not a report" message)
- Modify: `tests/Courier.Tests/RealReportTests.cs` (`ColumnDetectionTests`)
- Modify: `tests/Courier.Tests/ImportServiceTests.cs:28`, `tests/Courier.Tests/DirectoryServiceTests.cs:28` (the two `LcrReport` constructions)

**Interfaces:**
- Consumes: `ReportFormats.Known`, `IReportFormat`, `ColumnLayout`, `LcrField`, `ReportSource`.
- Produces:
  - `PdfTableReader.RowBreakThreshold(IReadOnlyList<TextLine> lines, double factor)` — the factor is now a parameter
  - `sealed record LcrReport(IReadOnlyList<LcrRow> Rows, int PageCount, string FileName, string Sha256, ReportSource Source)`

- [ ] **Step 1: Write the failing test for the new rejection message**

In `tests/Courier.Tests/LcrReportParserTests.cs`, replace the body of `Refuses_a_pdf_that_is_not_the_report`:

```csharp
    [Fact]
    public void Refuses_a_pdf_that_is_not_a_report_it_knows()
    {
        var notTheReport = new PdfNotAReport();
        var error = Assert.Throws<LcrReportException>(
            () => LcrReportParser.Parse(new MemoryStream(notTheReport.Bytes), "holiday-photos.pdf"));

        Assert.Contains("holiday-photos.pdf", error.Message, StringComparison.Ordinal);
        Assert.Contains("Single Adults", error.Message, StringComparison.Ordinal);
        Assert.Contains("Organizations and Callings", error.Message, StringComparison.Ordinal);
    }
```

And add, to the same class:

```csharp
    [Fact]
    public void Says_which_report_it_read()
    {
        using var stream = new MemoryStream(SyntheticReport.Build());
        var report = LcrReportParser.Parse(stream, "synthetic.pdf");

        Assert.Equal("Single Adults", report.Source.FormatName);
        Assert.Equal(ReportFields.All, report.Source.Carries);
    }
```

- [ ] **Step 2: Run to verify it fails**

Run: `dotnet test --filter FullyQualifiedName~LcrReportParserTests`
Expected: FAIL to compile — `LcrReport` has no `Source`.

- [ ] **Step 3: Make the row-break factor a parameter**

In `src/Courier.Core/Import/PdfTableReader.cs`, delete the `RowBreakFactor` constant and its doc comment, and replace `RowBreakThreshold` with:

```csharp
    /// <summary>The vertical gap at which one row ends and the next begins.
    ///
    /// Derived from the size of the text on the page and the format's own factor.
    /// Scaling against the text size rather than against the other gaps on the page is
    /// what keeps this working where every row happens to be a single line — a case no
    /// relative rule can tell apart from one tall row. The factor itself belongs to the
    /// report, not to the page: 2.0 separates the Single Adults report's rows and would
    /// merge every row of the Organizations and Callings report, whose rows sit 1.68x
    /// its text size apart.</summary>
    public static double RowBreakThreshold(IReadOnlyList<TextLine> lines, double factor)
    {
        var sizes = lines.SelectMany(l => l.Letters)
            .Select(l => l.PointSize)
            .Where(s => s > 0)
            .OrderBy(s => s)
            .ToList();
        return sizes.Count == 0 ? double.MaxValue : sizes[sizes.Count / 2] * factor;
    }
```

Also update the class doc comment's second bullet, which currently states a fact about one report as though it were general:

```csharp
///  * Rows are separated by more vertical space than the lines inside them, by a
///    ratio that differs from report to report. How much more is the format's
///    business; see IReportFormat.RowBreakFactor.
```

- [ ] **Step 4: Rewrite `LcrReportParser.cs`**

```csharp
using System.Security.Cryptography;
using UglyToad.PdfPig;

namespace Courier.Core.Import;

public sealed record LcrReport(
    IReadOnlyList<LcrRow> Rows, int PageCount, string FileName, string Sha256, ReportSource Source);

public sealed class LcrReportException(string message) : Exception(message);

/// <summary>Reads a report exported from LCR.
///
/// Which report it is, is worked out by inspecting the file: each registered format is
/// offered the pages in turn, and the first to recognise one drives every page. See
/// ReportFormats for the two Courier knows, and docs/architecture.md for why reading
/// these PDFs takes geometry at all.</summary>
public static class LcrReportParser
{
    public static LcrReport Parse(string path)
    {
        using var stream = File.OpenRead(path);
        return Parse(stream, Path.GetFileName(path));
    }

    public static LcrReport Parse(Stream stream, string fileName)
    {
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        var bytes = buffer.ToArray();
        var sha = Convert.ToHexStringLower(SHA256.HashData(bytes));

        using var document = PdfDocument.Open(bytes);
        var pageCount = document.NumberOfPages;

        // Every page is read at most once, however many formats are offered it.
        var cache = new Dictionary<int, IReadOnlyList<TextLine>>();
        IReadOnlyList<TextLine> LinesOf(int number) =>
            cache.TryGetValue(number, out var cached)
                ? cached
                : cache[number] = PdfTableReader.ReadLines(document.GetPage(number));

        var format = Choose(pageCount, LinesOf)
            ?? throw new LcrReportException(Unrecognised(fileName));

        var rows = new List<LcrRow>();
        ColumnLayout? layout = null;

        for (var number = 1; number <= pageCount; number++)
        {
            var lines = LinesOf(number);

            // Below the heading, and only below it. The Organizations and Callings
            // report prints the stake's Single Adult callings above the members table
            // on page 1; read as people, those rows are stitched into the row below
            // and corrupt it.
            var floor = double.PositiveInfinity;
            foreach (var group in PdfTableReader.GroupIntoRows(lines, Threshold(lines, format)))
            {
                var found = format.Detect(group);
                if (found is null) continue;
                layout = found;
                floor = group.Min(l => l.Baseline);
                break;
            }

            // A page before the first heading has nothing to read. A page after it
            // with no heading of its own is a continuation, and keeps every line.
            if (layout is null) continue;

            var body = lines
                .Where(l => l.Baseline < floor)
                .Where(l => !format.IsFurniture(l.Text))
                .ToList();

            foreach (var group in PdfTableReader.GroupIntoRows(body, Threshold(body, format)))
            {
                var row = BuildRow(group, layout, number);
                if (row is not null) rows.Add(row);
            }
        }

        return new LcrReport(
            Stitch(rows), pageCount, fileName, sha, new ReportSource(format.Name, format.Carries));
    }

    private static double Threshold(IReadOnlyList<TextLine> lines, IReportFormat format) =>
        PdfTableReader.RowBreakThreshold(lines, format.RowBreakFactor);

    /// <summary>The first registered format to recognise a heading anywhere in the
    /// document. Null when none does.</summary>
    private static IReportFormat? Choose(int pageCount, Func<int, IReadOnlyList<TextLine>> linesOf)
    {
        for (var number = 1; number <= pageCount; number++)
        {
            var lines = linesOf(number);
            foreach (var format in ReportFormats.Known)
                foreach (var group in PdfTableReader.GroupIntoRows(lines, Threshold(lines, format)))
                    if (format.Detect(group) is not null) return format;
        }
        return null;
    }

    private static string Unrecognised(string fileName) =>
        $"'{fileName}' does not look like a report Courier can read. It knows the " +
        $"{string.Join(" report and the ", ReportFormats.Known.Select(f => f.Name))} report. " +
        "Export one of those from LCR as a PDF and open it here.";

    private static LcrRow? BuildRow(IReadOnlyList<TextLine> group, ColumnLayout layout, int page)
    {
        var row = new LcrRow(
            layout.Cell(group, LcrField.Name),
            layout.Cell(group, LcrField.Email),
            layout.Cell(group, LcrField.Phone),
            layout.Cell(group, LcrField.Unit),
            layout.Cell(group, LcrField.Age),
            layout.Cell(group, LcrField.Birthday),
            layout.Cell(group, LcrField.Address),
            page);

        var empty = row is { Name.Length: 0, Email.Length: 0, Phone.Length: 0, Unit.Length: 0,
                             Age.Length: 0, Birthday.Length: 0, Address.Length: 0 };
        return empty ? null : row;
    }

    /// <summary>Re-joins a person split across two row groups. A long name can push a
    /// row's wrapped lines far enough apart to read as a break, leaving a fragment
    /// with no comma in the name; that fragment belongs to the row above it.</summary>
    private static IReadOnlyList<LcrRow> Stitch(IReadOnlyList<LcrRow> rows)
    {
        var stitched = new List<LcrRow>();
        foreach (var row in rows)
        {
            if (!row.LooksLikeAPerson && stitched.Count > 0)
            {
                var previous = stitched[^1];
                stitched[^1] = new LcrRow(
                    Merge(previous.Name, row.Name),
                    Merge(previous.Email, row.Email),
                    Merge(previous.Phone, row.Phone),
                    Merge(previous.Unit, row.Unit),
                    Merge(previous.Age, row.Age),
                    Merge(previous.Birthday, row.Birthday),
                    Merge(previous.Address, row.Address),
                    previous.Page);
                continue;
            }
            stitched.Add(row);
        }
        return stitched.Where(r => r.LooksLikeAPerson).ToList();
    }

    private static string Merge(string a, string b) =>
        a.Length == 0 ? b : b.Length == 0 ? a : $"{a} {b}";
}
```

Note the `Furniture` array and `IsBodyLine` are gone from this file — they moved into the descriptors in Task 2. `IsFurniture` returns true for an empty line, which is what the old `text.Length == 0` check did.

- [ ] **Step 5: Delete `LcrColumns.cs`**

```bash
git rm src/Courier.Core/Import/LcrColumns.cs
```

- [ ] **Step 5a: Keep the two tests that construct an `LcrReport` compiling**

`LcrReport` has gained a fifth field, and two test files build one directly. Neither is about report formats, so both get the report that prints everything.

In `tests/Courier.Tests/ImportServiceTests.cs`, add beside the other statics (line 13):

```csharp
    private static readonly ReportSource SingleAdults = new("Single Adults", ReportFields.All);
```

and change `Report` (line 28):

```csharp
    private static LcrReport Report(int rows) =>
        new([], 29, "manti-singles.pdf", new string('a', 64), SingleAdults);
```

In `tests/Courier.Tests/DirectoryServiceTests.cs`, add the same static beside `Today` (line 12), and change the construction in `SeedAsync` (line 28):

```csharp
            new LcrReport([], 1, "seed.pdf", new string('a', 64), SingleAdults), plan, Today);
```

These are the only two places outside the parser that construct an `LcrReport` — verified with `grep -rn "new LcrReport(" --include=*.cs src tests`.

- [ ] **Step 6: Update `ColumnDetectionTests` in `tests/Courier.Tests/RealReportTests.cs`**

Replace the whole `ColumnDetectionTests` class with:

```csharp
public sealed class ColumnDetectionTests(ITestOutputHelper output)
{
    [RequiresRealReport]
    public void Finds_the_column_positions_from_the_headings()
    {
        using var document = UglyToad.PdfPig.PdfDocument.Open(TestPaths.RealReport!);
        var lines = PdfTableReader.ReadLines(document.GetPage(1));
        var format = ReportFormats.SingleAdults;
        var threshold = PdfTableReader.RowBreakThreshold(lines, format.RowBreakFactor);

        var layout = PdfTableReader.GroupIntoRows(lines, threshold)
            .Select(format.Detect)
            .FirstOrDefault(l => l is not null);

        output.WriteLine($"row break at: {threshold:0.00}pt");
        Assert.NotNull(layout);
        Assert.True(layout.Has(LcrField.Address));
    }
}
```

- [ ] **Step 7: Run the whole suite**

Run: `dotnet test`
Expected: all pass, including every existing `LcrReportParserTests` case against the Single Adults synthetic. If `Reads_one_row_per_person_and_no_page_furniture` now returns 0 rows, the below-the-heading slice is the suspect: check that `floor` is the heading group's *lowest* baseline (`Min`), not its highest.

- [ ] **Step 8: Commit**

```bash
git add -A src/Courier.Core/Import tests/Courier.Tests/LcrReportParserTests.cs tests/Courier.Tests/RealReportTests.cs tests/Courier.Tests/ImportServiceTests.cs tests/Courier.Tests/DirectoryServiceTests.cs
git commit -F - <<'MSG'
refactor: pick the report format by inspecting the file

The parser no longer knows one report. It offers each page to every
registered format, lets the first that recognises a heading drive the
rest, and records on the report which one that was.

Three changes the second report forces. Headings are detected per row
group rather than merged across the page, because page 1 of the new
report carries a Name heading in two different tables and merging them
puts the columns out of order. Body lines are sliced to below the
heading, because one of those tables sits above it. And the row-break
factor comes from the format, because 2.0 merges every row of a report
whose rows sit 1.68x its text size apart.

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_0162Q5wUzGeN3fyawg57oWSC
MSG
```

---

### Task 4: A synthetic Organizations and Callings report, and tests against it

**Files:**
- Create: `tests/Courier.Tests/SyntheticCallingsReport.cs`
- Test: `tests/Courier.Tests/CallingsReportParserTests.cs`

**Interfaces:**
- Consumes: `LcrReportParser.Parse(Stream, string)`, `LcrReport.Source`, `LcrRow`.
- Produces: `internal static class SyntheticCallingsReport` with `byte[] Build()` — a two-page PDF holding 7 people and a `Count: 7`.

- [ ] **Step 1: Write the synthetic fixture**

Create `tests/Courier.Tests/SyntheticCallingsReport.cs`:

```csharp
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;

namespace Courier.Tests;

/// <summary>Builds a PDF laid out the way LCR lays out the Single Adult Members
/// section of the Organizations and Callings report, so the reader can be tested on
/// what actually makes that report hard without putting a real directory in the
/// repository.
///
/// Every feature the parser depends on is here: the heading repeated on both pages,
/// the stake's callings table printed above the members table on page 1, a name
/// wrapped over two lines and centred on its row, an Age column with a heading and no
/// values, a Gender column with nothing behind it, and the page's own furniture.</summary>
internal static class SyntheticCallingsReport
{
    private const double Size = 8.0;

    // The heading positions a real export uses, and the data positions, which sit a
    // point and a half to their right.
    private const double HName = 54.1, HGender = 152.4, HAge = 215.6, HBirthday = 240.6,
                         HPhone = 282.6, HEmail = 343.2, HUnit = 480.3;
    private const double XName = 55.6, XGender = 153.9, XBirthday = 242.1,
                         XPhone = 284.1, XEmail = 344.7, XUnit = 481.8;

    /// <summary>Consecutive rows, 1.68x the text size apart.</summary>
    private const double Step = 13.4;

    /// <summary>Half the gap between the two lines of a wrapped name, which sit either
    /// side of the row's own baseline.</summary>
    private const double Wrap = 4.5;

    public static byte[] Build()
    {
        var builder = new PdfDocumentBuilder();
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);

        var one = builder.AddPage(612, 792);
        var two = builder.AddPage(612, 792);

        void Put(PdfPageBuilder page, string text, double x, double y)
        {
            if (text.Length > 0) page.AddText(text, Size, new PdfPoint(x, y), font);
        }

        void Heading(PdfPageBuilder page, double y)
        {
            Put(page, "Name", HName, y);
            Put(page, "Gender", HGender, y);
            Put(page, "Age", HAge, y);
            Put(page, "Birth Date", HBirthday, y);
            Put(page, "Phone Number", HPhone, y);
            Put(page, "Email", HEmail, y);
            Put(page, "Current Unit", HUnit, y);
        }

        void Person(PdfPageBuilder page, double y,
                    string name, string gender, string birthday, string phone, string email, string unit)
        {
            Put(page, name, XName, y);
            Put(page, gender, XGender, y);
            Put(page, birthday, XBirthday, y);
            Put(page, phone, XPhone, y);
            Put(page, email, XEmail, y);
            Put(page, unit, XUnit, y);
        }

        void Footer(PdfPageBuilder page, string number)
        {
            Put(page, "21 Sep 2026", 28.3, 19.2);
            Put(page, "For Church Use Only \u00a9 2026 by Intellectual Reserve, Inc. All rights reserved.", 153.1, 19.2);
            Put(page, number, 578.6, 19.2);
        }

        // ---- page 1 ----
        Put(one, "Organizations and Callings", 28.3, 782.9);
        Put(one, "Test Utah Stake (000000)", 476.1, 782.9);
        Put(one, "Single Adult", 34.3, 730.8);

        // The callings table. Its own Name and Current Unit headings sit at quite
        // different positions from the members table's, which is what breaks any
        // detection that merges the headings found across a whole page.
        Put(one, "Calling", 35.1, 713.6); Put(one, "Name", 225.6, 713.6);
        Put(one, "Sustained", 321.7, 713.6); Put(one, "Set Apart", 408.5, 713.6);
        Put(one, "Current Unit", 483.5, 713.6);
        Put(one, "Stake Single Adult Adviser", 36.6, 694.9);
        Put(one, "Calling Vacant", 227.1, 694.9);
        Put(one, "Stake Single Adult Representative", 36.6, 681.5);
        Put(one, "Pilkington, Hattie", 227.1, 681.5);
        Put(one, "19 Aug 2026", 323.2, 681.5);
        Put(one, "Manti 2nd Ward", 485.0, 681.5);
        Put(one, "Count: 2", 34.3, 663.8);

        Put(one, "Single Adult Members", 34.3, 643.9);
        Heading(one, 626.7);

        const double first = 608.0;
        Person(one, first,
            "Ashdown, Marigold", "F", "17 Jan", "(435) 555-0100", "m.ashdown@example.com", "Manti 2nd Ward");

        // A seven-digit phone and no e-mail, as the report prints some.
        Person(one, first - Step,
            "Quilley, Barnaby", "M", "6 May", "555-0127", "", "Sterling Ward");

        // No phone at all.
        Person(one, first - Step * 2,
            "Crowther, Dell", "F", "30 Jul", "", "d.crowther@example.com", "Manti 4th Ward");

        // A name too long for its column. Its two lines sit either side of the row,
        // so neither of them lines up with any other cell.
        var wrapped = first - Step * 3 - Wrap;
        Put(one, "Featherstonehaugh,", XName, wrapped + Wrap);
        Person(one, wrapped,
            "", "F", "3 Jul", "(435) 555-0142", "w.feather@example.com", "Manti 10th Ward");
        Put(one, "Wilhelmina", XName, wrapped - Wrap);

        Person(one, wrapped - Wrap - Step,
            "Zelmore, Briony", "F", "20 Aug", "(801) 555-0155", "b.zelmore@example.com", "Manti 5th Ward");

        Footer(one, "1");

        // ---- page 2: the heading repeats, and there is no callings table ----
        Put(two, "Organizations and Callings", 28.3, 782.9);
        Put(two, "Test Utah Stake (000000)", 476.1, 782.9);
        Heading(two, 762.0);

        Person(two, 743.3,
            "Wraithwell, Mordecai", "M", "18 Sep", "(435) 555-0170", "m.wraithwell@example.com", "Manti 9th Ward");
        Person(two, 743.3 - Step,
            "Yelverton, Katriona", "F", "6 Aug", "(435) 555-0184", "", "Manti 1st Ward");

        Put(two, "Count: 7", 34.3, 710.0);
        Footer(two, "2");

        return builder.Build();
    }
}
```

- [ ] **Step 2: Write the failing tests**

Create `tests/Courier.Tests/CallingsReportParserTests.cs`:

```csharp
using Courier.Core.Import;

namespace Courier.Tests;

public sealed class CallingsReportParserTests
{
    private static LcrReport Report()
    {
        using var stream = new MemoryStream(SyntheticCallingsReport.Build());
        return LcrReportParser.Parse(stream, "print.pdf");
    }

    [Fact]
    public void Recognises_the_report_it_is()
    {
        var report = Report();
        Assert.Equal("Organizations and Callings", report.Source.FormatName);
        Assert.Equal(2, report.PageCount);
    }

    [Fact]
    public void Reads_one_row_per_person_across_both_pages()
    {
        // The report's own footer says Count: 7. Reading exactly that many back is
        // the strongest check available that no row was dropped or invented.
        var rows = Report().Rows;
        Assert.Equal(7, rows.Count);
        Assert.Equal(
            ["Ashdown, Marigold", "Quilley, Barnaby", "Crowther, Dell",
             "Featherstonehaugh, Wilhelmina", "Zelmore, Briony",
             "Wraithwell, Mordecai", "Yelverton, Katriona"],
            rows.Select(r => r.Name));
    }

    [Fact]
    public void Leaves_out_the_callings_table_printed_above_the_members()
    {
        // "Pilkington, Hattie" is a stake single adult representative, printed in a
        // different table with different columns and no contact details.
        Assert.DoesNotContain(Report().Rows, r => r.Name.Contains("Pilkington", StringComparison.Ordinal));
    }

    [Fact]
    public void Keeps_each_field_in_its_own_column()
    {
        var row = Report().Rows[0];
        Assert.Equal("Ashdown, Marigold", row.Name);   // the Gender glyph does not join it
        Assert.Equal("17 Jan", row.Birthday);
        Assert.Equal("(435) 555-0100", row.Phone);
        Assert.Equal("m.ashdown@example.com", row.Email);
        Assert.Equal("Manti 2nd Ward", row.Unit);
    }

    [Fact]
    public void Reads_nothing_into_the_fields_this_report_does_not_print()
    {
        Assert.All(Report().Rows, r =>
        {
            Assert.Equal("", r.Age);       // the column is printed and never filled
            Assert.Equal("", r.Address);   // there is no column at all
        });
    }

    [Fact]
    public void Rejoins_a_name_wrapped_over_two_lines()
    {
        var row = Report().Rows[3];
        Assert.Equal("Featherstonehaugh, Wilhelmina", row.Name);
        Assert.Equal("3 Jul", row.Birthday);
        Assert.Equal("Manti 10th Ward", row.Unit);
    }

    [Fact]
    public void Leaves_a_missing_field_empty_rather_than_borrowing_the_next_column()
    {
        var rows = Report().Rows;
        Assert.Equal("", rows[1].Email);
        Assert.Equal("555-0127", rows[1].Phone);
        Assert.Equal("", rows[2].Phone);
        Assert.Equal("d.crowther@example.com", rows[2].Email);
    }

    [Fact]
    public void Says_it_does_not_print_addresses_or_ages()
    {
        var carries = Report().Source;
        Assert.False(carries.Carry(ReportFields.Address));
        Assert.False(carries.Carry(ReportFields.Age));
        Assert.True(carries.Carry(ReportFields.Birthday));
        Assert.True(carries.Carry(ReportFields.Email));
        Assert.True(carries.Carry(ReportFields.Phone));
        Assert.True(carries.Carry(ReportFields.Unit));
    }

    [Fact]
    public void Normalises_into_people_the_rest_of_the_app_can_use()
    {
        var people = Report().Rows.Select(r => LcrNormalizer.Normalize(r)).ToList();

        Assert.All(people, p => Assert.NotNull(p.Ward));
        Assert.All(people, p => Assert.NotNull(p.BirthMonth));
        Assert.All(people.Where(p => p.PhoneE164 is not null),
            p => Assert.Matches(@"^\+1\d{10}$", p.PhoneE164!));
        Assert.All(people, p => Assert.Null(p.Address));
        Assert.All(people, p => Assert.Null(p.Age));
    }
}
```

- [ ] **Step 3: Run to verify they fail**

Run: `dotnet test --filter FullyQualifiedName~CallingsReportParserTests`
Expected: FAIL — `SyntheticCallingsReport` does not exist, then real assertion failures once it does.

- [ ] **Step 4: Run them again after adding the fixture from Step 1, and fix what breaks**

Run: `dotnet test --filter FullyQualifiedName~CallingsReportParserTests`
Expected: 9 passed.

Two failures are likely and both are in the fixture, not the parser:

- *Rows merge into one* — the gaps in the fixture are wrong. Every consecutive pair of baselines inside a row must be under 8.0pt apart and every pair between rows over it. Print them with a throwaway test that dumps `PdfTableReader.ReadLines` baselines and check against `Step = 13.4` and `Wrap = 4.5`.
- *`Pilkington, Hattie` appears as a person* — the below-the-heading slice is not taking effect, or the callings heading group is being detected as the members heading. Confirm `ReportFormats.OrganizationsAndCallings.Detect` returns null for the callings heading line, which Task 2 already tests.

- [ ] **Step 5: Run the whole suite**

Run: `dotnet test`
Expected: all pass. The Single Adults tests must be untouched by this.

- [ ] **Step 6: Commit**

```bash
git add tests/Courier.Tests/SyntheticCallingsReport.cs tests/Courier.Tests/CallingsReportParserTests.cs
git commit -F - <<'MSG'
test: read the Organizations and Callings report

A synthetic two-page export in the new layout, built in code like the
other one, carrying everything the parser has to survive: the heading
repeated per page, the stake's callings table above the members table,
a name wrapped and centred on its row, an Age column that is never
filled, a Gender column with nothing behind it, and the footer.

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_0162Q5wUzGeN3fyawg57oWSC
MSG
```

---

### Task 5: An import owns the fields its report prints

**Files:**
- Modify: `src/Courier.Core/Import/ImportPlan.cs:31-42` (`ImportPlan`)
- Modify: `src/Courier.Core/Import/ImportPlanner.cs` (`Plan`, `Diff`)
- Modify: `src/Courier.Data/ImportService.cs:19`
- Modify: `tests/Courier.Tests/ImportPlannerTests.cs` (12 call sites)
- Modify: `tests/Courier.Tests/ImportServiceTests.cs:35`, `tests/Courier.Tests/DirectoryServiceTests.cs:26`

**Interfaces:**
- Consumes: `ReportSource`, `ReportFields`.
- Produces:
  - `ImportPlanner.Plan(IReadOnlyList<NormalizedPerson> incoming, IReadOnlyList<ExistingPerson> existing, ReportSource source)`
  - `ImportPlan(… , IReadOnlyList<string> Warnings, IReadOnlyList<string> Notes)` with `bool HasNotes`

- [ ] **Step 1: Give the test helpers an address and an age**

`tests/Courier.Tests/ImportPlannerTests.cs:7-17` hard-codes `"1 Main"` and `40` into both helpers, so the new tests cannot vary the two fields they are about. Replace both with:

```csharp
    private static NormalizedPerson Incoming(
        string last, string first, string? ward = "Manti 2nd Ward",
        string? email = null, string? phone = null, int month = 3, int day = 4,
        string? address = "1 Main", int? age = 40) =>
        new(last, first, $"{last}, {first}", ward, age, month, day, address,
            email, phone, phone is null ? null : "+14355550100", false, ward ?? "");

    private static ExistingPerson Existing(
        string last, string first, string? ward = "Manti 2nd Ward",
        string? email = null, string? phone = null, bool active = true, int month = 3, int day = 4,
        string? address = "1 Main", int? age = 40) =>
        new(Guid.NewGuid(), last, first, month, day, ward, age, address, email, phone, active);
```

Every existing call keeps working: the defaults are the values that were hard-coded.

- [ ] **Step 2: Write the failing tests**

Add to `tests/Courier.Tests/ImportPlannerTests.cs`:

```csharp
    private static readonly ReportSource SingleAdults =
        new("Single Adults", ReportFields.All);

    private static readonly ReportSource Callings =
        new("Organizations and Callings",
            ReportFields.Unit | ReportFields.Birthday | ReportFields.Email | ReportFields.Phone);

    [Fact]
    public void A_report_that_prints_no_addresses_does_not_clear_the_ones_on_record()
    {
        var existing = Existing("Ashby", "Miriam", address: "812 North 700 East");
        var incoming = Incoming("Ashby", "Miriam", address: null, age: null);

        var plan = ImportPlanner.Plan([incoming], [existing], Callings);

        Assert.Empty(plan.Updated);
        Assert.Equal(1, plan.Unchanged);
    }

    [Fact]
    public void A_report_that_does_print_addresses_clears_one_it_leaves_blank()
    {
        // The field was reported, and reported as empty. That is a change.
        var existing = Existing("Ashby", "Miriam", address: "812 North 700 East");
        var incoming = Incoming("Ashby", "Miriam", address: null);

        var plan = ImportPlanner.Plan([incoming], [existing], SingleAdults);

        var change = Assert.Single(Assert.Single(plan.Updated).Changes);
        Assert.Equal("Address", change.Field);
        Assert.Null(change.To);
    }

    [Fact]
    public void A_report_that_prints_no_ages_does_not_clear_the_ones_on_record()
    {
        var existing = Existing("Ashby", "Miriam", age: 86);
        var incoming = Incoming("Ashby", "Miriam", address: null, age: null);

        var plan = ImportPlanner.Plan([incoming], [existing], Callings);

        Assert.Empty(plan.Updated);
    }

    [Fact]
    public void A_report_still_updates_the_fields_it_does_print()
    {
        var existing = Existing("Ashby", "Miriam", ward: "Manti 2nd Ward", address: "812 North 700 East");
        var incoming = Incoming("Ashby", "Miriam", ward: "Sterling Ward", address: null, age: null);

        var plan = ImportPlanner.Plan([incoming], [existing], Callings);

        var change = Assert.Single(Assert.Single(plan.Updated).Changes);
        Assert.Equal("Ward", change.Field);
    }

    [Fact]
    public void The_plan_says_which_report_it_read_and_what_that_report_leaves_out()
    {
        var plan = ImportPlanner.Plan([Incoming("Ashby", "Miriam")], [], Callings);

        var note = Assert.Single(plan.Notes);
        Assert.Contains("Organizations and Callings", note, StringComparison.Ordinal);
        Assert.Contains("addresses and ages", note, StringComparison.Ordinal);
        Assert.Contains("kept", note, StringComparison.Ordinal);
        Assert.True(plan.HasNotes);
    }

    [Fact]
    public void A_report_that_prints_everything_says_only_which_report_it_is()
    {
        var plan = ImportPlanner.Plan([Incoming("Ashby", "Miriam")], [], SingleAdults);

        Assert.Equal("Read as Single Adults.", Assert.Single(plan.Notes));
    }
```

- [ ] **Step 3: Run to verify they fail**

Run: `dotnet test --filter FullyQualifiedName~ImportPlannerTests`
Expected: FAIL to compile — `Plan` takes two arguments.

- [ ] **Step 4: Add `Notes` to `ImportPlan`**

In `src/Courier.Core/Import/ImportPlan.cs`, change the record:

```csharp
/// <summary>What an import would do, worked out before anything is written.</summary>
public sealed record ImportPlan(
    IReadOnlyList<NormalizedPerson> Added,
    IReadOnlyList<PersonUpdate> Updated,
    IReadOnlyList<ExistingPerson> Deactivated,
    IReadOnlyList<PersonReturn> Reactivated,
    int Unchanged,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<string> Notes)
{
    public int TotalInFile => Added.Count + Updated.Count + Reactivated.Count + Unchanged;
    public bool HasWarnings => Warnings.Count > 0;

    /// <summary>Notes are not warnings. Nothing is wrong; the user is being told
    /// something about the report they chose.</summary>
    public bool HasNotes => Notes.Count > 0;
}
```

- [ ] **Step 5: Teach `ImportPlanner` which fields it may touch**

In `src/Courier.Core/Import/ImportPlanner.cs`:

Change the signature and the `Record`/`Diff` calls:

```csharp
    public static ImportPlan Plan(
        IReadOnlyList<NormalizedPerson> incoming,
        IReadOnlyList<ExistingPerson> existing,
        ReportSource source)
    {
```

```csharp
        void Record(ExistingPerson match, NormalizedPerson person)
        {
            var changes = Diff(match, person, source);
            if (!match.IsActive) reactivated.Add(new PersonReturn(match, person));
            else if (changes.Count > 0) updated.Add(new PersonUpdate(match, person, changes));
            else unchanged++;
        }
```

Replace `Diff`:

```csharp
    /// <summary>What this report would change about somebody already on record.
    ///
    /// Only the fields the report actually prints are compared. A report with no
    /// Address column has not said anything about anyone's address, and an import
    /// from it must not blank the ones a report that does print them recorded.</summary>
    private static IReadOnlyList<FieldChange> Diff(ExistingPerson e, NormalizedPerson p, ReportSource source)
    {
        var changes = new List<FieldChange>();
        void Compare(ReportFields field, string name, string? from, string? to)
        {
            if (!source.Carry(field)) return;
            if (!string.Equals(from ?? "", to ?? "", StringComparison.OrdinalIgnoreCase))
                changes.Add(new FieldChange(name, from, to));
        }

        Compare(ReportFields.Unit, "Ward", e.Ward, p.Ward);
        Compare(ReportFields.Age, "Age", e.Age?.ToString(), p.Age?.ToString());
        Compare(ReportFields.Birthday, "Birthday",
            Birthday(e.BirthMonth, e.BirthDay), Birthday(p.BirthMonth, p.BirthDay));
        Compare(ReportFields.Address, "Address", e.Address, p.Address);
        Compare(ReportFields.Email, "Email", e.Email, p.Email);
        Compare(ReportFields.Phone, "Phone", e.Phone, p.PhoneRaw);
        return changes;
    }
```

Compose the notes just before the return, and pass them:

```csharp
        return new ImportPlan(added, updated, deactivated, reactivated, unchanged, warnings, Notes(source));
```

And add:

```csharp
    /// <summary>What to tell the user about the report they chose. Composed here
    /// rather than in the view, because views do not compute.</summary>
    private static IReadOnlyList<string> Notes(ReportSource source)
    {
        var missing = new List<string>();
        if (!source.Carry(ReportFields.Unit)) missing.Add("wards");
        if (!source.Carry(ReportFields.Address)) missing.Add("addresses");
        if (!source.Carry(ReportFields.Age)) missing.Add("ages");
        if (!source.Carry(ReportFields.Birthday)) missing.Add("birthdays");
        if (!source.Carry(ReportFields.Email)) missing.Add("e-mail addresses");
        if (!source.Carry(ReportFields.Phone)) missing.Add("phone numbers");

        return missing.Count == 0
            ? [$"Read as {source.FormatName}."]
            : [$"Read as {source.FormatName}. This report does not list {Listed(missing)}, " +
               "so the ones already recorded are kept."];
    }

    private static string Listed(IReadOnlyList<string> items) => items.Count switch
    {
        1 => items[0],
        2 => $"{items[0]} and {items[1]}",
        _ => $"{string.Join(", ", items.Take(items.Count - 1))} and {items[^1]}",
    };
```

- [ ] **Step 6: Pass the source from `ImportService`**

In `src/Courier.Data/ImportService.cs`, line 19:

```csharp
        return (report, ImportPlanner.Plan(incoming, existing, report.Source));
```

- [ ] **Step 7: Update the remaining call sites**

Every existing `ImportPlanner.Plan(a, b)` becomes `ImportPlanner.Plan(a, b, SingleAdults)` — the behaviour those tests assert is the behaviour of a report that prints everything. Do **not** give `Plan` a default value for the parameter: a silent default is how the rule gets skipped at a call site added later.

The eleven call sites in `tests/Courier.Tests/ImportPlannerTests.cs` need only the extra argument; `SingleAdults` is already declared there by Step 2.

`tests/Courier.Tests/ImportServiceTests.cs` and `tests/Courier.Tests/DirectoryServiceTests.cs` already have a `SingleAdults` static — Task 3 Step 5a added it when `LcrReport` gained its fifth field. Reuse it; do not declare a second one. Change `ImportAsync` in `ImportServiceTests.cs`:

```csharp
        var plan = ImportPlanner.Plan(people, existing, SingleAdults);
```

and `SeedAsync` in `DirectoryServiceTests.cs`:

```csharp
        var plan = ImportPlanner.Plan(people, existing, SingleAdults);
```

- [ ] **Step 8: Run the whole suite**

Run: `dotnet test`
Expected: all pass, including the six new planner tests.

- [ ] **Step 9: Commit**

```bash
git add src/Courier.Core/Import/ImportPlan.cs src/Courier.Core/Import/ImportPlanner.cs src/Courier.Data/ImportService.cs tests/Courier.Tests/ImportPlannerTests.cs tests/Courier.Tests/ImportServiceTests.cs tests/Courier.Tests/DirectoryServiceTests.cs
git commit -F - <<'MSG'
feat: an import owns the fields its report prints

The rule was that an import owns ward, age, birthday, address and the
printed e-mail and phone, and overwrites them every time. That was
written when there was one report and it printed all six. Applied to a
report with no Address column it wipes every address in the directory,
and they cannot be got back without the other export.

So the planner now compares only the fields the report prints, and the
plan carries a note saying which report was read and what it leaves out.

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_0162Q5wUzGeN3fyawg57oWSC
MSG
```

---

### Task 6: The write agrees with the plan

**Files:**
- Modify: `src/Courier.Data/ImportService.cs:23-133` (`ApplyAsync`, `ApplyFields`, `SyncLcrContactAsync`)
- Test: `tests/Courier.Tests/ImportServiceTests.cs`

**Interfaces:**
- Consumes: `LcrReport.Source`, `ReportSource.Carry`.
- Produces: no public signature change — `ApplyAsync(report, plan, today, cancellation)` is unchanged, because the report it already takes now carries the source.

- [ ] **Step 1: Let the test helpers vary an address, and build a report from any source**

In `tests/Courier.Tests/ImportServiceTests.cs`, `Person` (line 22) hard-codes `"1 Main"`. Replace it with:

```csharp
    private static NormalizedPerson Person(
        string last, string first, string? email = null, string? phone = null,
        string ward = "Manti 2nd Ward", string? address = "1 Main") =>
        new(last, first, $"{last}, {first}", ward, 40, 3, 4, address,
            email, phone, phone is null ? null : "+1435555" + phone[^4..], false, ward);
```

Add beside the `SingleAdults` static that Task 5 Step 7 introduced:

```csharp
    private static readonly ReportSource Callings = new("Organizations and Callings",
        ReportFields.Unit | ReportFields.Birthday | ReportFields.Email | ReportFields.Phone);

    /// <summary>A report from a given source, for the cases that turn on what the
    /// report printed rather than on what is in it.</summary>
    private static LcrReport From(ReportSource source) =>
        new([], 9, "print.pdf", new string('b', 64), source);
```

- [ ] **Step 2: Write the failing tests**

Add to `tests/Courier.Tests/ImportServiceTests.cs`:

```csharp
    [Fact]
    public async Task A_report_that_prints_no_addresses_leaves_the_stored_one_alone()
    {
        using var db = Open();

        // A Single Adults import, which does print an address.
        await ImportAsync(db, [Person("Ashby", "Miriam", address: "812 North 700 East")], Today);
        Assert.Equal("812 North 700 East", (await db.People.SingleAsync()).Address);

        // Then an Organizations and Callings import, which has no Address column at
        // all. Reaching for the other report must not cost the directory its
        // addresses.
        var existing = await DirectoryService.ExistingPeople(db).ToListAsync();
        var plan = ImportPlanner.Plan(
            [Person("Ashby", "Miriam", ward: "Sterling Ward", address: null)], existing, Callings);
        await new ImportService(db).ApplyAsync(From(Callings), plan, Later);

        var person = await db.People.SingleAsync();
        Assert.Equal("812 North 700 East", person.Address);
        Assert.Equal("Sterling Ward", person.Ward);   // the field it did print still lands
        Assert.Equal(Later, person.LastSeenOn);
    }

    [Fact]
    public async Task A_report_that_prints_no_phone_numbers_leaves_the_stored_one_alone()
    {
        using var db = Open();
        await ImportAsync(db, [Person("Quilley", "Barnaby", phone: "555-0127")], Today);

        // A report that prints a name and a ward and nothing else. The ward changes,
        // so this person is updated and the write runs — which is what makes this a
        // test of the guard rather than of doing nothing.
        var namesOnly = new ReportSource("Names only", ReportFields.Unit);
        var existing = await DirectoryService.ExistingPeople(db).ToListAsync();
        var plan = ImportPlanner.Plan(
            [Person("Quilley", "Barnaby", ward: "Sterling Ward")], existing, namesOnly);
        await new ImportService(db).ApplyAsync(From(namesOnly), plan, Later);

        var person = await db.People.Include(p => p.ContactPoints).SingleAsync();
        Assert.Equal("Sterling Ward", person.Ward);
        Assert.Equal("555-0127", person.LcrPhone);

        // The contact point was not touched, so it was not re-stamped as seen.
        var contact = Assert.Single(person.ContactPoints);
        Assert.Equal("555-0127", contact.Value);
        Assert.Equal(Today, contact.LastSeenInLcrOn);
    }
```

- [ ] **Step 3: Run to verify they fail**

Run: `dotnet test --filter FullyQualifiedName~ImportServiceTests`
Expected: FAIL — `person.Address` is null and `contact.LastSeenInLcrOn` is `Later`, because `ApplyFields` and `SyncLcrContactAsync` write unconditionally.

- [ ] **Step 4: Make the write respect what the report prints**

In `src/Courier.Data/ImportService.cs`, thread `report.Source` into the three places that write:

```csharp
            ApplyFields(person, incoming, report.Source, today);
```
```csharp
            ApplyFields(person, update.Incoming, report.Source, today);
```
```csharp
            ApplyFields(person, returning.Incoming, report.Source, today);
```
```csharp
            await SyncLcrContactAsync(person, incoming, report.Source, today, cancellation);
```
(and the same for the `update.Incoming` and `returning.Incoming` calls)

Replace `ApplyFields`:

```csharp
    /// <summary>Copies the fields this report prints. Preferred channel and notes are
    /// untouched by design, and so is any field the report has no column for: it has
    /// said nothing about them, and silence is not an instruction to blank them.</summary>
    private static void ApplyFields(Person person, NormalizedPerson incoming, ReportSource source, DateOnly today)
    {
        person.DisplayName = incoming.DisplayName;
        if (source.Carry(ReportFields.Unit)) person.Ward = incoming.Ward;
        if (source.Carry(ReportFields.Age)) person.Age = incoming.Age;
        if (source.Carry(ReportFields.Birthday))
        {
            person.BirthMonth = incoming.BirthMonth;
            person.BirthDay = incoming.BirthDay;
        }
        if (source.Carry(ReportFields.Address)) person.Address = incoming.Address;
        if (source.Carry(ReportFields.Email)) person.LcrEmail = incoming.Email;
        if (source.Carry(ReportFields.Phone)) person.LcrPhone = incoming.PhoneRaw;

        person.LastSeenOn = today;
        person.UpdatedAt = DateTimeOffset.UtcNow;

        // The export now carries them, so they are LCR's to manage from here.
        person.Source = PersonSource.Lcr;
    }
```

And `SyncLcrContactAsync`:

```csharp
    private async Task SyncLcrContactAsync(
        Person person, NormalizedPerson incoming, ReportSource source, DateOnly today, CancellationToken cancellation)
    {
        if (source.Carry(ReportFields.Email))
            await UpsertAsync(person, ContactKind.Email, incoming.Email, Normalize(incoming.Email),
                false, today, cancellation);
        if (source.Carry(ReportFields.Phone))
            await UpsertAsync(person, ContactKind.Phone, incoming.PhoneRaw, incoming.PhoneE164,
                incoming.AreaCodeAssumed, today, cancellation);
    }
```

- [ ] **Step 5: Run to verify they pass**

Run: `dotnet test --filter FullyQualifiedName~ImportServiceTests`
Expected: all pass.

- [ ] **Step 6: Run the whole suite**

Run: `dotnet test`
Expected: all pass. In particular the existing tests asserting an import never touches preferred channel, notes or hand-added contact details must be unchanged and green.

- [ ] **Step 7: Commit**

```bash
git add src/Courier.Data/ImportService.cs tests/Courier.Tests/ImportServiceTests.cs
git commit -F - <<'MSG'
fix: do not write fields the report did not print

The planner already skips them, so without this the Import screen
promises that an address is kept and the write clears it a moment
later. The plan and the write have to agree.

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_0162Q5wUzGeN3fyawg57oWSC
MSG
```

---

### Task 7: The Import screen says which report it read

**Files:**
- Modify: `src/Courier.App/ViewModels/ImportViewModel.cs`
- Modify: `src/Courier.App/Views/ImportView.axaml:10-12` (the lede) and `:40` and `:81-90` (the grid rows)
- Test: `tests/Courier.Tests/UserInterfaceTests.cs`

**Interfaces:**
- Consumes: `ImportPlan.Notes`, `ImportPlan.HasNotes`, `LcrReport.Source.FormatName`.
- Produces: `ImportViewModel.Notes` (`ObservableCollection<string>`) and `ImportViewModel.HasNotes`.

- [ ] **Step 1: Add `Notes` to the view model**

In `src/Courier.App/ViewModels/ImportViewModel.cs`, beside the existing `Warnings`:

```csharp
    public ObservableCollection<string> Notes { get; } = [];
    public bool HasNotes => Notes.Count > 0;
```

In `LoadAsync`, clear it alongside `Warnings` (line 54) and fill it after the warnings loop (line 86):

```csharp
        Notes.Clear();
```
```csharp
            foreach (var n in plan.Notes) Notes.Add(n);
```

Raise the change notification beside the existing one (line 93):

```csharp
            OnPropertyChanged(nameof(HasWarnings));
            OnPropertyChanged(nameof(HasNotes));
```

Do the same three-line treatment in `ApplyAsync` (lines 136-138) and `Cancel` (lines 158-160): `Notes.Clear();` and `OnPropertyChanged(nameof(HasNotes));`.

Add the format to the log line (line 88), which is safe — a report's name identifies nobody:

```csharp
            Log.Record("import.read", Log.Details(
                ("file", report.FileName), ("format", report.Source.FormatName),
                ("pages", report.PageCount), ("rows", report.Rows.Count),
```

- [ ] **Step 2: Add the notes strip to the view**

In `src/Courier.App/Views/ImportView.axaml`, the inner grid at line 40 gains a row. Change:

```xml
    <Grid Grid.Row="1" IsVisible="{Binding HasFile}" RowDefinitions="Auto,Auto,Auto,Auto,*,Auto,Auto">
```

to:

```xml
    <Grid Grid.Row="1" IsVisible="{Binding HasFile}" RowDefinitions="Auto,Auto,Auto,Auto,Auto,*,Auto,Auto">
```

Insert, immediately before the existing `Warnings` `ItemsControl` (currently line 81):

```xml
      <!-- Which report this is, and what it does not carry. Not a warning: nothing
           is wrong, and it must not be dressed as though something is. -->
      <ItemsControl Grid.Row="3" ItemsSource="{Binding Notes}" Margin="0,12,0,0">
        <ItemsControl.ItemTemplate>
          <DataTemplate>
            <Border Classes="notice" Margin="0,0,0,7">
              <TextBlock Text="{Binding}" FontSize="12.5" TextWrapping="Wrap" MaxWidth="720"
                         Foreground="{DynamicResource Ink2}"/>
            </Border>
          </DataTemplate>
        </ItemsControl.ItemTemplate>
      </ItemsControl>
```

Then renumber the rows below it: the `Warnings` `ItemsControl` becomes `Grid.Row="4"`, the change-list `Border` becomes `Grid.Row="5"`, the "People who drop out…" `TextBlock` becomes `Grid.Row="6"`, and the button `StackPanel` becomes `Grid.Row="7"`.

- [ ] **Step 3: Stop the lede naming one report**

Change the lede (lines 11-12) to:

```xml
    <TextBlock Classes="lede" Margin="0,4,0,0"
               Text="Export the Single Adults report, or the Organizations and Callings report, from LCR as a PDF and open it here. Courier works out which one it is. Nothing is saved until you have looked over the changes."/>
```

- [ ] **Step 4: Write the failing UI test**

`UserInterfaceTests` builds the real window headless, which is the only way a mistyped binding or a renumbered `Grid.Row` gets caught before a user finds it. Add, using the file's existing `InWindow` helper:

```csharp
    [Fact]
    public Task The_import_screen_shows_which_report_was_read() => InWindow((window, model) =>
    {
        model.ShowImport();
        var import = (ImportViewModel)model.Current;
        Assert.False(import.HasNotes);

        // The notes strip lives inside the grid that only appears once a file is open.
        import.HasFile = true;
        import.Notes.Add("Read as Organizations and Callings. This report does not list " +
                         "addresses and ages, so the ones already recorded are kept.");
        Assert.True(import.HasNotes);

        // Render it. A note that binds but never appears is the failure worth catching.
        Dispatcher.UIThread.RunJobs();
        window.Measure(window.ClientSize);
        window.Arrange(new Rect(window.ClientSize));

        Assert.Contains(
            window.GetVisualDescendants().OfType<TextBlock>(),
            t => t.Text is not null
              && t.Text.Contains("Organizations and Callings", StringComparison.Ordinal));

        return Task.CompletedTask;
    }, _folder);
```

`Rect`, `Dispatcher`, `TextBlock` and `GetVisualDescendants` are all already imported at the top of that file. `HasFile` is settable because `[ObservableProperty] private bool _hasFile` generates a public setter.

- [ ] **Step 5: Run the whole suite**

Run: `dotnet test`
Expected: all pass. A renumbering mistake in the XAML shows up as a headless-render failure in `UserInterfaceTests`, not as a compile error — if the Import screen test fails, re-check that the eight `Grid.Row` values in the inner grid run 0..7 with no duplicates.

- [ ] **Step 6: Commit**

```bash
git add src/Courier.App/ViewModels/ImportViewModel.cs src/Courier.App/Views/ImportView.axaml tests/Courier.Tests/UserInterfaceTests.cs
git commit -F - <<'MSG'
feat: say on the Import screen which report was read

Two reports now import, they carry different fields, and which one a
file is decides what the import will and will not overwrite. Someone
choosing a file should not have to know that; the screen says it.

The note is styled as a note. Nothing is wrong when a report has no
address column, and dressing it as a warning would teach people to
ignore the warnings that matter.

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_0162Q5wUzGeN3fyawg57oWSC
MSG
```

---

### Task 8: Measure against the real export

**Files:**
- Modify: `tests/Courier.Tests/TestPaths.cs`
- Modify: `tests/Courier.Tests/RealReportTests.cs`
- Create: `tests/Courier.Tests/Fixtures/README.md`
- Copy (not committed): `C:\Users\jonathan.allen\Downloads\print.pdf` → `tests/Courier.Tests/Fixtures/private/manti-callings.pdf`

**Interfaces:**
- Consumes: `LcrReportParser.Parse(string)`, `ImportService`, `DirectoryService`, `Wards.Snap`.
- Produces: `TestPaths.RealCallingsReport`, `RequiresRealCallingsReportAttribute`.

- [ ] **Step 1: Put the real export where it is gitignored, and confirm it is**

```bash
mkdir -p tests/Courier.Tests/Fixtures/private
cp "/c/Users/jonathan.allen/Downloads/print.pdf" tests/Courier.Tests/Fixtures/private/manti-callings.pdf
git check-ignore -v tests/Courier.Tests/Fixtures/private/manti-callings.pdf
git status --porcelain
```

Expected: `check-ignore` names the `.gitignore` rule, and `git status` does **not** list the PDF. If it does, stop and fix `.gitignore` before going further.

- [ ] **Step 2: Write the committed README that tells the next person what goes there**

Create `tests/Courier.Tests/Fixtures/README.md`:

```markdown
# Test fixtures

`private/` is gitignored and holds real LCR exports. They carry home addresses,
phone numbers and birthdays for hundreds of people and must never be committed —
nor must anything derived from one. Fixtures that are committed are synthetic and
built in code, by `SyntheticReport` and `SyntheticCallingsReport`.

The tests that need a real export skip themselves when it is absent, so the suite
passes on a machine that has never seen the directory.

| File | Report | Export it from |
|------|--------|----------------|
| `private/manti-singles.pdf`  | Single Adults              | LCR → Single Adults → print to PDF |
| `private/manti-callings.pdf` | Organizations and Callings | LCR → Organizations and Callings → print to PDF |

Delete them when you are finished with them.
```

- [ ] **Step 3: Add the path and the skip attribute**

In `tests/Courier.Tests/TestPaths.cs`, beside `RealReport`:

```csharp
    /// <summary>A real Organizations and Callings export, if one has been placed in
    /// the gitignored folder. Null on a machine that has never seen it.</summary>
    public static string? RealCallingsReport
    {
        get
        {
            if (RepoRoot is null) return null;
            var path = Path.Combine(RepoRoot, "tests", "Courier.Tests", "Fixtures", "private", "manti-callings.pdf");
            return File.Exists(path) ? path : null;
        }
    }
```

And, beside `RequiresRealReportAttribute`:

```csharp
/// <summary>Marks a test that needs a real Organizations and Callings export and
/// skips it when none is present.</summary>
public sealed class RequiresRealCallingsReportAttribute : FactAttribute
{
    public RequiresRealCallingsReportAttribute()
    {
        if (TestPaths.RealCallingsReport is null)
            Skip = "No Organizations and Callings export in tests/Courier.Tests/Fixtures/private/ — see the README in tests/Courier.Tests/Fixtures/.";
    }
}
```

- [ ] **Step 4: Write the failing tests**

Add to `tests/Courier.Tests/RealReportTests.cs`:

```csharp
/// <summary>Measures the parser against a real Organizations and Callings export.
/// The numbers here are what decide whether an import of that report can be
/// trusted, so they are asserted, not just printed.</summary>
public sealed class RealCallingsReportTests(ITestOutputHelper output)
{
    [RequiresRealCallingsReport]
    public void Reads_every_person_from_the_report()
    {
        var report = LcrReportParser.Parse(TestPaths.RealCallingsReport!);

        var phone = new Regex(@"^(\(\d{3}\)\s*)?\d{3}-\d{4}$");
        var email = new Regex(@"^[^@\s]+@[^@\s]+\.[A-Za-z]{2,}$");
        var birthday = new Regex(@"^\d{1,2} [A-Z][a-z]{2}$");

        int Bad(Func<LcrRow, string> field, Regex shape) =>
            report.Rows.Count(r => field(r).Length > 0 && !shape.IsMatch(field(r)));

        output.WriteLine($"format      {report.Source.FormatName}");
        output.WriteLine($"pages       {report.PageCount}");
        output.WriteLine($"people      {report.Rows.Count}");
        output.WriteLine($"bad phone   {Bad(r => r.Phone, phone)}");
        output.WriteLine($"bad email   {Bad(r => r.Email, email)}");
        output.WriteLine($"bad bday    {Bad(r => r.Birthday, birthday)}");
        output.WriteLine($"no email    {report.Rows.Count(r => r.Email.Length == 0)}");
        output.WriteLine($"no phone    {report.Rows.Count(r => r.Phone.Length == 0)}");
        foreach (var r in report.Rows.Where(r => r.Phone.Length > 0 && !phone.IsMatch(r.Phone)).Take(5))
            output.WriteLine($"  PHONE '{r.Phone}'");
        foreach (var r in report.Rows.Where(r => r.Email.Length > 0 && !email.IsMatch(r.Email)).Take(5))
            output.WriteLine($"  EMAIL '{r.Email}'");

        Assert.Equal("Organizations and Callings", report.Source.FormatName);
        Assert.Equal(9, report.PageCount);

        // The report prints "Count: 428" in its own footer. Reading exactly that many
        // people back is the strongest check available that no row was dropped.
        Assert.Equal(428, report.Rows.Count);
        Assert.Equal(0, Bad(r => r.Phone, phone));
        Assert.Equal(0, Bad(r => r.Birthday, birthday));
        Assert.Equal(0, Bad(r => r.Email, email));
    }

    [RequiresRealCallingsReport]
    public void Reads_nothing_into_the_columns_this_report_does_not_print()
    {
        var report = LcrReportParser.Parse(TestPaths.RealCallingsReport!);

        Assert.All(report.Rows, r => Assert.Equal("", r.Address));
        Assert.All(report.Rows, r => Assert.Equal("", r.Age));
        Assert.False(report.Source.Carry(ReportFields.Address));
        Assert.False(report.Source.Carry(ReportFields.Age));
    }

    [RequiresRealCallingsReport]
    public void Leaves_out_the_stake_callings_table()
    {
        // Page 1 prints the stake's Single Adult callings above the members table,
        // including rows reading "Calling Vacant" and names with no contact details.
        var report = LcrReportParser.Parse(TestPaths.RealCallingsReport!);
        Assert.DoesNotContain(report.Rows, r => r.Name.Contains("Vacant", StringComparison.Ordinal));
        Assert.All(report.Rows, r => Assert.Contains(',', r.Name));
    }

    [RequiresRealCallingsReport]
    public void Every_unit_snaps_onto_a_known_ward()
    {
        var report = LcrReportParser.Parse(TestPaths.RealCallingsReport!);
        var unsnapped = report.Rows.Where(r => Wards.Snap(r.Unit) is null).ToList();
        foreach (var r in unsnapped.Take(10)) output.WriteLine($"  UNIT '{r.Unit}'");
        Assert.Empty(unsnapped);
    }
}
```

And a second import test class, following `RealImportTests`:

```csharp
/// <summary>Runs a genuine Organizations and Callings export all the way through the
/// importer into a real database, then imports it again to prove it settles.</summary>
public sealed class RealCallingsImportTests(ITestOutputHelper output) : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"courier-callings-{Guid.NewGuid():N}.db");

    private Courier.Data.CourierDbContext Open()
    {
        var db = Courier.Data.CourierDatabase.Open(_path);
        Microsoft.EntityFrameworkCore.RelationalDatabaseFacadeExtensions.Migrate(db.Database);
        return db;
    }

    [RequiresRealCallingsReport]
    public async Task The_whole_directory_imports_and_then_imports_again_unchanged()
    {
        using var db = Open();
        var service = new Courier.Data.ImportService(db);

        var (report, plan) = await service.PrepareAsync(TestPaths.RealCallingsReport!);
        output.WriteLine($"first run: {plan.Added.Count} added, {plan.Warnings.Count} warnings");
        output.WriteLine($"note: {string.Join(" ", plan.Notes)}");

        Assert.Equal(428, plan.Added.Count);
        Assert.Empty(plan.Deactivated);
        Assert.Contains(plan.Notes, n => n.Contains("Organizations and Callings", StringComparison.Ordinal));

        var run = await service.ApplyAsync(report, plan, new DateOnly(2026, 9, 21));
        Assert.Equal(428, run.AddedCount);

        var people = await new Courier.Data.DirectoryService(db).RecipientsAsync();
        Assert.Equal(428, people.Count);

        var withPhone = people.Count(p => p.Phone is not null);
        var withEmail = people.Count(p => p.Email is not null);
        output.WriteLine($"reachable by phone: {withPhone}, by email: {withEmail}");
        Assert.True(withPhone > 300, $"only {withPhone} people got a usable phone number");
        Assert.True(withEmail > 200, $"only {withEmail} people got an email address");

        Assert.All(people.Where(p => p.Phone is not null),
            p => Assert.Matches(@"^\+1\d{10}$", p.Phone!));

        var (_, second) = await service.PrepareAsync(TestPaths.RealCallingsReport!);
        output.WriteLine($"second run: {second.Added.Count} added, {second.Updated.Count} updated, " +
                         $"{second.Deactivated.Count} deactivated, {second.Unchanged} unchanged");

        Assert.Empty(second.Added);
        Assert.Empty(second.Updated);
        Assert.Empty(second.Deactivated);
        Assert.Equal(428, second.Unchanged);
    }

    public void Dispose()
    {
        try { if (File.Exists(_path)) File.Delete(_path); }
        catch (IOException) { /* a temp file left behind harms nothing */ }
    }
}
```

- [ ] **Step 5: Run them**

Run: `dotnet test --filter FullyQualifiedName~RealCallings`
Expected: all pass, with 9 pages and 428 people.

If the count is not 428, dump the names either side of the discrepancy with `output.WriteLine` and compare against the eight rows in the real file whose names wrap — they are the rows most likely to be lost or doubled. If `withEmail` is under 200, check the e-mail column's right edge: e-mails run to x=477.3 and the Unit column starts at 480.3, so there is only 3pt of clearance.

- [ ] **Step 6: Run the whole suite and confirm nothing leaked into git**

Run: `dotnet test`
Run: `git status --porcelain`
Expected: all tests pass; the PDF is not listed.

- [ ] **Step 7: Commit**

```bash
git add tests/Courier.Tests/TestPaths.cs tests/Courier.Tests/RealReportTests.cs tests/Courier.Tests/Fixtures/README.md
git status --porcelain
git commit -F - <<'MSG'
test: measure the new reader against a real export

428 people over 9 pages, matching the report's own footer, with every
unit snapping onto a known ward and a re-import that changes nothing.
Skipped on a machine with no export, like the existing set.

The export itself stays in the gitignored folder. The README beside it
is committed, because the next person needs to know what goes there and
why it does not.

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_0162Q5wUzGeN3fyawg57oWSC
MSG
```

- [ ] **Step 8: Verify the commit carries no PDF**

Run: `git show --stat HEAD`
Expected: three files, none of them a `.pdf`.

---

### Task 9: Write down what changed and why

**Files:**
- Modify: `docs/architecture.md` ("Reading the report", "What an import may and may not touch")
- Modify: `CLAUDE.md` ("Import rules")

**Interfaces:**
- Consumes: nothing. This task ships no code.

- [ ] **Step 1: Rewrite "Reading the report" in `docs/architecture.md`**

Replace the section from `## Reading the report` down to (but not including) `## What an import may and may not touch` with:

```markdown
## Reading the report

This is the hard part, and it is worth knowing why before changing any of it.

An LCR report is a rendered HTML table. Its PDF contains no rules, no cell
boundaries, and no reading order that matches the table — pulling the text out
linearly interleaves the columns into nonsense. The only structure available is where
each glyph sits on the page.

Two measured facts drive `PdfTableReader`, and both hold for every report seen so far:

* **Cells are centred vertically within their row, not aligned to its top.** A cell
  wrapping to three lines sits at the row's centre plus and minus one line height; a
  two-line cell sits at plus and minus half of one. So a row's baselines land on a grid
  of *half* the line height, and no cell's first line lines up with any other cell's.
  This is why a row cannot be found by looking for where its name begins.
* **Rows are separated by more vertical space than the lines inside them.** How much
  more differs from report to report, so the ratio belongs to the format rather than to
  the reader — see `IReportFormat.RowBreakFactor`. It is scaled by the text size rather
  than by the other gaps on the page, because a relative rule cannot tell a page of
  single-line rows apart from one tall row.

### One reader, several reports

Courier reads two reports today and the list is meant to grow, so everything that
differs between them lives in a format rather than in the parser. A format states the
phrases that name its columns and the order they come in, the row-break factor above,
the lines that are page furniture, and which fields it actually prints.

`LcrReportParser` offers each page to every registered format; the first to recognise
a heading drives the rest of the document. `ReportFormats` holds the registry and both
descriptors. A third report of the same shape — a table whose own headings give the
column positions — is a short record. One of a different shape implements
`IReportFormat` and shares only `PdfTableReader`.

    Single Adults                 8.9pt text, rows 2.6x apart, wraps 1.44x
                                  name, email, phone, unit, age, birthday, address
    Organizations and Callings    8.0pt text, rows 1.68x apart, wraps 0.56x
                                  name, gender, age, birth date, phone, email, unit

Details that each cost a debugging session:

* Glyph positions come from the **advance box** (`StartBaseLine`/`EndBaseLine`), not the
  ink bounding box. Ink bounds leave gaps inside a word wide enough to look like spaces,
  which turns `17 Jan` into `1 7 Jan` and breaks every birthday.
* Space glyphs are **kept**, because the reports write real spaces and honouring them
  beats inferring every space from a gap.
* Column positions are **read from the report's own headings**, never hard-coded, and
  only from lines carrying two or more headings. The Single Adults toolbar line reads
  "Group by Unit Edit Report", and matching its "Unit" puts the columns out of order and
  folds four fields into one. The detected positions must increase left to right or the
  layout is rejected.
* Headings are detected **one row group at a time**, never merged across a page. Page 1
  of Organizations and Callings carries a `Name` heading in two different tables, at
  x=225.6 and x=54.1; merged, the columns run right to left and nothing is readable.
* Body lines are taken **only from below the heading**. That same page 1 prints the
  stake's Single Adult callings above the members table, and those rows have names in
  them.
* A column may be found and its contents **discarded**. Organizations and Callings
  prints Gender between the name and the age. Courier has no use for it, but without an
  edge there the `F` joins the name and everyone reads "Ashdown, Marigold F".

A PDF that no format recognises fails loudly with `LcrReportException`, naming the
reports Courier does know, rather than importing nonsense.
```

- [ ] **Step 2: Amend "What an import may and may not touch" in `docs/architecture.md`**

Replace the bullet pair in that section with:

```markdown
* **An import owns the fields its report prints** — ward, age, birthday, address, and
  the e-mail and phone as printed. These are overwritten every time.
* **Courier owns** the preferred channel, notes, and any contact details added by hand.
  No import may touch them. There are tests for this.

The first clause used to read "an import owns ward, age, birthday, address …" flatly,
which was true while there was one report and it printed all six. Organizations and
Callings prints no address at all and never fills its age column, and blanking a
directory's addresses because the user reached for the other report is not a trade
anyone would make. So a format declares what it prints, `ImportPlanner` diffs only
those fields, `ImportService` writes only those fields, and the Import screen says
which report was read and what it leaves out. A field the report printed and left
blank is still cleared — the report said something about it.
```

- [ ] **Step 3: Amend the import rule in `CLAUDE.md`**

In the `## Import rules` section, replace the first bullet:

```markdown
- An import owns **the fields its report prints** — ward, age, birthday, address, and
  the printed e-mail and phone. A report with no column for a field has said nothing
  about it and must not blank it; a report that prints the column and leaves it blank
  does clear it. Formats declare this as `ReportFields Carries`.
```

And in the `## Reading the report`-adjacent guidance at the top of the file, change:

```markdown
A cross-platform desktop app that imports the LCR Single Adults report into SQLite and
```

to:

```markdown
A cross-platform desktop app that imports an LCR directory report into SQLite and
```

- [ ] **Step 4: Check the docs against the code**

Run: `dotnet test`
Expected: all pass — the docs change no behaviour, but run it anyway before committing.

Read back both files and confirm every type name they mention exists: `IReportFormat`, `ReportFormats`, `ReportFields`, `ReportSource`, `ColumnLayout`, `PdfTableReader`, `ImportPlanner`, `ImportService`. A doc naming a type that was renamed mid-implementation is worse than no doc.

- [ ] **Step 5: Commit**

```bash
git add docs/architecture.md CLAUDE.md
git commit -F - <<'MSG'
docs: one reader, several reports

architecture.md described the geometry of a single report as though it
were the geometry of PDFs. Separate what is true of every LCR table --
vertically centred cells, rows further apart than the lines inside them
-- from what is true of one report, which is now a format.

Also narrows the import-ownership rule in CLAUDE.md to the fields a
report actually prints, and records why: the flat rule wipes every
address in the directory the first time someone imports the report that
has no address column.

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_0162Q5wUzGeN3fyawg57oWSC
MSG
```

---

## Final verification

- [ ] `dotnet test` — all green, warnings-as-errors clean.
- [ ] `git status --porcelain` — clean; no `.pdf`, no `.db`, no `settings.json`.
- [ ] `git log --oneline -9` — nine commits, one concern each.
- [ ] Open the app, import `print.pdf`, and confirm the Import screen reads *"Read as Organizations and Callings. This report does not list addresses and ages, so the ones already recorded are kept."* — then import the Single Adults export and confirm the note reads *"Read as Single Adults."* and that nobody's address was lost in between.
