# Courier

A small desktop app for keeping a stake directory current and getting a message to
everyone in it — by e-mail, by text, or by a recorded phone call, whichever each
person prefers.

Built for one person on one computer, with no server and no account to sign into.

## What it does

1. **Import.** Drop in the Single Adults report exported from LCR as a PDF. Courier
   reads it, compares it against what it already holds, and shows exactly what would
   change before anything is written. People who drop out of an export are marked
   inactive, never deleted.
2. **Keep.** Add contact details LCR doesn't have. They survive every future import,
   and they show up on a **To enter in LCR** list so someone can put them back into
   the official record — a list that clears itself once an export shows the detail
   has been entered.
3. **Send.** Write a message once. It goes out by e-mail, text, or a recording played
   over a phone call, split by what each person chose.

## Running it

    dotnet run --project src/Courier.App

## Building a release

One self-contained executable per platform, with no runtime to install:

    dotnet publish src/Courier.App -c Release -r osx-arm64
    dotnet publish src/Courier.App -c Release -r win-x64
    dotnet publish src/Courier.App -c Release -r linux-x64

Each produces a single compressed executable of about 55 MB in
`src/Courier.App/bin/Release/net10.0/<platform>/publish/`. Double-click it; there is
nothing to install first.

## Tests

    dotnet test

About 130 tests, a second or so. The suite never needs a real directory: the PDF reader
is tested against a synthetic report built in code with the same geometry as the real
one. The window itself is tested headless, so a mistyped binding fails the build rather
than the user. If you drop a genuine export at
`tests/Courier.Tests/Fixtures/private/manti-singles.pdf` (gitignored), four extra
tests run against it and check that every one of the 427 people — the number the report prints in its own
footer — comes back with a readable phone, e-mail, age, birthday and ward.

## Privacy

The database is a single unencrypted SQLite file in `~/Documents/Courier/`. That is a
deliberate choice — the LCR export it comes from is unencrypted too, so encrypting the
copy would move the weak point rather than remove it. What matters instead is that
whoever uses Courier understands what they are holding. The app says so plainly on the
Import screen and in Setup, and this repository will not accept a real export:
`tests/Courier.Tests/Fixtures/private/` is gitignored, and so is `*.db`.

Treat any file that came out of LCR the way you would treat the directory itself.
