# 0023. Healing & recovery: First Aid per-wound healing, natural healing, Medicine, and the Conditions of Medical Care table

## Status

Accepted — 2026-08-31. Resolves #109 (Layer 4, healing & recovery). Builds on the damage/HP path
(ADR 0017), the wound damage amount and fatal-wound rescue (ADR 0021, #111), the Layer 1
derived-characteristic recompute (ADR 0008), and the skill kernel / modifier pipeline (ADR 0007).
Follows the named-adjudication-port precedent of ADR 0018 (#50), ADR 0019 (#96), and ADR 0021 (#111).

## Context

The engine could damage hit points, add wounds (`Wound.DamageAmount`, #111), and drain
characteristics (poison/disease, #96), but nothing healed. This record covers the book's recovery
surface: First Aid's per-wound healing, natural healing over game weeks, the Medicine skill's role,
and the Conditions of Medical Care table.

Every value below was verified against the printed book text with `pdftotext`, not the issue or
`engine-implementation-plan.md` (AGENTS.md invariant 2). The issue's original body was superseded by
its own correction comment on the healing amounts and rates; both were re-checked against the pages.
The First Aid Special/Critical amounts were the specific audit flag — confirmed against the Ch 3
First Aid skill description.

## Decision

### First Aid — per-wound healing — sourced

Ch 3: Skills, "First Aid" (p.39): "Base Chance: 30%." A successful roll heals **1D3** hit points "to
a single wound or injury"; **Special 2D3**; **Critical 3+1D3**. A **Fumble** deals "1 general hit
point of damage" and heals nothing; a **Failure** heals nothing and "no further First Aid attempts
may be made." System Notes: healing is capped at "the amount of hit points the injury inflicted," and
"only one attempt may be made per wound." `HealingResolver.ResolveFirstAid` resolves the roll through
the skill kernel, then on a healing grade rolls the grade's dice, caps at
`Wound.DamageAmount` (#111), restores hit points through `AbilitySet.SetCurrentHitPoints` (the same
path damage removes them by, which clamps at maximum so healing cannot over-heal), and removes the
healed points from the wound (fully healed → removed; partially → reduced). Entropy order: the d100
roll, then the grade's healing dice (none on Fumble/Failure).

- **Stacking bonuses — sourced effect, house rounding.** "may add 1/2 of their Medicine skill rating
  and 1/5 of their Science (Pharmacy) skill rating," and equipment "may add up to a +20% bonus"
  (p.39). These are supplied as `AdditiveModifier`s of kind **Permanent** — figured into the rating
  before any Difficult grade halves it (ADR 0007), because the book frames them as bonuses to the
  rating itself. The fractions **round down** (house choice — the book prints no rounding rule; the
  numerators/denominators and the +20% cap are all data). "Hazardous or unsanitary conditions may
  make rolls Difficult" (p.39) is supplied as a `DifficultyModifier.Difficult`.
- **"Once per wound" is a caller seam — sourced constraint, house boundary.** A single attempt heals
  up to the whole wound, so the cap is enforced within the call; not re-attempting a wound already
  First-Aided (or after a Failure/Fumble) is caller-tracked, reported as
  `FirstAidOutcome.BlocksFurtherAttempts`, the same caller-tracked pattern as #111's same-day totals.

### The fatal-wound rescue is reused, not duplicated — sourced

First Aid's "A character at 0 or negative hit points … can be restored to life if their hit point
total is brought to 1+" (p.39) **is** the #111 fatal-wound rescue window (Ch 6, "Fatal Wounds",
p.156). First Aid's only role is to raise hit points; `HealingResolver.ResolvesFatalWoundRescue`
forwards to `MajorWoundResolver.SurvivesFatalWound` (which itself reuses
`DamageResolver.ResolvesToDeath` for the death threshold and adds the rescue window). No second copy
of the death threshold or the window exists.

### Natural healing — flat 1D3/week, reported — sourced

Ch 6, "Healing Naturally" (p.157): "Your character will normally heal 1D3 hit points per game week."
A **flat** rate, **not** CON-tied (the issue's "tie to CON/Stamina" hint was wrong and is ignored per
the correction comment). `RollNaturalHealingRate` **reports** the rolled rate; wall-clock accrual over
weeks is a caller seam (a fresh roll each week — the same "resolver reports the rate, caller applies
it over time" seam as disease #96). `ApplyWeeklyHealing` removes accrued points from wounds "spreading
the healing between multiple wounds as evenly as possible" (p.157) — distributed one hit point at a
time, round-robin, across wounds with remaining damage — and caps the healing that lands at the total
outstanding wound damage.

### Medicine — doubled rate and characteristic restoration — sourced

Ch 3: Skills, "Medicine" (p.46) / Ch 6 (p.157): "Base Chance: 05%." A success "doubles the healing
rate from 1D3 to **2D3** hit points per week" (`RollMedicineHealingRate`, reported like natural
healing). A stabilized poisoned/diseased character "recovers **1D3-1** hit points or characteristic
points per week" (Success), **1D3** (Special), or **1D3+1** (Critical) — `RollCharacteristicRestorationRate`
**reports** the grade's rolled rate; `ApplyCharacteristicRestoration` raises the characteristic through
`AbilitySet.Set` so derived values recompute live (Ch 2, p.13; ADR 0008), capped at the ruleset
maximum. Major-wound characteristic loss recovers only "through training or various means" (p.156,
vague) — **not** modeled with an invented clean rate, per the issue's instruction to report rather
than invent.

- **Restoration does not resurrect clamped hit points — sourced.** Ch 2 (p.13): a reduced maximum
  clamps current hit points at mutation time, and `AbilitySet.Set` enforces that a later restoration
  cannot un-clamp them. So restoring CON raises maximum hit points without healing current ones — a
  tested behavior (`Applying_characteristic_restoration_recomputes_derived_values_via_ability_set`).

### Conditions of Medical Care table — sourced, reproduced row-by-row

Ch 6, "Conditions of Medical Care" (p.157) is data in `Brp.Data/healing-ruleset.json`, loaded into a
`ConditionsOfMedicalCareTable` / `MedicalCareRow` lookup keyed by `MedicalCareTier`, reproduced **row
by row** in tests (`NoirHealingRulesetTests`, `[Theory][MemberData]` + an exact-count `[Fact]` = 3
rows). Natural healing is 1D3/week on every printed row.

| Care tier | Conditions | Effect on healing rate |
|---|---|---|
| Poor | Poor/unsanitary/stressful; mobile & exerting; or no care | Caregiver must succeed a **Difficult** First Aid or Medicine roll for any healing. Success → 1D3/week; failure → none; **fumble → 1D3 additional damage**. |
| Decent | Decent, sanitary, restful, moderate exertion | Heals **1D3** naturally (no roll). |
| Excellent | Excellent conditions/equipment, full bedrest & therapy, full-time care | Heals **1D3** naturally; a further successful First Aid/Medicine use allows **possible additional healing**. |

`ResolveConditionsOfCare` drives the healing-rate modifier: the poor tier gates all healing behind a
Difficult caregiver roll (through the skill kernel) and inflicts 1D3 on a fumble; the excellent tier's
"possible additional healing" is reported as an `AllowsAdditionalHealing` flag — that further use is a
separate First Aid/Medicine call, and the book prints no additional amount, so none is invented.

### Gamemaster-discretion points become named adjudication ports — following ADR 0018/0019/0021

Two calls the healing rules hand to the gamemaster become first-class ports on a new
`IHealingAdjudicator` (in `Brp.Core.Contests`), with a `HealingDecisionId` enum, canonical kebab-case
ids (`HealingDecisionIds.CanonicalId`), and a `DefaultHealingAdjudicator`. Return types are
`Brp.Core` values (no `Brp.Rules` dependency; AGENTS.md invariant 6).

| Decision id | What the book leaves open | Timing | Default | Source |
|---|---|---|---|---|
| `healing-conditions-tier` | Which of the three Conditions of Medical Care tiers the patient's environment falls in | pre-healing | Decent | **sourced** — Ch 6 p.157 |
| `healing-caregiver` | Who provides care, and therefore whether they roll First Aid or Medicine ("a Difficult First Aid or Medicine roll") | pre-roll | First Aid | **sourced** — Ch 6 p.157 |

- **Sourced ports, house defaults.** Each decision *port* is sourced to the passage that leaves the
  call open; the *default answers* are a house choice of the most neutral reading (documented on
  `DefaultHealingAdjudicator`): the middle care tier, and the more broadly trained skill. Tests drive
  every port with a deterministic stub.

## Out of scope (per `orc-scope-filter.md` and the issue)

Powers / fantastical / futuristic-tech healing (the "make rolls Easy" tech tier is noted but not
modeled — no equipment tier exists here); surgery / long-term-care simulation beyond the printed
rules; hit-location-specific healing and the "stop bleeding to a hit location" First Aid effect
(#112); the fatal-wound rescue *mechanic* itself (#111 — First Aid only triggers it); and any
wall-clock accrual over game weeks (rates are reported for a clock-aware caller, not simulated). The
Medicine effect "halts ongoing poison/disease damage" is the #96 drain's stop condition and is not
re-modeled here.

## Consequences

- `Brp.Rules.Combat` gains `HealingResolver` (with `FirstAidOutcome`, `FirstAidSupport`,
  `WeeklyHealingRate`, `CharacteristicRestorationRate`, `WoundHealing`, `HealingApplication`,
  `ConditionsOfCareOutcome`), the `HealingRuleset` / `FirstAidRuleset` / `NaturalHealingRuleset` /
  `MedicineRuleset` data model, and the `ConditionsOfMedicalCareTable` / `MedicalCareRow` /
  `HealingRollDifficulty` table model. `Brp.Data` gains `healing-ruleset.json` and
  `NoirHealingRuleset`. `Brp.Core.Contests` gains `IHealingAdjudicator`,
  `HealingDecisionId`/`HealingDecisionIds`, `DefaultHealingAdjudicator`, `MedicalCareTier`,
  `CaregiverSkill`, and `CaregiverRuling`.
- First Aid heals through the existing `AbilitySet.SetCurrentHitPoints` and `Wound` paths; Medicine
  characteristic restoration through `AbilitySet.Set` (recompute); the fatal-wound rescue through the
  #111 `MajorWoundResolver.SurvivesFatalWound` seam. No new HP or death logic is duplicated.
- Wall-clock accrual, the caregiver's identity/skill, and the environment tier all need
  caller/GM/time state this resolver does not hold. As in ADR 0018/0019/0021, the resolver computes
  the recovery and names the open calls; whichever piece orchestrates campaign time wires the ports
  and the accrual.
