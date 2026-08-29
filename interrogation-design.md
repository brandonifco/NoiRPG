# Interrogation — Design Doc & Paper Prototype
*Draft v0.1 — 2026-08-17. Answers design-review-notes.md point 3: the load-bearing minigame gets its own design and a paper test before any code.*

## The Problem to Solve

Interrogation is the mode players will repeat most, and its genre has the worst failure precedent: L.A. Noire's truth/doubt/lie problem, where the player's actual reasoning ("his alibi contradicts the phone records") had to be translated into an opaque verb ("Doubt?") whose consequences they couldn't predict. The player was reasoning correctly and still guessing at the input layer.

**Design law for this system: every input verb must directly express a thought the player is actually having.** If the player's deduction and the button they press ever come apart, the design has failed regardless of how good the meter feels.

## Core Loop

Interrogation is turn-based, statement by statement. The suspect makes a statement; the player responds with one of four verbs; the suspect reacts; repeat. The scene is played against two visible resources:

- **Composure** (suspect's) — derived from their POW/CHA. Chipped by pressure and contradictions. Thresholds unlock slips, revised statements, and admissions; at zero the suspect *breaks*.
- **Patience** (scene clock) — ticks down each turn. At zero the interrogation ends: the lawyer arrives, the suspect walks, the captain calls time. Patience is what makes each verb a spend, not a free action.

## The Four Verbs

1. **Listen** — accept the statement and let them keep talking. Costs nothing, recovers a little suspect composure. Every statement heard goes **on the record** (see below). The safe default that is never a trap: sometimes letting a liar run builds the contradiction you'll spring later.

2. **Press** — challenge the statement on manner, not evidence: Insight, Intimidate, or Fast Talk (player's choice of approach) opposed by the suspect's Stonewall. Success chips composure; a special success also reveals a **tell**. Failure hardens them (composure recovery, and repeated failed pressing burns extra patience). Pressing is the dice-flavored verb — texture and margin, per the clue rule philosophy.

3. **Present evidence** — the deduction verb, and the heart of the fix. The player picks a specific item from the case file and asserts it contradicts the current statement. **Whether the contradiction is real is never rolled** — if the evidence genuinely contradicts the statement, the hit always lands (major composure damage, statement collapses, suspect must revise). The d100 roll grades only the *reaction*: how much extra spills out, whether they name an accomplice in the scramble. Wrong evidence backfires: the suspect gains composure and learns what you don't know. The player's reasoning is deterministic; only the fallout has texture. This is the clue rule applied to interrogation.

4. **Change topic** — move to another statement thread. Costs a point of patience; resets any press momentum; sometimes the correct play when a thread is hardening.

## Tells and the Record

- **Tells**: an Insight special (via Press, or passively at scene entry for high-Insight builds) marks a statement on the transcript as *shaky* — the system's truth-state leaking to the player in a diegetic way. Tells align player reasoning with system truth instead of asking the player to mind-read the designer.
- **The record**: every statement heard is captured on the transcript and becomes pinnable evidence on the case board. This is how a "failed" interrogation stays a branch, not a wall: even a suspect who stonewalls to zero patience has committed to statements — statements that other evidence can contradict *later*, reopening them in a second session from a position of strength. Core clues that route through interrogation are Three-Doors guaranteed: one of the doors is always a statement that ends up on the record even in the worst run.

## Breaking, and Why It Isn't Always Winning

At zero composure a suspect breaks — but break behavior is authored per suspect archetype: confession, lawyering up mid-sentence, a panicked partial truth that implicates someone new, or collapse into something the player must now feel bad about. A broken suspect is a *changed* suspect; some cases are better served by a suspect left intact and unaware of what you know. Breaking is a tool, not the win condition; the win condition is the case board.

## Build Expression

The three press approaches map to backgrounds: the ex-cop's Intimidate hits composure hardest but risks hardening or false confessions; the ex-journalist's Fast Talk opens rapport routes Intimidate closes; high Insight turns the transcript into a marked-up document. Suspect archetypes (hard case, nervous witness, professional, true believer) respond differently to each approach, so the same interrogation plays differently per build — replayability from builds, as the framework promises.

## Anti-Slot-Machine Principles

1. The deduction verb (Present evidence) never rolls for truth — dice grade reactions, never verdicts.
2. No verb is ever strictly correct-but-failed: a failed Press changes the scene state legibly (hardening you can see), never silently.
3. Composure is visible, always. Hidden meters are how meters become slot machines.
4. Patience makes turns an economy, so "just press repeatedly" is a losing strategy by structure, not by punishment.

## Paper Prototype

**Materials**: statement cards per suspect (each card: the statement, its truth value, which evidence items contradict it, tell notes); evidence hand for the player; two token pools (composure, patience); d100.

**Roles**: one designer plays the suspect from a one-page script (archetype, break behavior, what each statement conceals); the playtester plays the detective with a build sheet (skill ratings for the press approaches and Insight).

**Procedure**: run a 10–15 statement interrogation. Suspect reads statements in script order with authored branch points; player responds with the four verbs; opposed rolls resolved on d100 in the open. Run each scripted suspect against two different builds minimum.

**Success criteria** (all four must hold to pass the gate):
1. Players choose Present-evidence based on actual contradictions they can articulate ("this conflicts with the bank statement") in the majority of presentations — the verb expresses their thought.
2. After any failed roll, the player can say what changed in the scene and what they'd try next — failure reads as state, not noise.
3. A playtester who runs out of patience or faces a hardened suspect still identifies something they gained (statements on record, a tell, a name) — failure is a branch.
4. Playtesters ask to rerun the same suspect with a different build unprompted — the mode carries repetition.

**Kill criteria**: if playtesters converge on spamming one verb, or describe the loop as "wearing the meter down," the structure is a slot machine with extra steps — stop, redesign, do not proceed to engine work (development-plan.md, Phase 1 gate).

## Open Questions

- Does Patience regenerate between sessions with the same suspect, or is total access to a suspect a case-level budget?
- Should Composure damage be visible as a number or only as authored behavior tiers (performance over precision)?
- False-confession risk on Intimidate-heavy builds: mechanical trap, authored consequence, or both?
