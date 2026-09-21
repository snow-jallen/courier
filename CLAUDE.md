# Courier — working rules

A cross-platform desktop app that imports the LCR Single Adults report into SQLite and
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

- An import owns ward, age, birthday, address, and the printed e-mail and phone.
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

## Before committing

`dotnet test` — 50 tests, about a second. Warnings are errors.
Small commits, one concern each, imperative subject with a `feat:`/`fix:`/`docs:`/
`refactor:` prefix; the body explains why.
