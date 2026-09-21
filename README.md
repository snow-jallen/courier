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

## Checking it is wired up without messaging anyone

**Twilio publishes test credentials.** In the Twilio console, under Account → API keys
& tokens, there is a second *Test* Account SID and Auth Token alongside the live pair.
Put those into Setup and Courier's calls are fully validated but nothing is sent, nothing
is delivered, and nothing is charged. Twilio also reserves "magic" numbers that force a
particular outcome, which is how each failure message can be seen without waiting for a
real one:

| Number          | What Twilio does                    | What Courier says |
| --------------- | ----------------------------------- | ----------------- |
| +15005550006    | succeeds                            | Sent              |
| +15005550001    | rejects it as an invalid number     | "Twilio does not recognise … as a phone number" |
| +15005550004    | reports the number as unsubscribed  | "…has replied STOP…text START…" |
| +15005550009    | reports it cannot receive texts     | "…is a landline, so it cannot receive a text" |
| +15005550002    | reports it as unroutable            | "…may be switched off, disconnected…" |

Put one of those in "Your own mobile (for tests)" and press the Test button.

Two caveats. The test credentials only validate requests — they will not place a real
call, so the **voice recording flow cannot be exercised with them**; that one needs the
live credentials and a real call to your own phone. And an invalid *From* number is
+15005550001, while +15005550007 is a number the account does not own.

**Email has no equivalent.** Google publishes no sandbox: an app password is against a
real mailbox. To try it without involving anyone, either send to yourself — which is all
the Test button does — or point Host and Port at a local mail catcher such as smtp4dev
or MailHog and watch the message arrive there.

## Privacy

The database is a single unencrypted SQLite file in `~/Documents/Courier/`. That is a
deliberate choice — the LCR export it comes from is unencrypted too, so encrypting the
copy would move the weak point rather than remove it. What matters instead is that
whoever uses Courier understands what they are holding. The app says so plainly on the
Import screen and in Setup, and this repository will not accept a real export:
`tests/Courier.Tests/Fixtures/private/` is gitignored, and so is `*.db`.

Treat any file that came out of LCR the way you would treat the directory itself.
