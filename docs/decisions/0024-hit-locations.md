# 0024. Hit Locations: type, D20 location table, per-location hit points, and armor-by-location

## Status

Accepted — 2026-09-01. Resolves #112 (decided ON in #4). Formalizes the `HitLocation` type
`ArmorDefinition`'s doc comment (ADR 0013) flagged as not yet existing. Builds on the Layer 1
hit-point figure (`AbilitySet.MaximumHitPoints`, ADR 0008), the dice/entropy seam (ADR 0003), and
the armor definition schema (ADR 0013). Deliberately does **not** touch `DamageResolver` or
`MajorWoundResolver` (ADR 0017/0021) — see "Reconciling with Major Wounds" below.

## Context

Ch 6: Combat gives the optional hit-location system across three passages, verified independently
with `pdftotext -f/-l` against the pinned PDF (AGENTS.md invariant 1/2):

- "Melee Hit Location Table (Option)" and the "Hit Locations" D20 table (p.145).
- "Hit Points by Hit Location (Option)" — the per-location fraction formula and its own printed
  lookup table for totals 1-21 (p.14).
- "Damage and hit Locations (Option)" — routing damage to a struck location and the total pool, the
  limb damage cap, and the three damage-band thresholds (pp.156-157).

Ch 8: Equipment, "Armor by Hit Location (Option)" (p.209) and the Modern Armor table's "Fits
Locations" column (p.207) give how armor applies per location. Ch 7: Spot Rules, "Falling" (p.172)
gives the one printed exception to the limb damage cap.

Only these passages and the already-shipped `ArmorDefinition`/`AbilitySet` were consulted. Nothing
here derives from `engine-implementation-plan.md` (AGENTS.md invariant 2).

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
literally prints "8–11". A D20 table must partition 1–20 exactly once each; the other six rows
(1-4, 12, 13-15, 16-18, 19-20) already claim 4+1+3+3+2 = 13 rolls, plus the undisputed part of
Left Leg (5-8, 4 rolls) = 17, leaving exactly 3 rolls for Abdomen. The only three-wide range
starting after 8 is **9–11**, which this engine implements. `NoirHitLocationRulesetTests` pins both
facts: the corrected table has no overlap, and a dedicated test documents what the book actually
prints at that cell.

### Per-location hit points — sourced formula, one printed table cell logged as inconsistent

`HitPointsByLocationCalculator.Compute` implements Ch 6, p.14's formula directly — "Leg, Abdomen,
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

`ArmorCoverage` (in `Brp.Rules.Gear`, alongside `ArmorDefinition`) resolves the printed armor
table's coarser coverage categories ("Head", "Chest", "Abdomen", "Arms", "Legs" — already stored as
plain strings on `ArmorDefinition.HitLocations`, ADR 0013) against the seven granular `HitLocation`
values: "Arms" covers both `LeftArm` and `RightArm`; "Legs" covers both legs; the other three map
1:1. `ArmorValueAt(location, isFirearm, wornArmor)` returns the highest covering piece's value for
the given attack type (Ch 8, p.209: "using the heaviest if these differ"), or zero if nothing worn
covers the location. `ArmorDefinition` itself is unchanged — its `HitLocations` field is exactly
what ADR 0013 shipped, now made operative rather than inert plain strings.

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
