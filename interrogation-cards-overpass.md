# Interrogation Statement Cards — "The Overpass"
*Draft v0.1 — 2026-08-17. Facilitator card decks for the interrogation paper prototype (interrogation-design.md). One deck per suspect; evidence ids reference `cases/overpass.yaml`. These cards are the prototype input — the interrogation data schema gets drafted from whatever survives the table.*

## How to Read a Card

> **[V2]** *"He never raised any concerns with me."*
> **Truth:** LIE · **Contradict:** reyes-cloud-notes, safe-deposit-envelope · **Tell:** pauses before "never"
> **On collapse:** revises to fallback line; −10 composure

- **Truth** is facilitator ground truth: TRUE, LIE, or EVASION (technically-true dodge).
- **Contradict** lists evidence that *genuinely* contradicts the statement. Presenting any listed item always lands (Present-evidence never rolls for truth): the statement collapses, composure damage applies, and the suspect must revise. Presenting unlisted evidence backfires: suspect +5 composure, and they learn what you're holding.
- **Tell** is what an Insight special reveals — mark the card as *shaky* on the transcript.
- Every statement heard goes **on the record** and becomes pinnable, whatever else happens.
- Cards are read roughly in order; branch notes override. Revised statements are quoted inline on the parent card.

Base damage (tune at the table): Press success −5 composure, special −10 and reveals a tell; landed contradiction −10; contradiction that collapses an alibi −15. Failed Press: suspect +5, and the *next* failed Press on the same thread costs 1 extra Patience.

---

## Deck P — Petra Okafor
*Scared witness. Composure 40 · Stonewall 35 · Patience 12.*
**Approach modifiers:** Intimidate is poison — any Intimidate press auto-succeeds for damage but costs 2 extra Patience and locks her threshold reveal for the session (she shuts down). Fast Talk and gentle Insight are the intended routes.
**Conceals:** she flagged the Kestrel invoices to Reyes; she saw Voss sign a Kestrel check; she is afraid of Hartney.
**Break (composure 0):** tells everything below the line, then flees town unless the player offers protection (decay step 2 fires).

> **[P1]** *"I barely knew Daniel. Different floors."*
> **Truth:** LIE · **Contradict:** reyes-cloud-notes (his notes credit "P." for flagging Kestrel), phone-records (six calls in his last two weeks) · **Tell:** she says his first name like a colleague, then corrects to "Mr. Reyes"
> **On collapse:** *"We talked sometimes. About work. That's not a crime."* −10

> **[P2]** *"Payroll and accounts payable don't even overlap."*
> **Truth:** EVASION · **Contradict:** vendor-ledger (her processing initials on Kestrel invoices) · **Tell:** answers a question you didn't ask

> **[P3]** *"Kestrel? They do facilities maintenance, I think."*
> **Truth:** LIE · **Contradict:** vendor-ledger (no work orders behind any Kestrel invoice) · **Tell:** she won't say the name a second time
> **On collapse:** *"I processed what was approved. Approved from upstairs."* −10 — first pointer at Voss

> **[P4]** *"I never look at vendor invoices closely. Volume's too high."*
> **Truth:** LIE · **Contradict:** reyes-cloud-notes ("P. caught it in the totals — third month running")

> **[P5]** *"Daniel seemed stressed lately. That's all I noticed."*
> **Truth:** TRUE (partial) · **Press (Insight):** success — she glances at the door when you say "stressed about what?"

> **[P6]** *"Nobody at Corvid would hurt anyone. It's a logistics company."*
> **Truth:** EVASION · **Tell:** eyes to the door; she's not reassuring you, she's reassuring herself

> **[P7]** *"Ms. Voss is a good CFO. Thorough."*
> **Truth:** TRUE, loaded · **Tell:** "thorough" lands like a warning, not praise

> **[P8]** *"I was home the night he died."*
> **Truth:** TRUE · no tells; corroborated if checked (building fob log)

> **[P9]** *"Mr. Hartney? He's just security. We don't talk."*
> **Truth:** LIE · **Tell:** hands go still · **Press (any, success):** *"He started taking his breaks near my desk. After Daniel died."* −5 self-inflicted

> **[P10 — threshold, composure ≤ 20 or after collapsing P1+P3]** *"He asked me if the Kestrel invoices looked right. I said no. That's all I did. I swear that's all."*
> Lands **petra-statement** on the record → **cc2 door satisfied.**

> **Below the line (break only):** she saw Voss sign a Kestrel check with her own stamp (supports cc3); Hartney's LLC is on the Kestrel payment list — she photocopied a page and mailed it to herself (seeds petra-ledger-page, the cc5 fallback).

---

## Deck V — Marla Voss
*Professional. Composure 75 · Stonewall 70 · Patience 8 (counsel on retainer).*
**Approach modifiers:** Intimidate presses harden her (+5 composure on your success or failure — she has eaten boardrooms). Fast Talk and Insight function. Evidence is what actually moves her: she never lies where paper can catch her *unless cornered* — her lies below are exactly the ones she believes are paperless.
**Conceals:** the skim is hers; she ordered Reyes "handled"; the calls to Hartney.
**Break (composure 0, or present phone-records + kestrel-payment-line together):** deflects onto Hartney *on the record* — lands **voss-deflection** (cc5 door): *"If Gil Hartney interpreted a personnel concern as something else, that is his crime. Get out, and speak to my attorney."* Then counsel arrives; session over.

> **[V1]** *"Daniel's death is a tragedy. Payroll was that man's whole life."*
> **Truth:** EVASION · **Tell:** condolence phrased in the past tense a beat too comfortably

> **[V2]** *"He never raised any concerns with me. Not once."*
> **Truth:** LIE · **Contradict:** reyes-cloud-notes ("Meeting w/ MV, 4:30 — bringing the Kestrel file"), safe-deposit-envelope (his memo of the meeting) · **Tell:** micro-pause before "never"
> **On collapse:** *"We had a scheduling conversation. He was agitated. I referred him to HR."* −10

> **[V3]** *"Kestrel is a legitimate facilities vendor. I sign hundreds of contracts."*
> **Truth:** EVASION/LIE · **Contradict:** kestrel-filings (registered agent: her ex-husband's firm) · **Tell:** the word "legitimate" arrives unprompted
> **On collapse:** *"My ex-husband's firm registers half the LLCs in this county."* −10, but she recovers stance — needs a second hit (V4) to stay down

> **[V4]** *"I don't handle vendor payments personally. That's two levels below me."*
> **Truth:** LIE · **Contradict:** kestrel-payment-line (endorsement stamps match her signature stamp)
> **On collapse:** −15 — paper she thought was clean; **her composure recovery stops for the rest of the session**

> **[V5]** *"Our security contractor reports to operations. I doubt I'd recognize him."*
> **Truth:** LIE · **Contradict:** hartney-llc-records or kestrel-payment-line (Hartney's LLC paid from her shell)

> **[V6]** *"I was at the Whitfield board dinner until midnight. Twenty witnesses."*
> **Truth:** TRUE — she did not pull the trigger. Presenting scene evidence against this backfires (+5). The alibi is real; the case against her is orchestration, and the player must feel that distinction.

> **[V7]** *"If money were missing, our auditors would have found it years ago."*
> **Truth:** EVASION · **Tell:** too smooth — the sentence has been said before, to a mirror

> **[V8]** *"Daniel had personal troubles. The marriage. Perhaps debts. Look there."*
> **Truth:** DEFLECTION (seeds the red herring) · **Press (Insight special):** the concern is rehearsed — she's *steering* · Presenting insurance-policy here as if agreeing marks the wrong path; facilitator notes the player took the bait

> **[V9]** *"I have never spoken to Mr. Hartney outside contract renewals."*
> **Truth:** LIE · **Contradict:** phone-records (three calls the night of the death, one at 12:20 AM)
> **On collapse:** −15 and go to threshold line V10

> **[V10 — threshold, composure ≤ 30]** *"Whatever Gil Hartney does with his evenings is his own business."*
> First open crack: she is cutting him loose. One more landed contradiction → break.

---

## Deck H — Gil Hartney
*Hard case. Composure 65 · Stonewall 60 · Patience 10.*
**Approach modifiers:** Fast Talk barely functions (−20 equivalent; he doesn't chat). Insight reads little (flat affect — tells below are the only ones he has). Intimidate works — but a *failed* Intimidate press gives him +10 and he starts enjoying the room.
**Conceals:** he was at the overpass; the lure call to Reyes; the Kestrel cut.
**Break (composure 0):** no confession. He goes silent and cold — but his alibi is collapsed **on the record**, and he stops accounting for the 11 PM hour entirely. That absence is pinnable. (Breaking him is *not* the win; the record is.)

> **[H1]** *"I do gates, cameras, and rounds. That's the job."*
> **Truth:** TRUE

> **[H2]** *"Night he died I was on shift at the depot till midnight. Check the roster."*
> **Truth:** LIE · **Contradict:** cctv-still (his truck at the Route 9 gas station, 11:40 PM) or towlot-statement · duty-roster is his *own exhibit* — pin it first and the contradiction hits −15 (alibi collapse)
> **On collapse:** *"Rosters run late all the time. Maybe I stepped out."* → H10 unlocked

> **[H3]** *"My truck never left the depot lot."*
> **Truth:** LIE · **Contradict:** cctv-still, towlot-statement · **Tell:** volunteers the truck before you asked about a vehicle

> **[H4]** *"Never met Reyes outside a badge check at the gate."*
> **Truth:** EVASION · **Contradict:** phone-records (his call to Reyes, 9:50 PM, night of death — the lure)
> **On collapse:** *"Maybe I called about a parking thing."* −10; the 9:50 call is now on the record next to an 11:40 sighting

> **[H5]** *"Kestrel? Never heard of it."*
> **Truth:** LIE · **Contradict:** kestrel-payment-line, hartney-llc-records

> **[H6]** *"My LLC does side work. Weddings, warehouses. All aboveboard."*
> **Truth:** EVASION · **Contradict:** hartney-llc-records (identical monthly amounts, no invoices) · **Tell:** "aboveboard" — his only borrowed word; he got it from someone who talks like V7

> **[H7]** *"Reyes was a sad little man. Suicide surprised nobody."*
> **Truth:** EVASION · **Tell:** the phrasing is *rehearsed* — near-verbatim to Voss's V8 framing. An Insight special here explicitly links the two decks.

> **[H8]** *"I don't own a handgun. Company policy, and I follow it."*
> **Truth:** TRUE, technically — the revolver was a pawn buy. Presenting revolver-trace ("big man, work jacket," no name) does *not* land as contradiction (+5 backfire) — teaches that texture evidence isn't proof.

> **[H9]** *"Ms. Voss and I talk contracts once a year, maybe."*
> **Truth:** LIE · **Contradict:** phone-records (three calls that night)

> **[H10 — threshold, composure ≤ 30 or after H2 collapse]** *"Maybe I drove out that way. Roads are public. You got nothing that says otherwise."*
> He is now placing *himself* near the scene on the record. Facilitator: deliver it flat, as a dare.

---

## Deck D — Dana Reyes
*Nervous witness, innocent. Composure 45 · Stonewall 30 · Patience 6.*
**Approach modifiers:** every Press costs 1 extra Patience — she is grieving, and the room knows it. Insight specials on her read *belief*: she believes every word she says.
**Conceals:** nothing about the murder. Only shame: the divorce filing was hers; she kept paying his insurance premiums anyway.
**Break (composure 0):** grief, then fury — nothing case-useful. She names no one because she knows nothing. *This deck exists to teach that breaking a suspect isn't winning; it's just breaking a person.* If the player pushes her to zero, the facilitator plays it in full.

> **[D1]** *"We were separated. I hadn't seen him in a month."*
> **Truth:** TRUE

> **[D2]** *"I don't know anything about his work. He stopped telling me years ago."*
> **Truth:** TRUE (mostly — see D5)

> **[D3]** *"The insurance? I'd forgotten it existed."*
> **Truth:** LIE (small, shamed) · **Contradict:** insurance-policy (premium receipts — *she* paid last month's)
> **On collapse:** *"He was falling apart. I filed the papers and then I kept paying his premiums. You explain that to me."* −10 — **exculpatory**: the lie protects embarrassment, not guilt. Facilitator plays it so the player feels the difference between a lie and a motive.

> **[D4]** *"Daniel wasn't suicidal. He was angry. Somebody did this to him."*
> **Truth:** TRUE · corroborates cc1; pinnable

> **[D5]** *"He said the job had gone rotten. He wouldn't say how. I stopped asking."*
> **Truth:** TRUE · texture for cc2; **Press (Insight special):** *"He said it the way you'd talk about a person, not a company."*

> **[D6]** *"I was at my sister's that night. We watched television. I can't tell you what."*
> **Truth:** TRUE · the alibi of someone with no alibi rehearsed — Insight reads honest

> **[D7]** *"He'd started locking his study. Him. The man lost his own keys twice a year."*
> **Truth:** TRUE · points at reyes-apartment (cc2 Research door)

> **[D8]** *"Ask that company. Ask the woman he worked for. He came home from that building smaller every day."*
> **Truth:** TRUE (instinct, not evidence) · pinnable as a lead, worthless as proof — the player should notice the difference

---

## Facilitator Notes for the Prototype Session

- **Session order matters:** Petra or Dana first teaches the verbs safely; Voss and Hartney assume the player has board evidence. A player who interrogates Voss with an empty case file should bounce off her — that is correct and should feel correct.
- **The cross-deck tell** (H7 ↔ V8: the same rehearsed framing in two mouths) is the deepest reward in the set — a player who pins both transcripts and strings them has connected the conspirators *through interrogation alone*. Note whether any playtester finds it.
- **Track for the success criteria** (interrogation-design.md): each Present — could the player articulate the contradiction beforehand? Each failed roll — could they say what changed? End of session — what did they *gain* from their worst interrogation?
- **The two backfire cards** (V6 board-dinner alibi, H8 revolver) are deliberate: real contradictions live on the listed cards only, and the player must learn that a *feeling* of guilt is not a contradiction. Watch whether backfires read as fair.
