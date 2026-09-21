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
2. **Keep.** Add people the export does not carry, and correct anyone's email, phone
   or address on the People screen. Somebody added by hand is never removed by an
   import, because their absence from an export says nothing about them. Courier
   never writes over what the export said — it records the correction, which is what
   makes it survive every future import and what puts it on the **To enter in LCR**
   list, so the official record can catch up. That list clears itself once an export
   comes back carrying the detail.
3. **Send.** Choose who by ward, channel, age, birthday month, or a search that reads
   notes as well as names — so writing "choir" or "#ride-needed" in somebody's note is
   all the tagging system there is, and all there needs to be. Write a message once. It goes out by e-mail, text, or a phone call,
   split by what each person chose — or all one way, when something is urgent enough to
   text everybody. Calls read the message out, or play a recording of you reading it
   off the screen, chosen per message. Either way you can have Courier ring you first and play back
   exactly what everyone else will hear.

4. **Look back.** Every send is kept: what it said, when it went, who it went to, on
   which channel, at which address, and what came back. Searchable per person and
   copyable as text.

## Setting it up

**[SETUP.md](SETUP.md) is the step-by-step guide** — creating the accounts, getting each
value Courier asks for, and registering so the texts actually arrive. Written for
somebody who does not work with this sort of thing. About an hour, plus a few days of
waiting for one registration.

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

## Sending as yourself

Courier is one person's tool. It is not an official church system and should not look
like one: every message is sent to **one person at a time**, addressed only to them, and
signed with your name and your calling, which you set on the Setup screen.

    Dinner is Friday at 6:30 at the stake center.
    — Jonathan Allen, Stake Singles Representative

Nobody ever sees who else a message went to — not because recipients are hidden, but
because each message really is its own message.

### Can the texts come from your own number?

**Yes, through your own phone — and which phone decides how.** Setup has a toggle:

- **iPhone** — Courier asks the Messages app on your Mac to send. **This only works with
  Courier running on a Mac**; Messages exists nowhere else, so on Windows or Linux the
  option is refused rather than accepted and then failing at the moment you press Send.
- **Android** — install [SMS Gateway for Android](https://sms-gate.app/), turn on Local
  Server, and paste the address and sign-in it shows into Setup. Courier then asks your
  phone over your own Wi-Fi. This works from any computer, and in local-server mode the
  numbers never leave your network.

Either way it is the same arrangement as Phone Link: the computer asks, the phone's own
line delivers.

**On a Mac with an iPhone, in detail:** Setup can send texts by asking Messages to
send them, which is how Phone Link works on Windows: the computer asks, the phone's own
line delivers. Messages then genuinely come from your number and replies arrive in your
own Messages app. Messages must be open and signed in, your iPhone needs Text Message
Forwarding switched on for that Mac, and macOS will ask once for permission to control
Messages.

The catch, for both phones, is that a personal line is meant for person-to-person
texting. A burst of hundreds is exactly what carrier spam systems look for, and it goes
out at roughly one message every two seconds. **Keep it to a few dozen** — a ward, a
committee, the people who did not reply — and use Twilio for the whole directory.

**Through a service, no.** Verify your mobile in the Twilio console as a caller ID and outgoing
calls show *your* number. People see you ringing, and returning the call reaches you
directly.

**Texts: no, and no service will let you.** Sending a text that appears to come from a
number you have not proven you control is spoofing; carriers block it and Twilio
forbids it. Hosting your own mobile number on Twilio is the supported way to send from
it, but it does not apply here twice over: ordinary mobile numbers from the big US
carriers generally cannot be hosted, and if yours could, Twilio would then receive your
personal texts instead of your phone.

What to do instead, which gets most of the way there:

1. **Buy a Twilio number in your own area code** so it reads as local rather than
   out-of-state.
2. **Forward its replies to your phone.** In the Twilio console, point the number's
   incoming-message webhook at a TwiML Bin containing
   `<Response><Message to="+1435...">{{From}}: {{Body}}</Message></Response>`. Replies
   then arrive in your normal Messages app with the sender's number in front of them. No
   server, no code.
3. **Let the signature do the recognising.** People do not recognise numbers; they
   recognise names. That is what the signature is for.

### RCS: making the sender recognisable

Paste a Twilio **Messaging Service** with an RCS sender attached into Setup, and texts
go out through it: phones that support RCS show a named, verified sender with proper
formatting, and every other phone gets exactly the SMS it would have got anyway, from
the same request. Leave it empty and nothing changes. There is no new channel and no new
preference — the people who chose "Text" simply get a better text where their phone
allows it.

One thing to check before spending time on it: **RCS sender registration is built for
businesses**, and Courier exists to serve one person's calling rather than an
organisation. Ask Twilio whether you can register an agent as an individual, and say
plainly that the sender name would be a person's. If the answer is no, leave the field
empty — the signature is doing that job already.

### Before the first real broadcast

Texting a list from a US long code requires **A2P 10DLC registration** — a one-off form
in the Twilio console and about $2 a month. Registered as a sole proprietor you get one
number and one campaign, roughly one message per second and a few thousand segments a
day. Sending to 427 people is comfortably inside that, and takes about seven minutes.

Unregistered traffic is filtered by the carriers, so this is not optional.

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

## When somebody needs help

Courier keeps a log beside its settings, one JSON object per line, thirty days of them.
It records what the app was asked to do and what failed: which screens were opened, how
big an import was, how many people a send reached, which credential test passed, the
full exception chain when something broke.

**It is safe to send to whoever is helping.** Names never go in it at all. An address is
masked to `a…e@e…m` or `…42 (11 digits)` — enough to match against a person on screen,
not enough to write to them. A provider's complaint has any address it quoted back taken
out, because those messages routinely repeat the number that failed. Messages are
recorded as a length and nothing else. A test asserts that a failed send leaves no name,
address or message text anywhere in the file.

Setup has a **Copy the recent log** button for pasting into an email.

## Privacy

The database is a single unencrypted SQLite file, in `~/Documents/Courier/` unless you
point Courier somewhere else — Setup can open a different one or start a fresh one
anywhere you like, and remembers which. Settings stay in the usual folder whatever the
database does, since they are what records where it went. That is a
deliberate choice — the LCR export it comes from is unencrypted too, so encrypting the
copy would move the weak point rather than remove it. What matters instead is that
whoever uses Courier understands what they are holding. The app says so plainly on the
Import screen and in Setup, and this repository will not accept a real export:
`tests/Courier.Tests/Fixtures/private/` is gitignored, and so is `*.db`.

Treat any file that came out of LCR the way you would treat the directory itself.
