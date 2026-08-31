# 0021. Major Wounds: the wound damage amount, the shock/Luck/table effect, cumulative minors, and the fatal-wound rescue window

## Status

Accepted — 2026-08-31. Resolves #111 (Layer 4, the Major Wounds effect). Builds on the damage/HP
path (ADR 0017) and the Layer 1 derived-characteristic recompute (ADR 0008), and follows the
named-adjudication-port precedent of ADR 0018 (#50) and ADR 0019 (#96).

## Context

Ch 2 gave the major-wound *threshold* (`AbilitySet.MajorWoundLevel`, half of total hit points,
rounded up — ADR 0008). Ch 6: Combat, "Damage & Healing" (pp.154-156) gives the *effect*, which was
unbuilt. `Wound` stored only a description, so no mechanic could classify a wound by its size. This
record covers the whole Ch 6 wound-classification surface: the wound damage amount, minor wounds,
major wounds (shock, the immediate Luck roll, and the Major Wounds Table), and fatal wounds (the
rescue window).

Only Ch 6, "Damage & Healing" (pp.154-156) and the Ch 2 hit-point rules were consulted. Every value
below was verified against the printed book text with `pdftotext -f/-l`, not the issue or
`engine-implementation-plan.md` (AGENTS.md invariant 2). The printed page numbers were re-verified:
Damage & Healing opens on p.154, Minor/Major Wounds and the first table rows are on p.155, and the
table's remainder plus Fatal Wounds are on p.156.

## Decision

### The wound damage amount — sourced (prerequisite)

`Wound` gains a `DamageAmount` (the hit points the wound dealt) alongside its description. Every site
that creates a `Wound` — the three `DamageResolver` apply paths (weapon `DamageRoll`, the #96
plain-int non-weapon overload, and knockout attacks) — now records the amount. The `Wound` is built
inside `DamageResolver`'s private `Apply`, where the applied damage is known, so the amount and the
hit-point subtraction can never diverge. This is the figure the major-wound trigger and First Aid's
per-wound cap (#109) compare against — not the character's remaining hit points, but the size of the
single blow (Ch 6, "Minor/Major Wounds", p.155).

### The major-wound trigger — sourced

A single wound is a major wound when its damage is **at least half the character's total hit points**
(Ch 6, p.155: "equal to or more than half the character's total hit points"). `MajorWoundResolver`
reuses the already-tested Layer 1 `AbilitySet.MajorWoundLevel` (Ch 2, p.14, rounded up) rather than
re-deriving the fraction. `IsMajorWound(woundDamage, target)` is the guard; `Resolve` throws if
called below the threshold.

### Shock — sourced, structured outcome (no roll)

On a major wound the character goes into shock (Ch 6, p.155): they "can fight on only for combat
rounds equal to their current remaining hit points," then fall unconscious. A character at **2 or
fewer hit points** after the wound "collapses immediately… and is unconscious for an hour." Shock is
returned as a `MajorWoundShock` structured outcome (`FightingRounds` = remaining hit points, or
`CollapsesImmediately`/`UnconsciousForAnHour` at ≤2 HP) for whichever piece runs a combat round to
apply — this resolver holds no encounter model, the same caller seam as #50/#96/#97. The collapse
threshold reuses `DamageRuleset.UnconsciousHitPointLevel` (2, Ch 2, p.13); the collapse duration
(one hour) is data. Shock is read from remaining hit points **before** any Luck-roll characteristic
drain, so a CON drain's later hit-point clamp cannot retroactively change the shock rounds.

### Permanence — the immediate Luck roll — sourced

On a major wound the character attempts a **Luck roll immediately** (Ch 6, p.155). Luck is POW's
characteristic roll (the ability ruleset names POW's roll "Luck," Ch 2), so this is the standard
POW×5 — a citation, not a house choice. **Success** → the wound "will heal cleanly and does not
inflict any permanent loss of characteristic points": no table roll, no drain, `AbleToFight` true
(shock still applies). **Failure** → the injury is permanent: roll the Major Wounds Table and
subtract the indicated points. Lost points may later be regained (out of scope: #109); the drain is
applied through `AbilitySet.Set` (via the #96 `InjuryDrain` helper, floored at the ruleset minimum)
so hit points, damage modifier, and major-wound level recompute live (ADR 0008), never baked.

### The Major Wounds Table — sourced, reproduced row-by-row

The 1D100 table (Ch 6, pp.155-156) is data in `Brp.Data/major-wound-ruleset.json`, loaded into a
banded `MajorWoundTable`/`MajorWoundRow` lookup mirroring `IllnessSeverityTable`/`DamageModifierBand`,
and reproduced **row by row** in tests (`NoirMajorWoundRulesetTests`, `[Theory][MemberData]` + an
exact-count `[Fact]` = 15 rows + a whole-1..100-coverage `[Fact]`). Each row carries only the
mechanical result — the characteristic loss(es), whether MOV is reduced by that loss, whether the
row's limb is unspecified, and the still-able-to-fight flag; the book's illustrative example causes
are not modeled (the printed grid is normative). A printed **00 is represented as 100**, matching
`IEntropySource.NextD100`.

| 1D100 | Characteristic loss | MOV reduced by loss | Still fight? |
|---|---|---|---|
| 01–10 | 1D3 DEX | yes | yes |
| 11–20 | 1D3 CHA | no | yes |
| 21–30 | 1D3 STR | no | yes (weapon, not shield) |
| 31–40 | 1D3 CON | yes | yes |
| 41–50 | 1D3 INT | no | yes |
| 51–60 | 1D6 DEX | yes | **no** |
| 61–70 | 1D6 CHA | no | yes |
| 71–80 | 1D6 STR | no | yes |
| 81–90 | 1D6 CON | yes | **no** |
| 91–92 | 1D6 CHA | no | yes |
| 93–94 | 1D6 DEX | no | yes |
| 95–96 | 1D6 DEX | no | yes (limb-side ruling) |
| 97–98 | 1D6 DEX | no | **no** |
| 99 | 1D3 each CHA, DEX, CON | no | **no** |
| 00 | 1D4 each from four characteristics (GM choice) | no | **no** |

- **MOV reduction is a structured outcome — sourced effect, house representation.** The rows that
  say "reduce MOV by the same amount" (01-10, 31-40, 51-60, 81-90) reduce MOV by the rolled
  characteristic loss. MOV is a flat value the engine does not derive from characteristics
  (`AbilitySet.Movement`, Ch 2, p.15), so there is nothing to recompute; the reduction is reported as
  `MajorWoundOutcome.MovementReduction` for the caller to apply. The rows the printed table does not
  annotate with a MOV clause (including 99, whose printed text omits it) report no reduction — the
  table beats the prose.

### Cumulative minor wounds — sourced

Several minor wounds the same day whose **total hit-point loss reaches the major-wound level** force a
**Luck roll or fall unconscious** (Ch 6, p.155). This is explicitly **not** a major wound: the Major
Wounds Table is **not** rolled ("do not roll on the Major Wounds Table for multiple minor wounds").
Separately, minor wounds reducing the character to **1 or 2 hit points** knock them out for up to an
hour. `ResolveCumulativeMinorWounds` takes the caller-tracked same-day total and reports both
(`ReachedMajorWoundEquivalent`/`FallsUnconscious` and `KnockedOutForAnHour`), consuming a d100 only
when the Luck roll is triggered.

### Fatal-wound rescue window — sourced, reuses the death-timing seam

A fatal wound is 0 or negative hit points (Ch 6, p.156): the character is knocked prone and cannot
act. Death results **unless** medical attention brings hit points to 1 or more **in the round of the
fatal wound or the round immediately after**. `DamageResolver.ResolvesToDeath` already models the
death hit-point test (ADR 0017); `MajorWoundResolver.SurvivesFatalWound` **adds the window**
(`FatalWoundRescueWindowRounds` = 1, the extra round after the wound round) and reuses
`ResolvesToDeath` for the hit-point test rather than duplicating the threshold. Applying the aid
itself, and holding the round clock, are caller seams (First Aid is #109).

### Gamemaster-discretion points become named adjudication ports — following ADR 0018/0019

Two calls the table hands to the gamemaster become first-class ports on a new
`IMajorWoundAdjudicator` (in `Brp.Core.Contests`, mirroring `IInjuryAdjudicator`), with a
`MajorWoundDecisionId` enum, canonical kebab-case ids (`MajorWoundDecisionIds.CanonicalId`), and a
`DefaultMajorWoundAdjudicator`. A separate triad from #96's `IInjuryAdjudicator` (Ch 7) keeps this
Ch 6 rule self-contained, exactly as #50 and #96 are separate triads. Return types are `Brp.Core`
values (no `Brp.Rules` dependency; AGENTS.md invariant 6).

| Decision id | What the book leaves open | Timing | Default | Source |
|---|---|---|---|---|
| `major-wound-limb-side` | Which side an unspecified limb wound (the 95-96 "left or right arm" row) falls on | narrative | left | **sourced** — Ch 6 p.155 |
| `major-wound-characteristics` | Which four characteristics the 00 row's loss strikes | pre-drain | STR, CON, DEX, INT (fixed order) | **sourced** — Ch 6 p.156 |

- **Limb side is narrative only — sourced port, house default.** The characteristic loss is identical
  whichever side is chosen, so this decides flavor, not mechanics. The book's suggested resolution is
  a 1D6 (1-3 left, 4-6 right); the neutral default returns left without rolling, and an adjudicator
  wanting the book's randomization rolls it. The decision *port* is sourced to the passage that leaves
  the call open; the *default answers* are a house choice of the most neutral reading (documented on
  `DefaultMajorWoundAdjudicator`). Tests drive every port with a deterministic stub.

### Incompatibility with hit locations — recorded for #112

The book states plainly (Ch 6, p.156, "Damage and Hit Locations") that the optional hit-location
system "is incompatible with Major Wounds and the two systems should not be used together," and the
Major Wounds text repeats it ("major wounds are incompatible with hit locations without considerable
gamemaster interpretation," p.155). This resolver applies loss to the single hit-point pool, as the
book does when hit locations are not used. **#112 must reconcile the two rather than layering hit
locations on top of Major Wounds** — recorded here so that issue inherits the constraint.

## Out of scope (per `orc-scope-filter.md` and the issue)

Not implemented here: hit locations (#112); First Aid / natural healing / Medicine, including
regaining lost characteristic points and applying the in-window fatal-wound aid (#109);
special-damage effects (#113); and the turn-economy application of shock / "unable to fight" /
unconsciousness to a running encounter (a caller seam — the resolver returns structured outcomes and
simulates no round). MOV is reported as a reduction amount, not mutated (the engine has no mutable
MOV).

## Consequences

- `Brp.Rules.Characters.Wound` gains `DamageAmount`; the three `DamageResolver` apply paths record
  it (built inside the private `Apply`).
- `Brp.Rules.Combat` gains `MajorWoundResolver` (with `MajorWoundOutcome`, `MajorWoundShock`,
  `MajorWoundCharacteristicResult`, `CumulativeMinorWoundOutcome`), the `MajorWoundRuleset` /
  `MajorWoundTable` / `MajorWoundRow` / `MajorWoundLoss` / `MajorWoundGamemasterChoice` data model.
  `Brp.Data` gains `major-wound-ruleset.json` and `NoirMajorWoundRuleset`. `Brp.Core.Contests` gains
  `IMajorWoundAdjudicator`, `MajorWoundDecisionId`/`MajorWoundDecisionIds`,
  `DefaultMajorWoundAdjudicator`, and the `BodySide` enum.
- The shock turn-economy, the MOV reduction, the First-Aid application of the fatal-wound rescue and
  characteristic-point regain, and the hit-location reconciliation all need caller/round/time state
  this resolver does not hold. As in ADR 0018/0019, the resolver computes the harm and names the open
  calls; whichever piece orchestrates a running encounter wires the ports and the timing.
