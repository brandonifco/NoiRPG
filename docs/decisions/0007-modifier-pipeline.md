# 0007. Modifier ordering, and difficulty that does not stack

## Status

Accepted — 2026-08-29. Resolves #2 and #3.

Two earlier drafts of this record were wrong. See "How this record was got wrong twice"
below; the mistakes are kept because the pattern in them is the useful part.

## Context

Computing an effective chance requires two things the engine cannot leave open: the
order in which different kinds of modifier apply, and whether two sources of the same
difficulty compound. A gamemaster settles both by judgment. An engine has to commit.

The design pillar in `noir-rpg-framework.md` is that the rating shown to the player is
the real probability, so whatever the engine does must be explainable in one line of UI.

## Decision

### Ordering — sourced

```
Gate → Override → PermanentAdditive → Multiplicative → SituationalAdditive → Clamp
```

Ch 5: System, "Modifying Action Rolls" → "Situational Modifiers" states that a
situational modifier is applied *after* a skill is modified for being Difficult or Easy,
expressly so that situational modifiers are not themselves doubled or halved. The same
passage states that modifiers which are *permanent* — integral to the skill rather than
external — are figured into the rating *before* it is doubled or halved.

That is two additive stages either side of the multiplicative one.

Gate precedes everything, following from Automatic ("no roll necessary") and Impossible
("all attempts fail"). Override before the additive stages is **not** a book rule; it is
an engineering choice, and is recorded as one. The book's override cases are stated as
flat replacement chances, which makes replacing-then-adjusting the only coherent reading.

Halving rounds **up** — Ch 5, "Difficult Actions" says so explicitly.

Worked examples the implementation reproduces:

| Case | Result |
|---|---|
| 65%, firing while engaged in combat (Difficult), dim light (situational −20%) | `65 ÷ 2 = 33`, then `−20` → **13%** |
| 65%, permanent +10, Difficult | `(65 + 10) ÷ 2` → **38%** |

**Why it matters:** under the rejected ordering a stated −20% quietly becomes −10%
whenever conditions are also bad, because it is halved along with everything else. The
source's ordering preserves the weight a modifier says it has.

### Difficulty grades — sourced

Automatic (no roll), Easy (doubled), Average (unmodified), Difficult (halved, round up),
Impossible (no roll; the book permits a gamemaster to allow a flat 1% at their
discretion, which the engine does not yet express).

**Easy and Difficult cancel pairwise, and this is sourced** — Ch 7, "Firing Into Combat"
works exactly this cancellation, where a Difficult condition and an Easy one offset.

### Difficulty does not stack — a house rule, but a supported one

Any number of sources of "Difficult" produce one halving. Easy and Difficult cancel.

The book does not state whether two Difficult grades compound, so the rule itself is
ours. It is not unsupported, though, and three passages point the same way:

- **Augments** (Ch 3) adjust difficulty by *one step*, "such as turning a Difficult roll
  into an Average one", and state that only one degree of adjustment is possible.
  Difficulty is modelled as a position on a ladder, not a multiplier stack.
- **Disguise** (Ch 3) addresses multiple Difficult conditions directly, and answers with
  an *additional flat penalty* rather than a second halving.
- **Simple Fatigue** (Ch 2) expresses severity beyond Difficult as its own named factor,
  never as two chained halvings.

Rejected: compounding. Stacked halvings reach odds where the roll stops feeling like a
decision, and a 17% arrived at by two invisible halvings reads as the game cheating even
when the arithmetic is defensible.

### Range bands are out of scope here

An earlier draft specified range bands in this record. It was wrong in every particular
and has been removed rather than corrected.

The book's actual bands (Ch 6, "Missile Weapons"; Ch 7, "Extended Range") are mostly the
difficulty grades themselves — point blank is *Easy*, medium range is *Difficult* — with
long range a distinct one-fifth factor. There is no general "beyond three times range is
impossible" rule; that was invented. Correctly implementing the bands also requires
DEX-derived point-blank distance, weapon-class exceptions, and targeting equipment.

That is combat-layer work, not modifier-pipeline work. Tracked separately.

Consequence worth stating: because medium range *is* a Difficult grade, it collapses
with other Difficult conditions under the non-stacking rule above. The earlier draft
introduced a separate multiplicative tier specifically to prevent that collapse, and in
doing so contradicted the text it should have been citing. The collapse is the book's
behaviour, not a bug to engineer around.

## How this record was got wrong twice

The first draft framed the ordering as an open question with two candidate answers. The
book specifies it, and specifies a three-stage scheme neither candidate described.

The second draft fixed the ordering but kept a range-band section written the same way —
asserted from memory, never checked. A conformance pass found every multiplier and every
threshold in it wrong, plus a fabricated cutoff.

Both errors have the same shape: a mechanical claim written without opening the book,
sitting next to claims that were checked, with nothing on the page distinguishing them.

The process correction, which this record now follows: **every mechanical claim in an ADR
either cites the chapter it was verified against, or says plainly that the source is
silent and the decision is ours.** Sections above are marked "sourced" or "house rule"
for exactly this reason. An unmarked assertion is a defect regardless of whether it
happens to be true.

## Consequences

- Ordering is a named policy holding the stage list as data, not implicit in call order,
  covered by a test that fails under the collapsed-additive alternative.
- Multipliers are rational rather than a closed enum, so the pipeline can express factors
  the difficulty ladder does not cover. It no longer claims to model any particular one.
- Additive modifiers carry a kind. Situational is the default because it is the common
  case; permanent is the exception and must be requested.
- Every modifier carries a source label and the chain renders its own derivation, with
  the two additive stages distinguishable — "why did my −20% apply after the halving" is
  precisely the question that rendering exists to answer.
- Gates short-circuit without consuming entropy, or a save-file replay would desynchronise
  depending on whether an impossible action was attempted.
- The 5% floor from ADR 0006 keys on the *base* chance and survives arbitrary penalties.
  It is applied by the resolver, after this pipeline, not inside it.
- The gamemaster's discretionary 1% on an Impossible action is not currently expressible,
  since gates short-circuit before overrides. Latent, not yet wrong.
