# 0020. Combat fumble tables: the four D100 consequence tables, a table-selecting resolver, and the ally/reroll seams

## Status

Accepted — 2026-08-31. Resolves #97 (Layer 4, the combat fumble tables). Completes the Ch 6 combat
surface begun by the attack/defense matrix (ADR 0016): the matrix flags *that* a fumble forces a
roll on the relevant table (`AttackDefenseOutcome.AttackerRollsOnFumbleTable` /
`DefenderRollsOnFumbleTable`), and this record delivers the tables that roll consults — piece F.

## Context

On a fumble (`SuccessLevel.Fumble`, decided by the resolution kernel #10 — **reused, not touched**,
along with its pinned errata test), Ch 6: Combat directs a D100 roll on "the relevant fumble table"
(p.146). The book prints **four** such tables, not one:

- **Melee Weapon Attack Fumbles** (p.148) — 12 rows
- **Melee Weapon Parry Fumbles** (p.148) — 10 rows
- **Missile Weapon Attack Fumbles** (p.148) — 12 rows
- **Natural Weapon Attack and Parry Fumbles** (p.149) — 11 rows

The #97 issue text guessed "the combat fumble table" (singular) and mislocated it in "Ch 7 / Ch 5";
the source extract handed to implementation had the right rows but the wrong pages (pp.147-148). The
**book is authoritative** (AGENTS.md invariant 1): all four tables were verified against the PDF and
sit on **pp.148-149** (printed p.147 holds the Attack/Defense Matrix). This is the recurring
**prose/issue-over-table** and **misattributed-citation** defect class named in
`docs/source-handling.md` — the printed tables win, and the page must match the print.

## Decision

### The four tables are data, reproduced row-by-row — sourced

All four tables live in `Brp.Data/fumble-ruleset.json` (loaded by `NoirFumbleRuleset` into the
immutable `FumbleRuleset`), per AGENTS.md invariant 7 — no row content hardcoded in C#. Each table is
a banded D100 lookup (`FumbleConsequenceTable` / `FumbleConsequenceRow`) mirroring the
`DamageModifierTable`/`IllnessSeverityTable` band structure. A percentile roll of **00 reads as 100**
(the engine-wide `IEntropySource.NextD100` convention), so the "00" row is stored with bounds of 100.

Every row is reproduced cell-by-cell in `NoirFumbleRulesetTests` — a `[Theory]` over all 45 rows plus
an exact-count `[Fact]` per table (12 / 10 / 12 / 11). The `FumbleConsequenceTable` constructor
additionally pins that the rows **tile [1,100] with no gap or overlap** and that every "use result
NN-NN" fallback names a real row on the same table; a transcription slip fails loudly rather than
mapping a roll to the wrong row.

Each printed effect is carried as (a) a `FumbleEffectKind` a caller dispatches on, (b) the exact
printed effect text for citation, and (c) structured quantities — an unrolled `DiceExpression`
`Amount` (rounds lost, meters thrown/scattered, weapon hit points), a flat `Magnitude` (the −30%
vision penalty, the −1 MOV twist, the 1 HP strain), a `LandedGrade` `HitGrade` for graded hits, and a
`Fallback`. **No effect is applied and no embedded die is rolled here** — dropping/throwing a weapon,
weapon hit-point loss, −MOV, hitting an ally or oneself are all structured outcomes returned to the
caller (the same caller seam as #50/#96; no encounter model lives in this layer).

### Selecting the table reuses the existing combat context — sourced routing

`FumbleResolver.SelectTable(WeaponClass, DefenseType)` maps the existing combat vocabulary onto the
four tables rather than inventing a parallel context enum:

- **`WeaponClass.Brawl`** (unarmed/natural) → **Natural** table (which combines attack and parry, so
  the defense is irrelevant).
- **Missile classes** (`Missile`, `Pistol`, `Revolver`, `Rifle`, `Shotgun`, `SubmachineGun`) →
  **MissileAttack**. A missile weapon has no parry table, so `DefenseType.Parry` is rejected.
- **Melee classes** (`Club`, `Dagger`) → `DefenseType.Parry` → **MeleeParry**, otherwise
  **MeleeAttack**. `DefenseType.None` reads as "the fumbled roll was an attack."

`DefenseType.Dodge` is rejected: the book prints no dodge fumble table. `FumbleTable` (the four-value
key the JSON files each table under) is the tables' *identity*, not a redundant re-encoding of the
context — the routing above is derivation logic, not book numbers.

### The reroll chain — sourced

The "**Blow it**" (99, roll twice more) and "**Blow it badly**" (00, roll three times more) rows are
followed **cumulatively**: the resolver tracks an outstanding-rolls count, and a reroll that itself
lands on 99/00 adds further rolls ("cumulative if rolled again"). Every roll — initial and reroll —
consumes one injected D100 draw; the resolver is otherwise pure, so the same seed reproduces the
identical step chain (AGENTS.md invariant 5). Each roll is recorded as a `FumbleStep` (the `Reroll`
markers included, as the audit trail of why more rolls followed); a caller applies the non-reroll
steps.

### The ally / fallback branch — sourced rows, named discretion port

The "hit nearest ally for normal/special/critical damage, **or use result NN-NN if no ally nearby**"
rows branch on a fact this layer does not hold. Following the `ISpotRuleAdjudicator` (ADR 0018) and
`IInjuryAdjudicator` (ADR 0019) precedent, "is an ally nearby" becomes the first-class port
`IFumbleAdjudicator.IsAllyInRange()` (in `Brp.Core.Contests`, returning a plain `bool` so no
`Brp.Rules` dependency inverts the layers — invariant 6), with the `FumbleDecisionId.AllyInRange`
id (`fumble-ally-in-range`) and a `DefaultFumbleAdjudicator`. The resolver reports **both** branches
in the returned `FumbleBranchSelection` (the ally-hit grade and the resolved fallback row) so a caller
sees the whole rule, and marks which applies from the adjudicator's answer. Resolving a fallback
consumes **no** entropy — "use result 41-50" is a lookup of that row, not a reroll. Tests drive the
port with a deterministic stub.

| Decision id | What the book leaves open | Timing | Default | Source |
|---|---|---|---|---|
| `fumble-ally-in-range` | Whether a friendly target is within reach of the fumbled blow | pre-effect | no ally in range (fallback used) | **sourced** — Ch 6 pp.148-149 |

The port is **sourced** to the rows that leave the call open; the default answer (no ally in range) is
a **house choice** of the most neutral reading — it invents no bystander the caller has not placed on
the field — documented on `DefaultFumbleAdjudicator`.

### The four tables, transcribed

| Table | Rows | Page |
|---|---|---|
| Melee Weapon Attack Fumbles | 12 | Ch 6, p.148 — **sourced**, row-by-row |
| Melee Weapon Parry Fumbles | 10 | Ch 6, p.148 — **sourced**, row-by-row |
| Missile Weapon Attack Fumbles | 12 | Ch 6, p.148 — **sourced**, row-by-row |
| Natural Weapon Attack and Parry Fumbles | 11 | Ch 6, p.149 — **sourced**, row-by-row |

Details worth recording:

- **The missile "weapon has no hit points" fallback is caller-decided, not adjudicated — house
  reading.** The missile row 66-80 ("do 1D6 damage to weapon's hit points, or use 81-85 if the weapon
  has no hit points") is a fallback of the same shape as the ally rows, but weapon hit points are not
  modeled in this layer. Rather than invent a second adjudicator port for a fact no current weapon
  data carries, the resolver reports both branches with `PrimaryApplies = null` (caller decides). If a
  weapon hit-point subsystem later lands, this can become a named port then.
- **`LandedGrade` is reused for the ally/foe/self hit grade — sourced vocabulary.** "Hit ... for
  normal/special/critical damage," "foe automatically hits with normal/special/critical hit," and
  "do normal damage to self" all use the same Normal/Special/Critical grades the attack/defense
  matrix already produces (ADR 0016), so the row carries a `LandedGrade` rather than a new enum.

## Out of scope (per `orc-scope-filter.md` and the issue)

Not implemented here: **weapon malfunctions** (e.g. a musket's 95-00 misfire — a weapon-data concern
that can co-occur with a fumble but is separate); the fumble **range** (which rolls fumble — kernel
#10, reused via `SuccessLevel.Fumble`, untouched); **applying** any ally/self damage, −MOV, weapon
hit-point loss, or lost-round turn-economy effect (all returned as structured outcomes for a
caller); **hit-location** damage (the "in the attacking limb if hit locations used" clauses — hit
locations remain a deferred, out-of-scope subsystem, so the resolver reports the grade to total hit
points as the book also does without hit locations); and any fantastical results.

## Consequences

- `Brp.Rules.Combat` gains `FumbleRuleset` (+ `FumbleConsequenceTable`/`FumbleConsequenceRow`/
  `FumbleFallback`), `FumbleResolver`, the `FumbleResolution`/`FumbleStep`/`FumbleBranchSelection`
  outcome records, and the `FumbleTable`/`FumbleEffectKind`/`FumbleFallbackCondition` enums.
  `Brp.Data` gains `fumble-ruleset.json` and `NoirFumbleRuleset`. `Brp.Core.Contests` gains
  `IFumbleAdjudicator`, `FumbleDecisionId`/`FumbleDecisionIds`, and `DefaultFumbleAdjudicator`.
- The applied effects (dropped/thrown weapons, weapon hit points, −MOV and lost rounds, ally/self
  damage, hit locations) all need caller/round/encounter state this resolver does not hold. As in
  ADR 0018/0019, the resolver names and structures the consequences; whichever piece orchestrates a
  running encounter wires the outcomes and the `fumble-ally-in-range` port into a live fight.
- With #96 (injury spot rules) and #97 (fumble tables) done, Layer 4's combat surface is complete;
  Layer 5 (the noir game layer, epic #98) is next per `ROADMAP.md`.
