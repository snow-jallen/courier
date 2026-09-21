# Reading more than one LCR report

## Why

Courier reads one report: **Single Adults**, exported from LCR as a rendered HTML
table. A second report turns out to carry the same people — **Organizations and
Callings**, whose "Single Adult Members" section lists 428 of them — and a stake
clerk reaches for whichever one LCR puts in front of them. Today the second file is
rejected outright.

The two reports differ in more than their headings, so "add a few more heading
synonyms" does not work. They differ in the vertical rhythm that separates one row
from the next, in the order of their columns, in which columns exist at all, and in
what else is printed on the page. Each of those is a fact about one report, and none
of them belongs hard-coded in a parser that is supposed to read both.

This design puts the per-report facts in one place per report and leaves the geometry
— which is genuinely shared — where it is.

## What the two reports are

Measured from a real export of each. Sizes are PDF points.

|                        | Single Adults                | Organizations and Callings        |
|------------------------|------------------------------|-----------------------------------|
| Headings               | Preferred Name, Individual E-mail, Phone, Unit, Age, Birthday, Address - Street 1 | Name, Gender, Age, Birth Date, Phone Number, Email, Current Unit |
| Column order           | name, email, phone, unit, age, birthday, address | name, gender, age, birthday, phone, email, unit |
| Heading repeats        | page 1 only                  | every page                        |
| Text size              | 8.9pt                        | 8.0pt                             |
| Row pitch              | 23.2pt (2.6x)                | 13.4pt (1.68x)                    |
| Wrapped-line pitch     | 12.8pt (1.44x)               | 4.5pt (0.56x)                     |
| Cells vertically centred | yes                        | yes                               |
| Address                | yes                          | absent                            |
| Age                    | printed                      | column printed, value never filled (0 of 428) |
| Gender                 | absent                       | M / F                             |
| Second table on page 1 | no                           | yes — the stake's Single Adult callings |
| Footer                 | report URL, `Page n of m`    | `For Church Use Only ... Intellectual Reserve`, page number |
| Row count check        | `Count: 427`                 | `Count: 428`                      |

Three of those rows are the ones that break the current code:

* **Row pitch 1.68x.** `PdfTableReader.RowBreakFactor` is 2.0, a constant. At 8.0pt
  text the threshold is 16.0pt and every 13.4pt row boundary falls below it, so the
  whole page reads as one row.
* **Column order.** `LcrColumns.Edges` builds its boundaries from a fixed field order.
  The new report swaps phone and email, and edges built in the wrong order put every
  field in the wrong cell.
* **Gender.** Courier has no use for it, but the column has to be *found* regardless:
  without an edge at x=153.9 the `F` glyph falls inside the Name column and every
  name reads `Ashdown, Marigold F`.

## Approach

A format is a strategy. `LcrReportParser` keeps a registry of them, asks each in turn
to recognise the document, and lets the first that succeeds drive every page.

Both of today's formats are the same implementation — `ColumnLayoutFormat`, built from
a declarative record — because both are "a table whose own headings give the column
positions". A third report of that shape is a short record. A report that is not that
shape implements `IReportFormat` and shares nothing but `PdfTableReader`.

Two alternatives were considered and rejected. Pure declarative descriptors with no
interface read well while every report is a headings-and-columns table, but the first
one that is not — a grouped-by-ward report with subheadings, say — forces the
abstraction open with no escape hatch. A whole parser per format gives maximum freedom
but duplicates row slicing, stitching and furniture filtering, which is exactly where
the bugs have been.

## The seam

```csharp
public enum LcrField { Name, Email, Phone, Unit, Age, Birthday, Address }

/// <summary>The fields a report prints, and therefore the only fields an import
/// from it is allowed to overwrite.</summary>
[Flags]
public enum ReportFields
{
    None = 0, Unit = 1, Age = 2, Birthday = 4, Address = 8, Email = 16, Phone = 32,
    All = Unit | Age | Birthday | Address | Email | Phone,
}

public interface IReportFormat
{
    /// <summary>What to call this report on screen, in LCR's own words.</summary>
    string Name { get; }

    /// <summary>A vertical gap wider than this many times the text size starts a new
    /// row. A property of the report's layout, not of the page.</summary>
    double RowBreakFactor { get; }

    ReportFields Carries { get; }

    /// <summary>The column positions, if this group of lines is this report's heading.
    /// Null for any other group, which is every group on a body page. The caller does
    /// the grouping, at this format's own RowBreakFactor.</summary>
    ColumnLayout? Detect(IReadOnlyList<TextLine> group);

    /// <summary>True for a line belonging to the page rather than to a person.</summary>
    bool IsFurniture(string lineText);
}
```

`ColumnLayout` replaces `LcrColumns`:

```csharp
public sealed record ColumnLayout(IReadOnlyList<(double X, LcrField? Field)> Columns)
{
    private const double Slack = 1.5;   // unchanged, and for the same reason
    public string Cell(IEnumerable<TextLine> group, LcrField field);
}
```

Two changes from `LcrColumns`, each forced by the new report:

* **Edges are derived by sorting the detected positions**, not by walking a fixed field
  order. A format states which headings exist; the page states where they are.
* **A column may carry a null field.** Gender is detected so that its edge exists and
  discarded so that nothing stores it. Adding Gender to the schema is out of scope:
  Courier has no use for it today, and it would cost a migration.

`ColumnLayoutFormat` is the descriptor-driven implementation. A column is declared by
the heading phrases that name it and the field, if any, behind it:

```csharp
/// <summary>One column of a report. Headings are the phrases that name it, tried
/// longest first. A null field means the column is found only so that its edge
/// exists — whatever is printed in it is discarded.</summary>
public sealed record ColumnSpec(IReadOnlyList<string> Headings, LcrField? Field)
{
    public ColumnSpec(string heading, LcrField? field) : this([heading], field) { }
}

new ColumnLayoutFormat(
    Name: "Organizations and Callings",
    Columns: [
        new("Name",          LcrField.Name),
        new("Gender",        null),
        new("Age",           LcrField.Age),
        new("Birth Date",    LcrField.Birthday),
        new("Phone Number",  LcrField.Phone),
        new("Email",         LcrField.Email),
        new("Current Unit",  LcrField.Unit),
    ],
    RowBreakFactor: 1.0,
    Carries: ReportFields.Unit | ReportFields.Birthday
           | ReportFields.Email | ReportFields.Phone,
    Furniture: ["For Church Use Only", "Count:"]);
```

**Every column listed is required.** A group that yields six of these seven is not
this report's heading, and neither format has an optional column today. If one ever
does, the descriptor grows a flag rather than the rule growing an exception.

A column's headings are a list of **phrases**, matched against consecutive words on a
line, longest first. That is what lets `Individual E-mail`, `Birth Date`,
`Phone Number` and `Current Unit` be single headings, and it is what keeps the old
report's bare `Individual` at the phone column from being read as its e-mail heading.
`Preferred Name` wraps across two lines in the old report, so its Name column lists
`["Preferred Name", "Preferred", "Name"]`.

The two descriptors cannot claim each other's file. The old one requires `Address`,
which the new report has none of. The new one requires `Gender` and `Email`, and the
old report writes `E-mail`. Registration order therefore does not matter today; the
rule is nevertheless first match wins, and it is recorded here so that a future
overlapping descriptor is a deliberate decision rather than a surprise.

## Choosing the format

Selection runs once per document, not once per page.

1. Read page 1's lines with `PdfTableReader.ReadLines`, which is format-independent.
2. Offer them to each registered format in turn. The format groups the lines using
   **its own** row-break factor, then calls `Detect` on **each group separately**,
   accepting the first group that yields every required heading in increasing x order.
3. If no format claims page 1, read page 2 and offer it to all formats, and so on.
   Each page is read once and shown to every format, so a cover page costs one extra
   read rather than one per format.
4. If no format claims any page, throw `LcrReportException` naming the formats tried.

Detecting per group, rather than merging every heading found anywhere on the page into
one dictionary as `LcrColumns.Detect` does today, is not a refinement — the new report
breaks the merged version outright. Page 1 carries a `Name` heading at x=225.6 in the
callings table and another at x=54.1 in the members table; merged, the columns run
right to left and the layout is rejected.

Within a group, the existing rule stands: **a line must carry two or more recognised
headings to contribute any**. That rule is what keeps the old report's toolbar line,
`Group by Unit Edit Report`, from donating a `Unit` position that folds four fields
into one.

## Reading the rows

Per page, with the chosen format:

1. Read the lines. Find the heading group, if this page has one.
2. Keep only lines **below** the heading. The new report prints the stake's Single
   Adult callings table above the members heading on page 1 — names at x=227,
   rows reading `Calling Vacant`, no contact details at all. Parsed as people they
   would be stitched into the row above and corrupt it. Pages with no heading keep
   every line, so the old report's continuation pages are unaffected.
3. Drop furniture. This has to happen **after** heading detection and **before**
   grouping: after, because the old format's furniture list contains `Preferred`,
   `Street 1` and `(1 Jan)`, which are its own headings; before, because a footer line
   left in place joins the last row of the page.
4. Group into rows at `median point size * format.RowBreakFactor`.
5. Build an `LcrRow` per group from the layout's edges, then `Stitch` as today.

`PdfTableReader` itself does not change. Both reports centre their cells vertically on
the row, which is the fact that makes the reader hard and the fact that makes it
reusable. Only the threshold differs, and it becomes a parameter rather than a
constant.

`1.0` for the new report is measured, not guessed: text is 8.0pt, consecutive rows sit
13.4pt apart (1.68x) and the lines of a wrapped name sit 4.5pt apart (0.56x). Any
factor in (0.56, 1.68) separates them. A factor of 1.0 puts the threshold at 8.0pt:
1.8x the widest gap inside a row, and 0.6x the narrowest gap between two rows.

The callings table is ignored rather than imported. It prints a calling, a name, two
dates and a unit — no phone, no e-mail, no address — so there is nothing in it a
message could be sent to.

## What an import may overwrite

`LcrReport` gains a `Source`, which is the pair of facts every consumer downstream
needs about where the rows came from:

```csharp
public sealed record ReportSource(string FormatName, ReportFields Carries);
```

The rule in CLAUDE.md is that an import owns ward, age, birthday, address and the
printed e-mail and phone, and overwrites them every time. That rule was written when
there was one report and it printed all six. Applied unchanged to a report that prints
four, it wipes every address in the directory the first time someone imports the wrong
file — and the addresses cannot be recovered without going back to the other export.

So the rule narrows by one clause: **an import owns the fields its report prints.**

* `ImportPlanner.Plan` takes a `ReportSource` and skips uncarried fields in `Diff`. An
  Organizations-and-Callings import produces no `Address` and no `Age` change, so none
  is shown and none is recorded. The parameter is **required**, not defaulted to
  `ReportFields.All`: a silent default is how the rule gets forgotten at a new call
  site, and the existing tests are short enough to update.
* `ImportService.ApplyFields` and `SyncLcrContactAsync` read `report.Source.Carries`
  and leave the corresponding properties alone. The plan and the write must agree; if
  only the planner learns the rule, the screen promises something the write does not
  keep. There is a test for exactly that disagreement.

`Carries` is a fact about the report, not about the person. A format that carries Email
and prints none for someone still clears that person's LCR e-mail — the field was
reported, and reported as empty. That is today's behaviour and it stays.

**Age in the new report is mapped as a real column but left out of `Carries`.** The
heading is printed and the value is empty on all 428 rows of the only export available.
Reading it and ignoring it would be dishonest; not reading it at all would silently
discard a value if some stake's export does fill one. Mapping it and declining to let
it overwrite says what is actually true, and turning it on later is one flag.

`ImportPlan` gains `Notes`, composed by `ImportPlanner` from the `ReportSource` —
in `Core`, because views do not compute. The Import screen shows, above the plan:

> Read as Organizations and Callings. This report does not list addresses or ages, so
> the ones already recorded are kept.

Notes are not warnings. Nothing is wrong, and the line must not be styled as though
something is.

## Files

New, in `src/Courier.Core/Import/`:

    LcrField.cs            LcrField, ReportFields, ReportSource
    IReportFormat.cs       the strategy
    ColumnLayout.cs        replaces LcrColumns.cs
    ColumnLayoutFormat.cs  the descriptor-driven implementation
    ReportFormats.cs       the registry and the two descriptors

Changed:

    Core/Import/LcrReportParser.cs   format selection, then per-page drive
    Core/Import/ImportPlan.cs        Notes
    Core/Import/ImportPlanner.cs     ReportSource parameter; Diff skips uncarried fields; Notes
    Data/ImportService.cs            ApplyFields and SyncLcrContactAsync respect Carries
    App/ViewModels/ImportViewModel.cs, App/Views/ImportView.axaml   format name and notes
    docs/architecture.md             "Reading the report" rewritten

Deleted: `Core/Import/LcrColumns.cs`.

`LcrRow` keeps its seven fields. A report that does not print one leaves it empty,
which is what it already does for a person with no e-mail.

## Testing

`SyntheticCallingsReport.Build()`, a second in-code fixture beside `SyntheticReport`,
laid out the way the new report lays out — no real data, same as today. It must
reproduce every feature the parser depends on:

* the heading repeated on both of its pages
* the callings table above the members table on page 1
* a name wrapped to two lines, centred 4.5pt either side of the row
* an Age column with a heading and no values
* a Gender column
* a person with no e-mail, and a person with no phone
* the `For Church Use Only` footer and a `Count:` line

Tests:

1. Each synthetic is recognised as exactly its own format, and neither format claims
   the other's file.
2. A PDF that is neither throws `LcrReportException` and names both formats tried.
3. The new format reads one row per person, with Gender discarded and Age empty.
4. The callings table above the members heading yields no people.
5. The wrapped name is rejoined.
6. Planner: a format that does not carry Address leaves an existing address untouched
   and reports no `Address` change. A format that does carry Address and prints it
   empty still clears it.
7. `ImportService`: applying a plan from an uncarried-field report does not null the
   column, and does not touch the contact points for a kind the report omits.
8. Every existing test in `LcrReportParserTests`, `ImportPlannerTests` and
   `ImportServiceTests` still passes. The rules CLAUDE.md says the tests enforce —
   that an import never writes preferred channel, notes or hand-added details — are
   unchanged.

Against real exports, skipped when absent, in the existing `[RequiresRealReport]`
pattern: a `[RequiresRealCallingsReport]` set over
`tests/Courier.Tests/Fixtures/private/manti-callings.pdf` asserting 9 pages, 428 rows
to match the report's own `Count: 428`, every unit snapping onto a known ward, clean
phone and birthday shapes, and a re-import that changes nothing.

The real export is copied into the gitignored private fixtures folder and its README
gains a line. Nothing derived from it is committed.

## Out of scope

* Storing Gender. No use for it today, and it costs a migration.
* Importing the callings table. No contact details, so nothing to send to.
* Asking the user, per import, whether to clear the fields a report omits. The format
  declares what it prints; that is not a decision to hand to someone who did not
  choose which report LCR gave them.
