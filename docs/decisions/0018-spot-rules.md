# 0018. Situational combat spot rules: five modifier producers, and the named adjudication ports for the book's open calls

## Status

Accepted — 2026-08-31. Resolves #50 (Layer 4 piece F, situational combat spot rules). Piece F was
explicitly deferred to this work in ADR 0015 and ADR 0016 (0016's "What this piece does not build"
lists "Spot rules — piece F"). Builds on ADR 0007 (the modifier pipeline) and ADR 0014 (range
bands), whose firing-into-combat treatment this record reconciles.

## Context

Ch 7: Spot Rules collects a long list of situational combat rules. `orc-scope-filter.md` and the
issue's acceptance criteria narrow piece F to five in-scope situational *combat* rules — Ambushes,
Backstabs and Helpless Opponents, Cover, Darkness, and Firing Into Combat — and explicitly exclude
the rest (see "Out of scope" below).

The book's own foundation for these rules is Ch 5: System, "Modifying Action Rolls" (p.132): every
one of them adjusts a roll either by a **difficulty grade** (Easy/Difficult) or by a **situational
percentage**, and Ch 5 fixes the ordering between the two — "any situational modifier is applied
after a skill is modified due to being Difficult or Easy. This way, the modifiers are not doubled
or halved." That is exactly ADR 0007's pipeline. So the whole of piece F is a set of **modifier
producers** feeding the existing pipeline, not a new resolution path.

Ch 7 (pp.162–173) and the Ch 5 Situational Modifiers table (p.133) are the sole sources consulted.
`engine-implementation-plan.md` is not authoritative for mechanics (AGENTS.md invariant 2); it is
not used here.

## Decision

### The mechanism — each spot rule is a modifier producer, not a new path — sourced

`SpotRuleResolver` (static, in `Brp.Rules.Combat`) mirrors `RangeBandResolver`: a per-rule method
returns the rule's `Brp.Core.Modifiers.Modifier` contributions, and `Evaluate` concatenates them
with a roll's other modifiers and runs `ModifierPipeline.Evaluate`. A rule that makes an action
Easy or Difficult emits a `DifficultyModifier`, so it takes part in ADR 0007's non-stacking
collapse (a spot-rule Difficult and a range-band Difficult halve once, not twice; an Easy and a
Difficult cancel pairwise). A rule that applies a flat percentage emits a **situational**
`AdditiveModifier`, so its stated weight survives the difficulty stage (Ch 5, p.132). A rule that
forbids an action emits a `GateModifier` (Impossible). No spot rule needs an exclusive override, so
unlike range bands the contributions are plain composable modifiers — no `RangeBandOutcome`-style
closed hierarchy is required.

Per AGENTS.md invariant 7, the book's **percentage** values are data in
`Brp.Data/spot-rule-ruleset.json` (loaded by `NoirSpotRuleRuleset` into `SpotRuleRuleset`), not C#
constants. The rules that work purely by difficulty grade carry no per-rule number, because the
Easy/Difficult multipliers already live as data on `ModifierPolicy` (ADR 0007) — duplicating them
per rule would be dead configuration, the same mistake ADR 0014 corrected with its removed
`LongRangeMultiplier` field.

### The five implemented rules

Each rule's book modifier and citation:

| Rule | Book modifier | Citation |
|---|---|---|
| **Ambushes** | Attack **Easy** (missile unseen/seen; hand-to-hand vs. unaware target); attack **unmodified** vs. an aware target. Defense **forbidden** (missile unseen), **Difficult** (hand-to-hand vs. unaware target), or normal. | Ch 7, "Ambushes", p.162 — sourced |
| **Backstabs & Helpless Opponents** | Attack **Easy** (both cases). Unprotected back: defense **Difficult** only if the target made a Difficult Listen/Sense, else none. Helpless: defense **forbidden**. "No additional damage." | Ch 7, "Backstabs and Helpless Opponents", p.164 — sourced |
| **Cover** | Attack **Difficult** (book worked example: 72% → 36%). | Ch 7, "Cover", p.169 — sourced |
| **Darkness** | Situational **−20%** (semi-darkness) / **−50%** (pitch black); **halved** when the opponent is detected via a Difficult Sense/Listen roll. | Ch 7, "Darkness", p.169, drawing on the Ch 5 Situational Modifiers "Environment" row, p.133 — sourced |
| **Firing Into Combat** | **−20%** firing *into* a melee; **Difficult** firing *while engaged*; a point-blank Easy cancels the while-engaged Difficult when both are in close range. | Ch 7, "Firing Into Combat", p.173 — sourced |

Details worth recording:

- **Ambushes (p.162) — sourced.** Modeled as four `AmbushKind` cases, each producing the attacker's
  attack modifier and the target's defense modifier for the requested role. Only the initial ambush
  round carries these; after it, "normal combat, no surprise modifiers" resumes. The
  hand-to-hand-aware target's "cannot retaliate or move until the next combat round" is a
  turn-economy effect on its *next* action, not a modifier on any roll, and is deliberately **not**
  emitted as a `Modifier` (a caller that sequences rounds — ADR 0015's `CombatActionRequest` — owns
  it). Armor "defends normally… unless the attackers are using aimed attacks to bypass armor" is a
  damage/armor concern (piece D), out of scope here.
- **Backstabs & Helpless (p.164) — sourced.** Both cases are Easy attacks and, per the book, do
  **no additional damage** — the Easy grade is the whole benefit, not a damage bonus. The helpless
  target "cannot make a dodge or parry" regardless of detection; the unprotected-back target may
  make a Difficult defense only if it detected the attacker.
- **Cover (p.169) — sourced.** The Difficult attack is the only pre-roll modifier. The "a roll over
  the adjusted chance but under the normal rating hits the obstacle" band is derivable from the
  resolved `ModifierChain` (the interval between the Difficult effective chance and the unmodified
  base) and is not re-encoded as a separate mechanic.
- **Darkness (p.169; Ch 5 p.133) — sourced.** The rule points at the Situational Modifiers table;
  the "Environment" row supplies −20% (darkness/semi-darkness) and −50% (pitch black). The detection
  halving ("reduce the darkness modifier by half") scales the penalty magnitude by the ruleset's
  1/2 fraction, **rounded toward zero** so the reduced penalty favors the roller (the codebase
  convention for rounding a penalty; ADR 0007). For the printed values (−20→−10, −50→−25) the
  rounding is exact and does not bite. Light sources and Powers that offset darkness are out of
  scope (Powers are cut entirely; Light Sources is a separate rule).

### Firing into combat — reconciling ADR 0007 and `AdditiveModifier` — sourced

Two earlier citations disagreed on this rule: ADR 0007 recorded firing into combat as **Difficult**,
while `AdditiveModifier`'s own remarks recorded it as **−20%**. The book (p.173) shows both were
half the rule: "Firing a missile weapon **into** combat is modified by −20%, while firing a missile
weapon **while engaged** in combat is Difficult. However, if the attacker and the target are both
within close combat range, the attack is Easy (for Point-blank Range), so the Difficult and Easy
modifiers cancel one another."

`SpotRuleResolver.FiringIntoCombat` produces the −20% additive for the into-a-melee condition and
the Difficult grade for the while-engaged condition, independently. It deliberately does **not**
re-emit the point-blank Easy: that Easy is `RangeBandResolver`'s contribution for
`RangeBand.PointBlank` (ADR 0014), and when a caller composes it alongside this rule's Difficult,
ADR 0007's non-stacking collapse cancels the pair automatically. Producing the Easy here as well
would double-count it. This is the same collapse the existing
`RangeBandResolverTests.Point_blank_and_a_difficult_condition_cancel_pairwise` (p.173) already
asserts with a hand-rolled `DifficultyModifier.Difficult("firing into combat")`; that test is left
untouched and green, and a new
`SpotRuleResolverTests.Firing_while_engaged_difficult_is_cancelled_by_a_point_blank_easy_when_both_in_close_range`
shows this resolver produces exactly what that test hand-rolls. No parallel or duplicate mechanic is
introduced.

The stray-ally risk (a roll between the skill rating and the −20% chance may hit a bystander, chosen
by Luck rolls; "the attacker is not eligible for an experience check" on such a shot) is a
post-roll gamemaster call, routed to an adjudication port (below), not a modifier.

### The gamemaster-discretion points become named adjudication ports

Ch 7's spot rules leave several calls "at the gamemaster's discretion." Following the `IAdjudicator`
precedent (ADR / `engine-implementation-plan.md` decision D5), each becomes a first-class named port
rather than a silent hardcoded choice: `ISpotRuleAdjudicator` (in `Brp.Core.Contests`) with a
`SpotRuleDecisionId` enum, canonical kebab-case ids (`SpotRuleDecisionIds.CanonicalId`), and a
`DefaultSpotRuleAdjudicator` stub whose defaults are the minimal-assumption reading. The port's
return types are `Brp.Core.Contests` values (not `Brp.Rules.Combat` types) so the port stays within
`Brp.Core` and does not invert the layer dependency (AGENTS.md invariant 6) — the reason
`DarknessSeverity` lives beside the port rather than in the combat-rules layer.

| Decision id | What the book leaves open | Timing | Default | Source |
|---|---|---|---|---|
| `darkness-severity` | Which darkness tier applies (semi-darkness −20% vs. pitch black −50%) | pre-roll | `SemiDarkness` (asserts the milder condition) | **sourced** — Ch 7 p.169; Ch 5 p.133 |
| `cover-penetration` | Whether the shot's damage penetrates the cover to reach the target | post-roll | `StoppedByCover` (matches the base rule's own "hits the obstacle" outcome) | **sourced** — Ch 7 p.169 |
| `cover-extent` | How much of the target the obstacle screens / which hit locations are protected | pre-roll (announced) | `PartiallyProtected` (the rule's own premise) | **sourced** — Ch 7 p.169 |
| `backstab-helpless-reprieve` | Whether a helpless target gets a POW×1 reprieve that stays the attacker's hand this round | pre-action | `NoReprieve` (the book says the GM *may* allow it) | **sourced** — Ch 7 p.164 |
| `firing-into-combat-stray-target` | Which bystander (if any) a stray shot hits, via Luck rolls | post-roll | none struck | **sourced** — Ch 7 p.173 |

The decision *ports* are sourced to the book passages that leave the call open; the *default
answers* are a **house choice** of the most neutral reading (each documented on
`DefaultSpotRuleAdjudicator`). `cover-penetration` and `cover-extent` carry only coarse outcomes
because the damage arithmetic and hit-location systems they would feed are deferred pieces, out of
scope here; the ports exist so those calls are named now rather than silently fixed. Tests drive
every port with a deterministic stub.

## Out of scope (per `orc-scope-filter.md` and the issue's acceptance criteria)

Not implemented here: falling, poison, and disease (a sibling issue); damage numbers, wounds, and
hit locations; the fumble tables; fatigue points, dying blows, and aging; and every other Ch 7 spot
rule outside the five combat rules above (Aimed Attacks, Big and Little Targets, Both Sides
Surprised, Broken Weapons, Chases, Disarming, Knockback, Fortified Positions, and so on). Light
Sources and any Power that offsets darkness are likewise out of scope.

## Consequences

- `Brp.Rules.Combat` gains `SpotRuleRuleset`, `SpotRuleResolver`, and the `SpotRuleRole`,
  `AmbushKind`, and `BackstabKind` enums. `Brp.Data` gains `spot-rule-ruleset.json` and
  `NoirSpotRuleRuleset`, all data-driven per invariant 7. `Brp.Core.Contests` gains
  `ISpotRuleAdjudicator`, `SpotRuleDecisionId`/`SpotRuleDecisionIds`, `DefaultSpotRuleAdjudicator`,
  and the ruling types (`DarknessSeverity`, `CoverPenetrationRuling`, `CoverExtentRuling`,
  `HelplessReprieveRuling`, `StrayTargetRuling`).
- The firing-into-combat mechanic now has one home: this resolver produces both the −20% and the
  Difficult, reconciling ADR 0007 and `AdditiveModifier`'s remarks. The range-band tests' hand-rolled
  `DifficultyModifier.Difficult("firing into combat")` remains valid and green; a future cleanup
  could route them through this resolver, but it is not required for correctness.
- Ambush "cannot retaliate or move," cover's obstacle-hit band, the cover-penetration damage roll,
  the stray-target Luck rolls, and the helpless reprieve's effect on the round all need caller/round
  state this pre-roll producer does not hold. The seam mirrors ADR 0015/0016: the spot-rule layer
  produces modifiers and names the open calls; whichever piece orchestrates a running encounter
  wires the ports and applies the turn-economy effects.
- If a later reading finds a distinction that matters for cover penetration or extent (once damage
  and hit locations exist), the ports already carry the decision points — only the consuming piece,
  not this producer, would change.
