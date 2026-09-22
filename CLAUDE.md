# Courier — working rules

A cross-platform desktop app that imports an LCR directory report into SQLite and
sends messages by each person's preferred channel. Read `docs/architecture.md` before
touching the PDF reader or the import rules; both encode measurements that are not
obvious from the code.

## Never in this repository

- **No real LCR export, and nothing derived from one.** Those files hold home
  addresses, phone numbers and birthdays for hundreds of people. Real exports go in
  `tests/Courier.Tests/Fixtures/private/`, which is gitignored, and the tests that use
  them skip when they are absent. Fixtures that get committed are synthetic, built in
  code by `SyntheticReport`.
- No `.db` files, no `settings.json`.

## The database is not encrypted, on purpose

Do not propose encrypting it. The export it comes from is unencrypted, so encrypting
the copy moves the weak point instead of removing it. The app's answer is to tell the
user plainly what the file holds — on the Import screen and as the first card in Setup.
Keep that copy blunt and specific; it is a feature, not boilerplate.

## Import rules

- An import owns **the fields its report prints** — ward, age, birthday, address, and
  the printed e-mail and phone. A report with no column for a field has said nothing
  about it and must not blank it; a report that prints the column and leaves it blank
  does clear it. Formats declare this as `ReportFields Carries`.
- Courier owns preferred channel, notes, and hand-added contact details. **An import
  must never write to these.** Tests enforce it; keep them.
- Nobody is deleted. Falling out of an export is a soft delete (`IsActive`,
  `DeactivatedOn`); reappearing restores the same row with its history intact.
- Plan first, apply second. `ImportPlanner` computes; `ImportService` writes, in one
  transaction. The user sees the plan before anything lands.

## Schema

- Enums stored by name, never by number.
- Every Guid key is set in the initializer and configured `ValueGeneratedNever()`.
- `dotnet dotnet-ef migrations add <Name> -p src/Courier.Data -s src/Courier.Data`
  after any entity change; the suite fails on drift.

## User interface

This is for people without a technical background. Name things the way they would:
"Send a message", not "dispatch batch". Errors say what went wrong and what to do about
it. Every credential has a Test button that sends a real message to the user's own
address, so "Working" means it actually worked.

Pure logic lives in `Core` with tests. Views do not compute.

## Sending

- **Courier augments one person's calling. It is not an official church system and must
  not look like one.** The rail names the person, not the stake. Every message goes to
  one recipient on its own and is signed with that person's name and calling — a text
  from an unrecognised number gets ignored or reported; a signed one gets answered.
  Signature composition lives in `Core/Domain/Signature.cs` so the preview, the length
  shown and what actually leaves are the same string.
- A provider's error code must never reach the screen. Translate it into what happened
  and what to do. Tests assert the codes are absent; keep them.
- Reachability carries a reason. Nobody is dropped from a send silently.
- Each delivery is written down before the next is attempted, so an interrupted send
  leaves an honest record.

## Logging

- `Log.Record` / `Log.Failure` from anywhere; the sink is set once at start-up.
- **Nothing identifying goes in the log.** It exists to be sent to whoever is helping,
  which makes it a copy of the directory unless it is deliberately not one. Names never;
  addresses only through `Redact.Address`; provider messages only through
  `Redact.Failure`, since they quote the address back; message bodies only as a length.
  There is a test that a failed send leaks none of it — keep it.
- Log what was done and what failed, not what was typed.

## Before committing

`dotnet test` — about 130 tests, a second or so. Warnings are errors.
Small commits, one concern each, imperative subject with a `feat:`/`fix:`/`docs:`/
`refactor:` prefix; the body explains why.
