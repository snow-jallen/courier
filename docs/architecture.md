# How Courier is put together

    src/Courier.Core       pure logic: reading the PDF, normalising it, working out a diff
    src/Courier.Data       EF Core entities, migrations, applying an import
    src/Courier.Messaging  sending over each channel
    src/Courier.App        Avalonia user interface
    tests/Courier.Tests    all of the tests

`Core` depends on nothing but PdfPig. `Data` depends on `Core`. The UI depends on all
three and nothing depends on the UI, so every rule below is testable without opening a
window.

## Reading the report

This is the hard part, and it is worth knowing why before changing any of it.

The LCR report is a rendered HTML table. Its PDF contains no rules, no cell
boundaries, and no reading order that matches the table — pulling the text out
linearly interleaves the columns into nonsense. The only structure available is where
each glyph sits on the page.

Two measured facts drive `PdfTableReader`:

* **Cells are centred vertically within their row, not aligned to its top.** A cell
  wrapping to three lines sits at the row's centre plus and minus one line height; a
  two-line cell sits at plus and minus half of one. So a row's baselines land on a grid
  of *half* the line height, and no cell's first line lines up with any other cell's.
  This is why a row cannot be found by looking for where its name begins.
* **Rows are separated by more vertical space than the lines inside them.** On a real
  export whose text is 8.9pt: lines wrapped inside a row are 12.8pt apart (1.44x the
  text size), consecutive rows are 23.2pt apart (2.6x). `RowBreakFactor` sits at 2.0,
  between the two, and is scaled by the text size rather than by the other gaps on the
  page — a relative rule cannot tell a page of single-line rows apart from one tall row.

Three more details that each cost a debugging session:

* Glyph positions come from the **advance box** (`StartBaseLine`/`EndBaseLine`), not the
  ink bounding box. Ink bounds leave gaps inside a word wide enough to look like spaces,
  which turns `17 Jan` into `1 7 Jan` and breaks every birthday.
* Space glyphs are **kept**, because the report writes real spaces and honouring them
  beats inferring every space from a gap.
* Column positions are **read from the report's own headings**, never hard-coded, and
  only from lines carrying two or more headings. The report's toolbar line reads
  "Group by Unit Edit Report", and matching its "Unit" puts the columns out of order and
  folds four fields into one. The detected positions must increase left to right or the
  layout is rejected.

A PDF that is not this report fails loudly with `LcrReportException` rather than
importing nonsense.

## What an import may and may not touch

`ImportPlanner` produces an `ImportPlan` — added, updated, deactivated, reactivated,
unchanged — and changes nothing. `ImportService.ApplyAsync` writes it in one
transaction, or not at all.

LCR issues no stable identifier, so people are matched on **name plus birthday**, then
on name alone for anyone left over. Birthday is printed for nearly everyone, never
changes, and is what tells two people of the same name apart.

The split that matters:

* **An import owns** ward, age, birthday, address, and the e-mail and phone as printed.
  These are overwritten every time.
* **Courier owns** the preferred channel, notes, and any contact details added by hand.
  No import may touch them. There are tests for this.

Nobody is ever deleted. Falling out of an export sets `IsActive = false` and
`DeactivatedOn`; reappearing clears both and keeps the original `FirstSeenOn`, the
preferred channel, and every past delivery. An import that would deactivate more than
10% of the directory returns a warning for the user to confirm, because that pattern
means a partial export far more often than it means an emptying stake.

## Contact details and the LCR backlog

A person has many `ContactPoint`s, each marked `Lcr` or `Local`.

The **To enter in LCR** report is every contact point that is `Local`, has never been
seen in an export, and has not been ticked off by hand. When a later export does carry
that same value, the existing row is marked as seen rather than duplicated — so the
backlog clears itself the moment the entry has actually been made.

## Conventions

* Enums are stored **by name**, never by number, so adding a channel cannot renumber
  existing rows.
* Every key is a `Guid` set in the entity's initializer, and every key is configured
  `ValueGeneratedNever()`. Without that, EF reads an already-set key as proof the row
  exists and issues an UPDATE where an INSERT was meant — anything attached through a
  navigation property fails to save, silently at the model level and loudly at runtime.
* Instants are `DateTimeOffset` in the entities, converted to UTC text in SQLite so
  `ORDER BY` and `WHERE` work in SQL instead of in memory.
* A day that a person or a record belongs to is a `DateOnly`, in the user's local
  calendar. Never derive one from an instant.
* After changing an entity: `dotnet dotnet-ef migrations add <Name> -p src/Courier.Data
  -s src/Courier.Data`. The suite fails if the migrations and the model disagree.
* Warnings are errors. Run `dotnet test` before committing.
