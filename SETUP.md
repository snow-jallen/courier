# Setting up Courier

A step-by-step guide to getting the accounts and numbers Courier needs. No technical
knowledge assumed. Do the parts in order — later parts need values from earlier ones.

**Set aside about an hour**, plus a few days of waiting in the middle of Part 3 while a
registration is reviewed. You can use email straight away; texting has to wait for that
review.

## What it will cost

| What | When | Cost |
| --- | --- | --- |
| Email through your own Gmail | — | free |
| Twilio account credit | once, to start | $20 minimum |
| A phone number | every month | about $1.15 |
| Registering to text people | once | $4 + $15 |
| Keeping that registration | every month | $2 |
| Each text you send | per message | about $0.008 |
| Each call you make | per minute | about $0.014 |

So roughly **$40 to get started** and **about $3.20 a month**, plus about **$3.50 every
time you text everyone**. Calling everyone costs about $6. Emailing costs nothing.

## Keep a note as you go

You will collect seven values. Write them somewhere private — they are the keys to an
account that can spend money.

- [ ] Gmail address
- [ ] Gmail app password (16 letters)
- [ ] Twilio Account SID (starts `AC`)
- [ ] Twilio Auth Token
- [ ] Your Twilio phone number (starts `+1`)
- [ ] Your own mobile number (starts `+1`)
- [ ] Messaging Service SID (starts `MG`) — optional, Part 7

---

## Part 1 · Email, through your own Gmail

Courier sends email from your own Gmail account. Google will not accept your normal
password from another program, so you make a separate one just for Courier. It only
works for sending mail and you can revoke it at any time.

1. Go to **myaccount.google.com** and sign in.
2. Open **Security** in the left-hand menu.
3. Find **2-Step Verification**. If it is off, turn it on and follow the prompts — app
   passwords do not exist until it is on.
4. Back on the Security page, search the page for **App passwords**. (It is easiest to
   use the search box at the top of the Google Account page and type "app passwords".)
5. Where it asks for a name, type `Courier`, and press **Create**.
6. Google shows a **16-letter password in four blocks**. Copy it now — it is never shown
   again. The spaces do not matter; Courier ignores them.

> **Write down:** your Gmail address, and this 16-letter password.

Gmail will send to about **500 people a day**. With 427 people in the directory that is
one broadcast a day comfortably, but not two.

---

## Part 2 · A Twilio account and a phone number

Twilio is the service that actually sends texts and places calls. You pay only for what
you send.

1. Go to **twilio.com** and press **Sign up**. Use your real name and a real email.
2. Verify your email, then your mobile number, when asked.
3. Twilio asks some setup questions. Answer honestly; nothing here locks you in.
4. You land on the **Console**. On that first page you will see **Account SID** and
   **Auth Token**. The token is hidden behind a **Show** link.

> **Write down:** the Account SID (a long string starting `AC`) and the Auth Token.

### Add credit

A new account is in *trial mode* and can only message numbers you have personally
verified — no use for a directory of 427 people. To lift that:

5. Press **Upgrade** (top of the console) and add funds. **$20 is the minimum** and is
   plenty to start.

### Buy a number

6. In the console menu go to **Phone Numbers → Manage → Buy a number**.
7. Set **Country** to United States. Under **Number**, put `435` in the *Match to* box
   so you get a local Sanpete-area number — people are far likelier to open a text from
   a local number.
8. Make sure **SMS** and **Voice** are both ticked under Capabilities.
9. Press **Search**, pick one you like, and **Buy**. It costs about $1.15 a month.

> **Write down:** your new number, in the form `+14355550188` — a plus, then 1, then the
> ten digits, no spaces or brackets.

*If the console menu looks different from this, use the search box at the top of the
Twilio console and type what you are looking for. Twilio rearranges it from time to time.*

---

## Part 3 · Registering so your texts actually arrive

US phone carriers block bulk texting from unregistered numbers. This step is **not
optional** — skip it and your messages will silently vanish rather than fail loudly.

Because Courier serves your calling rather than a business, you register as a **Sole
Proprietor**, which needs no tax ID or company.

1. In the console go to **Messaging → Regulatory Compliance** (older consoles call it
   *Trust Hub* or *A2P 10DLC*).
2. Start a **Sole Proprietor** brand registration. You will be asked for:
   - your first and last name,
   - your email address,
   - **your own mobile number** — Twilio texts it a code you type back in.
3. Then create a **Campaign** and attach the number you bought in Part 2. You will be
   asked what your messages are about and for one or two examples. Be plain and
   truthful, for instance:

   > *Announcements to adult single members of the Manti Utah Stake who have asked to
   > receive them — activity times, locations and changes.*
   >
   > Sample: `Stake singles dinner this Friday, 6:30 PM at the stake center. — Jonathan
   > Allen, Stake Singles Representative`

4. Expect **$4** to register, **$15** to review the campaign, and **$2 a month** after.
5. **Wait.** Review usually takes a few days. You will be emailed.

Two things worth knowing: your mobile number can only be used on **three** such
registrations ever, and as a sole proprietor you may send about **one message a second**
— so texting all 427 people takes roughly seven minutes. That is normal, and Courier
shows the progress.

---

## Part 3b · Optional: texting from your own phone instead

Parts 2 and 3 set up Twilio, which reaches everybody but sends from a number nobody
recognises. Courier can also send texts through **your own phone**, the way Phone Link
does on Windows: the computer asks, your phone's line delivers. Messages then genuinely
come from your number, and replies arrive in your own Messages app.

Use it for a ward, a committee, or the handful who did not reply. **Not for all 427** —
a burst of hundreds from a personal line is what carriers throttle, and it sends at
about one message every two seconds.

On the Setup screen, under *Where texts go out from*, choose your phone.

### If you have an iPhone

**Courier must be running on a Mac.** The iPhone route works by asking the Messages app
to send, and Messages only exists on macOS — on Windows or Linux the option is greyed
out, and a setting carried over from a Mac is blocked with an explanation rather than
failing once for every person.

1. On the Mac, open **Messages** and make sure you are signed in.
2. On the iPhone: **Settings → Apps → Messages → Text Message Forwarding**, and switch
   on the Mac. Without this, green-bubble texts to people without iMessage will not go.
3. In Courier, choose *From my iPhone* and press **Send myself a test text**.
4. macOS will ask whether Courier may control Messages. Say yes. If you miss the prompt,
   it is **System Settings → Privacy & Security → Automation → Courier → Messages**.

### If you have an Android phone

Works from any computer — Courier just makes a web request to your phone.

1. Install **SMS Gateway for Android** from <https://sms-gate.app>.
2. Open it, switch **Local Server** on, and tap the **Offline** button to start it.
3. The app shows an **address** (like `http://192.168.1.44:8080`) and a **username and
   password**. Copy all three into Courier.
4. Press **Send myself a test text**.

The phone must be awake and on the same Wi-Fi as the computer. Its address changes when
it rejoins the network, so if sending stops working, check that first. In this mode
nothing goes through anybody else's server — the numbers stay on your own network.

## Part 4 · Making calls show *your* number

By default a call from Courier shows your Twilio number. You can make it show your real
mobile instead, so people recognise you and can simply call back.

1. Go to **Phone Numbers → Manage → Verified caller IDs**.
2. Press **Add a new caller ID**, enter your own mobile number.
3. Twilio rings you and reads out a code. Type it in.

That is all. Courier will use it for calls. **This does not work for texts** — see below.

---

## Part 5 · Getting replies on your phone

Texts cannot be sent from your personal mobile number. No service permits it: sending
from a number you have not proved you control is spoofing, and carriers block it. So
replies go to your Twilio number by default, where you would never see them.

This forwards them to your phone. It takes about five minutes and needs no programming.

1. In the console, find **TwiML Bins** (use the search box; it lives under *Developer
   tools* or *Runtime*).
2. Create a new bin, name it `Forward replies`, and paste this in, replacing the number
   with **your own mobile**:

   ```xml
   <?xml version="1.0" encoding="UTF-8"?>
   <Response>
     <Message to="+14355550164">{{From}}: {{Body}}</Message>
   </Response>
   ```

3. Save it.
4. Go to **Phone Numbers → Manage → Active numbers**, click your number.
5. Under **Messaging**, set *A message comes in* to **TwiML Bin**, and choose
   `Forward replies`. Save.

Now when somebody replies, it arrives as a text on your phone showing their number and
what they said. To answer, text them directly from your own phone — which is usually
what you want anyway.

You can do the same for calls to that number: make a second bin with
`<Response><Dial>+14355550164</Dial></Response>` and set *A call comes in* to use it.

---

## Part 6 · What the phone calls will say

Nothing to set up in advance. This is chosen on the **Send screen**, for each message,
because the message is the script.

When a message is going to anybody who prefers a call, the Send screen offers two
options:

- **Read the message out.** Twilio reads your words aloud. Nothing to record and ready
  immediately — right for a short, factual announcement.
- **In my own voice.** Courier rings you and asks you to record after the beep. Read
  your message off the screen, hang up, and Courier collects the recording.

Either way, **Call me and play it** rings you and plays exactly what everyone else will
hear. Worth doing once before a message goes to three hundred people.

Change the message after recording it and Courier throws the recording away and says so.
A recording of the old wording would go out sounding confident and be wrong.

Both need the Twilio account from Part 2 — calls always go through it, even when your
texts come from your own phone (Part 3b).

---

## Part 7 · Optional: a nicer-looking text

If you paste a **Messaging Service** into Courier, texts can arrive as RCS on phones
that support it — showing a proper sender name instead of an unknown number — while
every other phone gets exactly the same SMS as before.

Before spending time here, **ask Twilio support one question**: *can an individual,
registered as a sole proprietor, create an RCS sender using a person's name?* RCS sender
registration is built for businesses. If the answer is no, skip this part entirely — the
signature on every message already tells people who you are.

If yes: **Messaging → Services → Create Messaging Service**, add your number as a sender,
add the RCS sender once approved, and copy the service's SID (starts `MG`).

---

## Part 7b · Optional: keeping the directory somewhere else

Courier keeps everything in one file, `contacts.db`, in a Courier folder inside your
Documents. On the **Setup** screen, under *Where your directory lives*, you can:

- **Open a different file** — an existing Courier directory, perhaps one you moved to
  another drive.
- **Start a new one** — an empty directory somewhere of your choosing. The old file is
  left exactly where it was; nothing is moved or deleted.

A file that is not a Courier directory is refused before it is opened, so picking the
wrong thing changes nothing.

A synced folder such as Dropbox or iCloud works, with one rule: **only one computer may
have Courier open at a time.** Two at once can leave the file unreadable, and a sync
conflict on a database is not something you can merge by hand.

---

## Part 8 · Filling it into Courier

Open Courier and go to **Setup**.

1. **Who these messages are from** — your name and your calling. This is added to the
   end of every message, and it is what makes people trust a text from a number they do
   not know.
2. **Email** — your Gmail address and the 16-letter app password from Part 1. Press
   **Send myself a test email**. It should arrive within a few seconds.
3. **Text and voice** — the Account SID, Auth Token, your Twilio number, and your own
   mobile number from Part 2. Press **Send myself a test text**.
4. **Messaging Service** — only if you did Part 7. Otherwise leave it empty.
5. Press **Record my message** (Part 6), then **Call me and play the recording**.
6. Press **Save**.

### Trying it without messaging anyone

Twilio gives every account a second, *test* Account SID and Auth Token, in the console
under **Account → API keys & tokens**. Put those into Courier instead and every button
works, but nothing is delivered and nothing is charged. Combine them with these numbers
to see exactly what Courier says when things go wrong:

| Put this as your own number | What you should see |
| --- | --- |
| `+15005550006` | it works |
| `+15005550001` | "Twilio does not recognise … as a phone number" |
| `+15005550004` | "…has replied STOP…" |
| `+15005550009` | "…is a landline, so it cannot receive a text" |

Test credentials cannot place a real call, so the **voice recording still needs your
live credentials**.

---

## When something goes wrong

Courier never shows you an error code. It tells you what happened and what to do. The
ones you are most likely to meet:

| What Courier says | What to do |
| --- | --- |
| Google would not accept that password | You used your Gmail password. Go back to Part 1 and make an app password. |
| Twilio would not accept your account details | The Auth Token is usually copied short. Copy it again. |
| Your Twilio account is still a trial | Add funds — Part 2, "Add credit". |
| Gmail has stopped accepting messages for today | You have passed 500 emails. Everything up to that point went out; send the rest tomorrow. |
| Your Twilio account is out of credit | Add funds. Nothing was charged for the failed attempt. |
| …is a landline, so it cannot receive a text | Set that person to a phone call on the People screen. |
| …has replied STOP | They opted out. They must text START to your number before you can reach them again. |

Texts that simply never arrive, with no error at all, almost always mean **Part 3 is not
finished**. Check the registration status in the console.

---

## One last thing, which is not technical

These 427 people gave their details to the Church, not to you, and being in a directory
is not the same as agreeing to be texted or auto-called. Rules about consent apply to
you the same as anyone, and because Courier is your own tool rather than an official
system, that judgement is yours. Talk it over with your stake presidency **before** the
first broadcast, and give people an easy way to say "stop".
