# How Courier is put together

    src/Courier.Core       pure logic: reading the PDF, normalising it, diffing an
                           import, and choosing who a message goes to
    src/Courier.Messaging  sending over each channel, and the settings file
    src/Courier.Data       EF Core entities, migrations, applying an import, sending
                           a batch and writing down what happened
    src/Courier.App        Avalonia user interface
    tests/Courier.Tests    all of the tests

`Core` depends on nothing but PdfPig. `Messaging` depends on `Core`. `Data` depends on
both. Nothing depends on the UI, so every rule below is testable without opening a
window — and the UI itself is tested headless, which is the only way a mistyped binding
gets caught before a user finds it.

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

## What an import may and may not touch

`ImportPlanner` produces an `ImportPlan` — added, updated, deactivated, reactivated,
unchanged — and changes nothing. `ImportService.ApplyAsync` writes it in one
transaction, or not at all.

LCR issues no stable identifier, so people are matched on **name plus birthday**, then
on name alone for anyone left over. Birthday is printed for nearly everyone, never
changes, and is what tells two people of the same name apart.

The split that matters:

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
blank is still cleared — the report said something about it. A field printed but empty
on every row is a third case, and a judgment the format's author makes rather than a
rule the reader can apply on its own: Organizations and Callings maps an Age column but
leaves it out of `Carries`, because a column that is never once filled has never
actually said anything, unlike one that's blank for some people and filled for others.

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

## Sending

`Audience` turns the directory into a chosen set: filter, sort, and — the part that
matters — **reachability**, which carries a reason rather than a bool. Someone who
prefers e-mail and has no e-mail address is named on screen before the send, not
dropped silently during it. The count on the Send button and the addresses the send
uses come from that one calculation, so the screen cannot promise something the send
does not keep.

`BroadcastService` writes each delivery before attempting the next. A send that is
interrupted half way therefore leaves an honest record: the messages already sent
cannot be unsent, and the user has to be able to see which those were.

Every sender turns its provider's failures into a sentence the user can act on. "535
5.7.8" and "21610" tell the people this app is for exactly nothing; "Gmail needs an app
password, here is where to make one" and "they replied STOP and must text START first"
do. The tests assert the codes themselves never reach the screen.

The voice channel plays a recording of the user, never text-to-speech. Courier rings
them with inline TwiML that records them, Twilio keeps the recording, and the broadcast
plays it back by its Twilio address. Passing TwiML inline when a call is created is
what lets a desktop app place calls at all — the usual arrangement needs a public web
server for Twilio to fetch instructions from, which this app has no business running.

Credentials live in a JSON file beside the database, never inside it: they are not
directory data and should not ride along in its backups. The file is owner-readable
only, because unlike the directory a Twilio token can be used to spend money.

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
