# 0024. Hit Locations: type, D20 location table, per-location hit points, and armor-by-location

## Status

Accepted — 2026-09-01. Resolves #112 (decided ON in #4). Formalizes the `HitLocation` type
`ArmorDefinition`'s doc comment (ADR 0013) flagged as not yet existing. Builds on the Layer 1
hit-point figure (`AbilitySet.MaximumHitPoints`, ADR 0008), the dice/entropy seam (ADR 0003), and
the armor definition schema (ADR 0013). Deliberately does **not** touch `DamageResolver` or
`MajorWoundResolver` (ADR 0017/0021) — see "Reconciling with Major Wounds" below.

## Context

The optional hit-location system spans two chapters, verified independently with
`pdftotext -f/-l -layout` against the pinned PDF (AGENTS.md invariant 1/2):

- Ch 2: Characters, "Hit Points by Hit Location (Option)" — the per-location fraction formula and
  its own printed lookup table for totals 1-21 (p.14).
- Ch 6: Combat, "Melee Hit Location Table (Option)" and the "Hit Locations" D20 table (p.145); and
  "Damage and hit Locations (Option)" — routing damage to a struck location and the total pool, the
  limb damage cap, and the three damage-band thresholds (pp.156-157).

Ch 8: Equipment, "Armor by Hit Location (Option)" and "Layering Armor" (both p.209), and the armor
tables' "Fits Locations" column (pp.207-208, all four armor tables: Primitive, Ancient and Medieval,
Modern, Advanced) give how armor value applies per location and how overlapping pieces combine. Ch 7:
Spot Rules, "Falling" (p.172) gives the one printed exception to the limb damage cap.

Only these passages and the already-shipped `ArmorDefinition`/`AbilitySet` were consulted. Nothing
here derives from `engine-implementation-plan.md` (AGENTS.md invariant 2).

**Revision note (post-acceptance).** Independent conformance review (rules-conformance +
Codex-conformance) found four defects in the first version of this record and its implementation:
an armor-value citation that was fabricated (attributed "using the heaviest if these differ" to
armor *value*, when that clause governs only burden and skill modifier — armor value totals, per a
different p.209 passage, "Layering Armor"), an armor-coverage vocabulary gap ("All"/"All but head"
threw instead of resolving), a chapter mislabel (the per-location hit-point table is Ch 2, not
Ch 6), and an unsound justification for the D20 table correction (below). All four are fixed in this
revision; see each subsection.

## Decision

### `HitLocation` — sourced

A seven-value enum (`RightLeg`, `LeftLeg`, `Abdomen`, `Chest`, `RightArm`, `LeftArm`, `Head`), per
Ch 6, p.145. Nonhuman hit-location tables (Ch 11: Creatures) are out of scope — the noir setting is
human-only.

### The D20 hit-location table — sourced, reproduced row-by-row, one printed misprint corrected

`HitLocationTable`/`HitLocationTableRow` (mirroring `MajorWoundTable`/`MajorWoundRow`), loaded from
`hit-location-ruleset.json` by `NoirHitLocationRuleset.Load()`. Reproduced row-by-row, all 20 D20
faces, in `NoirHitLocationRulesetTests`.

| D20 | Location | Description |
|---|---|---|
| 1–4 | Right Leg | Right leg from hip to bottom of foot |
| 5–8 | Left Leg | Left leg from hip to bottom of foot |
| 9–11 | Abdomen | Hip joint to bottom rib cage |
| 12 | Chest | Ribcage up to neck and shoulders |
| 13–15 | Right Arm | Entire right arm |
| 16–18 | Left Arm | Entire left arm |
| 19–20 | Head | Neck and Head |

**Printed misprint, corrected.** The book prints the Abdomen row as **8–11**, one value overlapping
the Left Leg row immediately above it (printed **5–8**) — the digit 8 is claimed by both rows.
Confirmed via the PDF's glyph bounding boxes (`pdftotext -bbox`, per `docs/source-handling.md`'s
escalation recipe) to be a real printed character, not a whitespace-extraction artifact: the source
literally prints "8–11".

The engine implements **9–11**, not the printed 8–11. The first version of this record justified
that correction by an arithmetic argument ("the only partition consistent with 20 faces") that does
not actually hold: **Left Leg 5–7 plus Abdomen 8–11 also sums to 20 rolls** and is equally
"consistent" by that test alone — face-counting the six undisputed rows only proves *some* row must
absorb the overlap, not *which side* of it does. The correction stands on different, sounder
grounds instead: the Left Leg row's own printed range, **5–8**, is clean and unambiguous on its own
— nothing about it is malformed or disputed; the printed table is otherwise symmetric between the
two legs (Right Leg 1–4 and Left Leg 5–8 are both exactly 4 faces); and the canonical BRP humanoid
hit-location table (the same D20 table this book's own Ch 6 Hit Locations table descends from) gives
Abdomen as **09–11**. Given a clean, undisputed Left Leg row and the canonical table's Abdomen range
agreeing on 9–11, that is the correction this engine implements, not a face-count coincidence.
`NoirHitLocationRulesetTests` pins both facts: the corrected table has no overlap, and a dedicated
test documents what the book actually prints at the disputed cell.

### Per-location hit points — sourced formula, one printed table cell logged as inconsistent

`HitPointsByLocationCalculator.Compute` implements Ch 2: Characters, p.14's formula directly — "Leg, Abdomen,
Head: 1/3 total hit points. Chest: 4/10 total hit points. Arm: 1/4 total hit points," each rounded
up via the existing `Rounding.Divide(..., RoundingMode.Up)` (ADR 0008's convention) — rather than a
band table, so it produces a value for any total, not just the printed 1-21 range.

The book also prints a lookup table of this formula's own results for totals 1-21 ("provided below
based on Maximum Hit Points"). `HitPointsByLocationCalculatorTests` reproduces all **45** printed
cells (15 totals × 3 distinct formulas: Leg/Abdomen/Head share one, Chest and Arm each their own).
**44 of 45 match exactly.** The one exception: the printed "16–17" Arm column gives a single value,
4, for both totals — correct for 16 (⌈16/4⌉ = 4) but not 17 (⌈17/4⌉ = 5).

Unlike the D20 table misprint above, here the *formula* is what the prose instructs the reader to
"use," and the table is explicitly introduced as *derived from* it ("provided below based on...").
This inverts the usual "table beats prose" default (`docs/source-handling.md`): the table is the
subordinate artifact here, not an independently authored source competing with the formula. Combined
with 44 of 45 cells across three different fractions agreeing with the closed form, this is treated
as a table transcription error, not a formula error. The engine implements the formula; the one
disagreeing cell is pinned in
`HitPointsByLocationCalculatorTests.Printed_table_disagrees_with_its_own_formula_at_exactly_one_cell`,
naming both the printed value and the value the engine actually returns, per the
`ResistanceTableTests` precedent (`docs/source-handling.md`, "Known errata in the book").

### Armor by hit location — sourced

`ArmorCoverage` (in `Brp.Rules.Gear`, alongside `ArmorDefinition`) resolves the armor tables'
coverage categories against the seven granular `HitLocation` values. **The printed "Fits Locations"
column itself uses only five literal labels** — "Head", "Chest", "Arms", "All", and "All but head"
— verified cell by cell against all four armor tables (Ch 8, pp.207-208: Primitive, Ancient and
Medieval, Modern, Advanced) with `pdftotext -layout`; the column never prints a standalone
"Abdomen" or "Legs" cell. "Arms" covers both `LeftArm` and `RightArm`; "All" covers every location;
"All but head" covers every location except `Head`; "Head"/"Chest" map 1:1.

`ArmorCoverage` additionally accepts **"Abdomen" and "Legs" as a data-authoring convenience, not a
second printed vocabulary**: `armor-ruleset.json` sometimes expands a printed "All" into its five
constituent locations by name instead of leaving it as the single string "All" (e.g. Riot Gear's
`HitLocations` lists `["Head", "Arms", "Chest", "Abdomen", "Legs"]` rather than `["All"]`) — a
transcription choice ADR 0013 made, not a claim that the book prints those two words in this
column. `ArmorCoverage` supports both authoring styles so either resolves identically. (An earlier
version of this record and `ArmorCoverage`'s doc comment listed "Abdomen"/"Legs" alongside the five
genuinely printed labels without distinguishing them; corrected here.)

The in-scope modern subset (ADR 0013) uses "Chest" and "All" (e.g. "Clothing, Heavy" = All); "All
but head" is printed only on out-of-scope historical armors (Lamellar, Plate, Ring, Scale) but is
handled so the vocabulary itself never throws. `ArmorDefinition` itself is unchanged — its
`HitLocations` field is exactly what ADR 0013 shipped, now made operative rather than inert plain
strings.

**Armor value at a location totals across overlapping covering pieces — sourced, corrected from an
earlier fabricated citation.** `ArmorValueAt(location, isFirearm, wornArmor)` sums the given attack
type's value from every worn piece that covers the location, per Ch 8, "Layering Armor" (p.209):
soft armor worn with other armor "add[s] their usual armor value," and overlapping anything else
"total[s] the armor value" (at the cost of tripling the lesser piece's ENC, which is not modeled
here — only the armor-value total is). Zero if nothing worn covers the location.

An earlier version of this record and `ArmorValueAt`'s doc comment instead took the *maximum* of
overlapping pieces, citing p.209's "Armor by Hit Location (Option)" text, "using the heaviest if
these differ," as though it governed armor value. That citation was fabricated: the actual
sentence — "The burden is that of the pieces worn on the chest, abdomen, or legs, using the
heaviest if these differ" — governs **burden**, and the very next bullet governs **skill
modifier**; neither bullet mentions armor value, which the section's first bullet instead says to
take "from the armor charts" without any aggregation rule of its own. The aggregation rule for
armor *value* specifically is "Layering Armor," a different, adjacent passage on the same page,
which this revision now cites instead. ENC/burden/skill-modifier layering (the "heaviest" rule, and
ENC tripling for hard-over-soft) is out of scope for this issue and is not implemented; only the
armor-value total is.

### Damage routed to the struck location and the total pool, with the limb cap — sourced

`HitLocationHitPoints` tracks each location's cumulative recorded damage against its
`HitPointsByLocationCalculator`-derived maximum, alongside (not instead of) the character's existing
single-pool `AbilitySet.CurrentHitPoints` (Ch 6, p.156: "Keep track of each wound and each location
separately, but also keep a running total"). `HitLocationDamageResolver.ApplyDamage`:

1. Subtracts `armorValue` from the incoming (pre-armor) damage, per `ArmorCoverage.ArmorValueAt`.
2. For a limb (`RightArm`/`LeftArm`/`RightLeg`/`LeftLeg`) hit, caps the amount actually applied to
   both the location and the total pool at `LimbDamageCapMultiplier` (2, data) times the location's
   hit points (Ch 6, p.157: "cannot take more than twice the possible points of damage in an arm or
   leg from a single blow... the remaining damage has no effect"). A triple-or-more hit is capped
   identically to a double hit — the excess above 2× is discarded outright, not partially applied.
3. Head/Chest/Abdomen are never capped — the book's cap language names only "an arm or leg."
4. Classifies the blow's raw (pre-cap) damage against the printed thresholds
   (`HitLocationDamageBand`: `Unaffected`/`EqualOrExceedsLocationHitPoints`/`...Double...`/`...Triple...`),
   letting a caller apply the book's own per-location narrative text (a leg falling prone, an arm
   dropping what it held, bleeding rates, Stamina rolls, instant death for a tripled head/chest/
   abdomen hit) without this resolver hardcoding a copy of that prose.

   **A boundary distinction the band's doc comment calls out explicitly, for a future caller:** the
   `EqualOrExceedsDoubleLocationHitPoints` band fires at **≥2×**, matching the printed section
   heading verbatim ("Damage Equals or Exceeds Double the Location's Hit Points," pp.156-157). But
   that section's *head/chest/abdomen* consequence (unconsciousness and bleeding) is worded in the
   body text as **"more than twice"** (Ch 6, p.157) — i.e. **>2×**, not ≥2× like the limb
   consequence in the same section ("cannot take more than twice the possible points of damage...
   from a single blow"), which is itself worded as a cap at exactly twice, not a strict ">2×"
   trigger. A caller wiring the head/chest/abdomen debilitating effect must
   not fire it at exactly 2×; the band only tells the caller which printed section applies, not
   which strict/non-strict comparison that section's specific consequence uses. No behavioral
   change follows from this in the resolver itself — `HitLocationDamageBand`'s doc comment now
   states both readings so a future caller does not conflate them.

### Design contract: `HitLocationDamageResolver` is a stateless, single-blow classifier — house decision, deliberate

**House decision**, made explicitly by the project owner during conformance review: `ApplyDamage` is
deliberately stateless with respect to a location's damage *history*. It classifies exactly the one
damage amount it is given for the current blow against a location's hit points; it does not read
`HitLocationHitPoints`'s already-recorded damage from earlier blows back in to decide the current
blow's `HitLocationDamageBand`, and it does not itself apply any of the per-band effects. This is
**in scope as designed, not a gap the Issue left open**:

- **Accumulating damage across multiple blows to the same location** — so that, for instance, two
  lesser hits to an arm eventually reach the printed "disabled" threshold together, not just a
  single blow reaching it alone — is the **caller's** responsibility. `HitLocationHitPoints` already
  tracks the running total needed for this (`DamageTakenAt`/`RemainingAt`, updated by every
  `ApplyDamage` call); a caller wanting a cumulative "disabled" rule compares that running total
  against zero (or the location's maximum) itself, on its own schedule, rather than relying on this
  resolver's per-call `Band` to have folded prior blows in.
- **Applying the effects a `HitLocationDamageBand` implies** (a leg falling prone, an arm dropping
  what it held, a limb being severed, unconsciousness, instant death) is deferred to the caller —
  already recorded as out of scope above, and restated here because it is the same design choice,
  not a separate one: a stateless classifier that reports a band is exactly the right shape for a
  resolver that does not also own turn-economy, narrative state, or Stamina rolls.

This mirrors, rather than deviates from, the Issue's existing deferral pattern: `MajorWoundResolver`
(ADR 0021) similarly reports structured outcomes for a caller's encounter loop to apply, and
`HitLocationDamageBand`'s own remarks already stated that its narrative consequences are a caller
concern. Recording it here makes the *classifier's statelessness itself* — not just "effects are
deferred" — an explicit, citable contract for future callers and reviewers, so a later change that
tries to make `ApplyDamage` "smarter" by reading prior damage internally is a deliberate redesign
requiring its own review, not an unnoticed behavior drift.

### The falling exception — sourced

`ApplyDamage` takes a `bypassLimbCap` flag. Ch 7: Spot Rules, "Falling" (p.172): "The entire damage
done by the fall applies both to the rolled hit location and to the falling character's total hit
points. This is an exception to the rule that a limb may take only twice its hit points in damage."
Passing `bypassLimbCap: true` for a fall's hit-location roll applies the raw damage uncapped; this
issue does not otherwise wire up the falling-damage location roll itself (Ch 7, p.172: "a fall does
damage to 1D4 hit locations") — `FallingResolver` (#96, ADR 0019) predates hit locations and is
untouched; a future issue can have it call `HitLocationResolver`/`HitLocationDamageResolver` with
this flag when hit locations are the active damage model.

### Reconciling with Major Wounds — sourced constraint, kept as two parallel paths

The book states plainly (Ch 6, p.156) that hit locations and Major Wounds "should not be used
together," and ADR 0021 recorded the same constraint against this issue in advance. This issue
resolves it by keeping the two **fully separate, parallel** resolvers: `HitLocationDamageResolver`
never calls `MajorWoundResolver`, and neither `DamageResolver` nor `MajorWoundResolver` is modified.
A caller picks one damage model for a given game/character and uses only that resolver's apply path;
nothing in the shared `AbilitySet`/`WoundTrack` state forces a mix. This is the same "reconcile by
not layering" resolution the constraint anticipated, not a new house rule.

## Out of scope (per the issue and `orc-scope-filter.md`)

- The per-location narrative effects each `HitLocationDamageBand` implies (falling prone, dropping
  a weapon, bleeding rates, the Stamina rolls to avoid unconsciousness/death, severed-limb
  bookkeeping) — Ch 6, pp.156-157's prose beyond the numeric bands. A caller's turn-economy layer
  interprets the band, the same deferral ADR 0021 made for the Major Wounds Table's flavor text.
- Wiring `FallingResolver` (#96) to actually roll 1D4 hit locations for fall damage — only the cap
  bypass this issue's cap logic needs is added (`bypassLimbCap`); #96's resolver is untouched.
- Nonhuman hit-location tables (Ch 11: Creatures) — out of scope per the human-only noir setting.
- Helmet-specific armor values (Ch 8, p.209's helmet table) — the shipped armor subset (ADR 0013)
  already folds helmet coverage into Riot Gear's "Head" location rather than a separate item.
- Fire/hit-location interaction (Ch 7, p.172's "Hit locations may determine where fire affects a
  character") — no shipped mechanic yet routes fire damage through hit locations at all.

## Consequences

- `Brp.Rules.Combat` gains `HitLocation`, `HitLocationTableRow`, `HitLocationTable`,
  `HitLocationRoll`, `HitLocationResolver`, `HitPointsByLocation`, `HitLocationRuleset`,
  `HitPointsByLocationCalculator`, `HitLocationHitPoints`, `HitLocationDamageBand`,
  `HitLocationDamageResult`, `HitLocationDamageResolver`.
- `Brp.Rules.Gear` gains `ArmorCoverage`; `ArmorDefinition`'s doc comment is updated to point at it
  (no change to its shape or shipped data).
- `Brp.Data` gains `hit-location-ruleset.json` and `NoirHitLocationRuleset`.
- `DamageResolver`, `MajorWoundResolver`, `FallingResolver`, and `ArmorDefinition`'s data are
  unchanged.
