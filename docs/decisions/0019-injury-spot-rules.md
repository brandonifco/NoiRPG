# 0019. Injury/effect spot rules: falling, poison, and disease, flowing through the damage and characteristic-drain paths

## Status

Accepted — 2026-08-31. Resolves #96 (Layer 4, the injury/effect half of the Ch 7 spot rules). The
sibling of ADR 0018 (#50, the situational-*modifier* half): where 0018's rules produce
`Modifier`s for the roll pipeline, these rules produce **damage or characteristic drain** and flow
through the damage/HP path (ADR 0017) and the Layer 1 derived-characteristic recompute (ADR 0008).
Together #50 and #96 complete the Ch 7 spot-rules surface for Layer 4.

## Context

Ch 7: Spot Rules collects situational rules of two natures. #50 delivered nature (a): rules that
modify an action roll. This record covers nature (b): rules that inflict harm — **Falling** (p.171),
**Poison** and **Poison Antidotes** (pp.175-176), and **Disease** with the **Illness Severity Table**
(pp.169-170). These do not touch the modifier pipeline; they remove hit points or lower a
characteristic, and a lowered characteristic must recompute its derived values (hit points, major
wound level, category modifiers) rather than being baked (AGENTS.md; ADR 0008's live-recompute
`AbilitySet`).

Ch 7 (pp.169-176) and the associated Ch 2 hit-point rules are the sole sources consulted. Each value
below was verified against the printed book text, not the issue or `engine-implementation-plan.md`
(AGENTS.md invariant 2).

## Decision

### The mechanism — reuse the existing damage and drain seams — sourced

Three static resolvers in `Brp.Rules.Combat` (`FallingResolver`, `PoisonResolver`, `DiseaseResolver`)
compute harm from ruleset data plus injected entropy and apply it through the mapped, already-tested
calls:

- **Hit-point loss** (falling, poison-to-HP, minor disease) goes through a **new non-weapon overload
  of `DamageResolver.ApplyDamage`** — `ApplyDamage(AbilitySet, WoundTrack, int hitPointDamage,
  DamageRuleset, string)` — which reaches the same private `Apply` the weapon overload uses, so
  hit-point tracking (Ch 2, p.13: HP may go negative) and condition classification
  (`Unaffected`/`Unconscious`/`FatallyWounded`, from `DamageRuleset`) are identical. No fake
  `DamageRoll` is fabricated for non-weapon damage.
- **Characteristic drain** (poison-to-characteristic, disease) goes through `AbilitySet.Set` (via a
  shared `InjuryDrain` helper that floors the new value at the characteristic's ruleset minimum), so
  `MaximumHitPoints`, `MajorWoundLevel`, and `DamageModifier` recompute live (ADR 0008).
- **POT vs CON** reuses `ResistanceResolver.Resolve(active: POT, passive: CON, entropy)` — binary
  `Succeeded` (poison overcomes CON), `ResistancePolicy.Standard`.
- The **CON×N recovery ladder** reuses `AbilityRuleset.CharacteristicRoll(CON, n)` →
  `AbilityResolver.Resolve`.

All book numbers live in `Brp.Data/injury-ruleset.json` (loaded by `NoirInjuryRuleset` into the
immutable `InjuryRuleset` = `FallingRuleset` + `PoisonRuleset` + `DiseaseRuleset`), per AGENTS.md
invariant 7. The Illness Severity Table is a banded lookup (`IllnessSeverityTable` /
`IllnessSeverityBand`) mirroring `DamageModifierTable` / `DamageModifierBand`, and is reproduced
row-by-row in tests (`NoirInjuryRulesetTests`).

### The implemented rules

| Rule | Book value | Citation |
|---|---|---|
| **Falling — base** | 1D6 per 3 m fallen | Ch 7, "Falling", p.171 — **sourced** |
| **Falling — force** | dice rolled doubled when thrown with considerable force | Ch 7, p.171 — **sourced** |
| **Falling — small SIZ** | SIZ ≤ 5: reduce damage by 1D6 | Ch 7, p.171 — **sourced** |
| **Falling — large SIZ** | SIZ > 20: +1D6, and another 1D6 per fraction of 20 above, cumulative with force | Ch 7, p.171 — **sourced** (band interpretation is a house reading, below) |
| **Falling — armor** | half protection up to 3 m | Ch 7, p.171 — **sourced** (armor beyond 3 m is a house reading, below) |
| **Poison — overcome** | overcomes CON → full POT as damage | Ch 7, "Poison", p.175 — **sourced** |
| **Poison — resisted** | does not overcome CON → half POT, round up | Ch 7, p.175 — **sourced** |
| **Poison — target** | damage to total HP or to a characteristic | Ch 7, p.175 — **sourced** |
| **Poison — onset** | 3 combat rounds (fast) / 3 full turns (slow) default | Ch 7, p.175 — **sourced** |
| **Poison — two doses** | two doses = two separate resistance rolls | Ch 7, p.175 — **sourced** |
| **Antidote** | POT taken ≤ 6 full turns before poisoning subtracts from poison POT before damage | Ch 7, "Poison Antidotes", p.176 — **sourced** |
| **Disease — contract** | Stamina roll: success avoids, failure contracts | Ch 7, "Disease", p.169 — **sourced** (Stamina = CON's roll, below) |
| **Disease — minor cost** | 1–2 HP (1D2) + 1D6 fatigue over a few days | Ch 7, p.169 — **sourced** |
| **Disease — recovery ladder** | day 2 CON×2, day 3 CON×3, +1 multiplier/day; fumble −×1; strenuous −×1 per condition | Ch 7, p.169 — **sourced** |
| **Illness Severity Table** | 0 None / 1 Mild (wk) / 2 Acute (day) / 3 Severe (hr) / 4+ Terminal (min) | Ch 7, "Illness Severity Table", p.170 — **sourced**, reproduced row-by-row |

Details worth recording:

- **Stamina is CON's characteristic roll — sourced.** The book says only "make a Stamina roll" here,
  but the ability ruleset (Ch 2) names CON's roll "Stamina," so the contraction roll is the standard
  CON roll (CON×5). This is a citation, not a house choice.
- **Characteristic points lost = number of failed CON rolls — house procedural reading (marked).**
  The book (p.170) says "the first characteristic point is lost within 24 hours... each successive
  loss is added to the total whenever the CON roll is made to recover," and cross-indexes the failure
  count on the Illness Severity Table to give a *rate* (per week/day/hour/minute). The resolver drains
  one point per failed recovery roll and reports the table's degree as the rate; it does not simulate
  wall-clock time. Recorded here rather than left silent.

### House readings of ambiguous or silent prose (marked)

- **Falling large-SIZ bands.** "adding an extra 1D6 damage if the character's SIZ is over 20 and
  another 1D6 for every fraction of 20 after that" is read as **one extra die per started 20-point
  band above 20** (`ceil((SIZ − 20) / 20)`: SIZ 21-40 = one die, 41-60 = two, …), treating the
  "over 20" die and the "fraction of 20" dice as one series rather than double-counting them. The
  alternative literal reading (a flat +1D6 *plus* one per fraction) would give every over-20 faller a
  minimum of +2D6; the chosen reading is the more conservative.
- **Falling armor beyond 3 m.** The book grants "half protection... up to three meters" and is silent
  on longer falls. The resolver applies half the armor value within 3 m and **no** armor beyond it.
  Half-armor rounds toward zero (`RoundingMode.Down`).
- **Falling small-SIZ / large-SIZ as dice-pool modifiers.** Force doubles the base **dice count**;
  the large-SIZ dice are added and the small-SIZ 1D6 is subtracted as separately rolled dice, with
  the total floored at zero — matching the book's "reduce by 1D6" / "adding an extra 1D6" language.

### The gamemaster-discretion points become named adjudication ports

Following the `ISpotRuleAdjudicator` precedent (ADR 0018), Ch 7's "at the gamemaster's discretion"
clauses in these rules become first-class ports: `IInjuryAdjudicator` (in `Brp.Core.Contests`) with
an `InjuryDecisionId` enum, canonical kebab-case ids (`InjuryDecisionIds.CanonicalId`), and a
`DefaultInjuryAdjudicator` whose defaults are the minimal-assumption reading. Return types are
`Brp.Core` values (no `Brp.Rules` dependency; AGENTS.md invariant 6).

| Decision id | What the book leaves open | Timing | Default | Source |
|---|---|---|---|---|
| `falling-surface` | How the surface / intervening obstacles adjust falling damage | post-roll | no adjustment (0) | **sourced** — Ch 7 p.171 |
| `poison-onset` | Which onset category (fast/slow), and any bespoke delay | pre-effect | fast-acting, printed default | **sourced** — Ch 7 p.175 |
| `antidote-cross-type` | How much of a mismatched antidote's POT still applies | pre-effect | none (0) | **sourced** — Ch 7 p.176 |
| `disease-affected-characteristic` | Which characteristic a disease drains | pre-drain | CON | **sourced** — Ch 7 p.170 |

The decision *ports* are sourced to the passages that leave the call open; the *default answers* are
a **house choice** of the most neutral reading (documented on `DefaultInjuryAdjudicator`). Tests drive
every port with a deterministic stub.

## Out of scope (per `orc-scope-filter.md` and the issue)

Not implemented here: the situational-modifier spot rules (ADR 0018); radiation, fire/heat, prone,
and stake/trap damage; **hit-location** falling damage (Ch 7 p.171: "a fall does damage to 1D4 hit
locations" — the entire hit-location subsystem is deferred, so the split is **named as discretion**
and not built); the fumble tables (#97); a **fatigue-point** subsystem (the minor-disease 1D6 fatigue
is rolled and reported but not applied — no fatigue system exists yet); "some diseases may combine the
effects" (p.170) beyond selecting one affected characteristic; and any fantastical content.

## Consequences

- `Brp.Rules.Combat` gains `InjuryRuleset` (+ `FallingRuleset`/`PoisonRuleset`/`DiseaseRuleset`),
  `FallingResolver`, `PoisonResolver`, `DiseaseResolver`, the `IllnessSeverityTable`/
  `IllnessSeverityBand` lookup, and the `IllnessDegree`/`IllnessLossPeriod`/`PoisonOnsetUnit` enums
  and outcome records. `Brp.Data` gains `injury-ruleset.json` and `NoirInjuryRuleset`.
  `Brp.Core.Contests` gains `IInjuryAdjudicator`, `InjuryDecisionId`/`InjuryDecisionIds`,
  `DefaultInjuryAdjudicator`, and the ruling types.
- `DamageResolver` gains a public plain-int `ApplyDamage` overload — the non-weapon damage entry
  point falling and poison share, reused rather than reimplemented.
- The onset delays, the poison hit-location variant, the fatigue subsystem, disease combined-effects,
  and the falling hit-location split all need caller/round/time state these resolvers do not hold. As
  in ADR 0018, the resolvers compute and apply the harm and name the open calls; whichever piece
  orchestrates a running encounter or campaign clock wires the ports and the timing.
