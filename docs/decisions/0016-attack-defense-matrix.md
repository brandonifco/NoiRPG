# 0016. Attack/defense matrix: data-driven cells, the undefended case, and the deferred -30%

## Status

Accepted — 2026-08-30. Resolves #49 (Layer 4 piece C). Builds on #47 (combat round, ADR 0015).
Feeds piece D (damage).

## Context

Piece B (#47) sequences who acts when in a combat round; nothing yet resolves what happens
when an attack meets a defense. Ch 6: Combat, "Attack and Defense Matrix" (p.147) is a printed
table cross-referencing the attacker's degree of success against the defender's parry-or-dodge
degree of success. `engine-implementation-plan.md` §3 calls this "the 7-row table as data, not
an `if` chain" (the plan's row count is itself imprecise — see "The matrix shape" below); its
formulas are not authoritative (AGENTS.md invariant 2), but the instruction to keep this a data
table, not a branch chain, is sound and is what this record follows.

Ch 6, pp.145-147 is the sole source consulted for the matrix cells; Ch 5 supplies the five
success grades (`Brp.Core.Resolution.SuccessLevel`) that index it.

## The matrix shape — sourced, 17 cells not 7 rows

The printed table (p.147) has three grade-headed columns (Attack Roll, Parry Roll, Dodge Roll)
and one Result column. Reading it as (attacker grade × defender grade) pairs — since the Parry
and Dodge columns always carry the identical grade per row and the result text says the result
applies "if parried" or "if dodged" for the parry-specific footnotes only — the table resolves
to:

- 5 defender-grade columns (Critical, Special, Success, Failure, Fumble) for each of 3 attacker
  grades that require a defender roll at all (Critical, Special, Success) = 15 cells.
- 1 cell each for attacker grades Failure and Fumble, whose defender column reads "—" ("No roll
  required") rather than naming a grade = 2 cells.

**17 cells total**, not the plan's "~7-row table." `attack-defense-matrix-ruleset.json`'s
`matrixCells` array carries exactly these 17 entries, each transcribed from its own printed row;
`NoirAttackDefenseMatrixRulesetTests.The_shipped_ruleset_has_exactly_the_seventeen_printed_cells`
pins the count, and `Every_printed_matrix_cell_matches_the_book` is a `[Theory]` reproducing all
17 rows individually — not a sample — per AGENTS.md's "the test suite must reproduce that table
exactly" convention.

## The outcome type — what piece D needs without re-reading the matrix

`AttackDefenseOutcome` carries:

- **`LandedGrade`** (`Miss` / `Normal` / `Special` / `Critical`) — the *effective* grade of hit,
  after the matrix's downgrade. E.g. an attacker Critical against a defender Special downgrades
  to `Normal` ("achieves a success"); an attacker Critical against a defender Fumble stays
  `Critical`. This is deliberately a distinct type from `SuccessLevel` (which has a `Fumble`
  member that makes no sense as something that "lands" on a defender) rather than reusing it.
- **`ArmorTreatment`** (`NotApplicable` / `Subtracted` / `Bypassed` / `DoesNotApply`) — kept as
  four values rather than collapsed to a boolean "ignore armor," because the printed text uses
  two distinct phrases for what might be the identical rule: "Defender's armor value is
  bypassed" (Critical attack vs. Failed defense) and "Defender's armor value does not apply"
  (Critical attack vs. Fumbled defense). Nothing in Ch 6 states these are synonyms or gives a
  reason for the different wording (the second cell also adds "Defender rolls on the appropriate
  fumble table," which the first does not — that may be the reason for the separate phrasing, or
  may be incidental). Rather than guess and merge them, both tokens are preserved verbatim
  ("Keep tokens close to the book," per this issue) so that if a future rules-conformance pass
  finds a distinction that matters to piece D, the data already carries it.
- **`ParryWeaponDamage`** (`DamagedParty` + `Points`, nullable) — present only on the footnoted
  cells (p.147, footnote `*`), and only when the defense actually used was `DefenseType.Parry`.
  `AttackDefenseResolver.Resolve` strips it to `null` for `DefenseType.Dodge`, since a dodge has
  no weapon to damage. Some cells damage the *defender's* weapon (the attack partially got
  through), others the *attacker's* weapon (the defender's parry beat the attack outright, e.g.
  Success attack vs. Critical defense) — `DamagedParty` distinguishes them; neither is a shield
  (see "Shields are cut" below).
- **`DefenderRollsOnFumbleTable`** / **`AttackerRollsOnFumbleTable`** — flags only; the fumble
  tables themselves are piece F.

`AttackDefenseResolver.Resolve(attackerGrade, defenseType, defenderGrade, ruleset)` is the sole
entry point. It is not an if/switch chain over grade names (invariant 7): it looks up a matching
`AttackDefenseMatrixCell` in `AttackDefenseMatrixRuleset.Cells` (or `UndefendedOutcomes` for the
no-defense case) and returns that cell's data-defined outcome verbatim (aside from the
Parry/Dodge weapon-damage strip, which is a defense-type branch, not a grade branch).

## Shields are cut — sourced, modelled as "defending weapon"

`orc-scope-filter.md` cuts the Shield skill from scope. The book's cell text says "parrying
weapon **or shield** takes N points of damage" (p.147) and its footnote `**` illustrates full
damage with a greatsword example — neither is engine content. `ParryWeaponDamage` and its
`DamagedParty` enum model only "defending weapon" / "attacking weapon"; no `Shield` type,
member, or string exists anywhere in `Brp.Rules.Combat`.
`AttackDefenseResolverTests.No_shield_concept_exists_anywhere_in_the_attack_defense_types` pins
this with a reflection scan over every public and non-public member and enum value in the
namespace, not a spot check of one or two names.

## The undefended case (`DefenseType.None`) — a reasoned inference, not directly printed

The matrix's 15 defended cells all assume the defender rolled a grade. The book never states
what happens when a defender takes no defensive action at all — it is silent on this specific
scenario in the Attack and Defense Matrix section. This piece needed to decide it explicitly
(the issue calls it out as an acceptance criterion), and the decision is:

**A defender who takes no action at all is treated identically to a defender who rolled a
defense and got a Failure.** Concretely, `AttackDefenseMatrixRuleset.UndefendedOutcomes` for
attacker grades Critical/Special/Success reuses the same landed grade and armor treatment as
that attacker grade's own vs-Failure matrix cell:

| Attacker grade | Undefended outcome | Same as matrix cell |
|---|---|---|
| Critical | Critical hit, armor bypassed | Critical vs. Failure |
| Special | Special hit, armor subtracted | Special vs. Failure |
| Success | Normal hit, armor subtracted | Success vs. Failure |

This is **flagged explicitly as a house interpretation**, not a printed rule (see AGENTS.md's
"Sourced or house rule" convention) — but it is not an arbitrary one. Two things corroborate it:

1. The book's own **Combat Summary table** (p.145) treats "no roll required" (attacker Failure,
   attacker Fumble) as producing the same class of result regardless of what the other side
   does — the matrix's own Failure/Fumble rows already model "no defender roll needed" as
   equivalent across all defense possibilities. Extending "no roll happened" (an unrolled
   defense) to read the same as "a roll happened and produced zero degrees of success" (a
   Failure) is the same reasoning applied to the other side of the interaction.
2. For the Critical case specifically, it is independently corroborated by Ch 6's own general
   rule for Critical Success (p.146): "Unless countered with a critical parry, a critical attack
   result always ignores armor, even if that armor is all-encompassing" — which matches
   "Bypassed" regardless of whether the defender rolled a Failure or nothing at all.

Attacker Failure and Fumble need no defender grade **regardless** of `defenseType` — this was
already true in the printed matrix (the "—" columns) and needed no new inference;
`AttackDefenseResolver.Resolve` checks for these grades before even looking at `defenseType`.

## The -30% successive-parry/dodge penalty — DEFERRED, applied outside this piece

Ch 6, "Parry" and "Dodge" (p.144): "Each successive parry attempt after the first is modified
by -30% to the skill rating, cumulative," and the identical rule for dodge. The book also notes
"[c]ertain attacks cannot be parried (e.g., from a vastly larger attacker or area/sweep
attacks)."

**Decision: this is a modifier on the *defense roll* that produces the defender's grade, not a
concern of the matrix lookup, and is deferred out of this piece.** `AttackDefenseResolver`
consumes an already-computed `SuccessLevel? defenderGrade` — it has no knowledge of how many
times this defender has already parried or dodged this round, and applies no -30% arithmetic
anywhere. The caller (a later piece, or the eventual Action-phase orchestrator) is responsible
for tracking successive-defense counts, applying the cumulative -30% to the relevant skill
rating before rolling, and for the "cannot be parried" ruling — all of which require state this
matrix resolver does not hold (how many defenses this combatant has already attempted this
round, the relative SIZ of attacker and defender, whether the attack is an area/sweep attack).

This mirrors the seam ADR 0015 already drew for piece C: "what triggers a combatant having more
than one action per round... is piece C's (or a later piece's) concern" — the same shape of
decision (arithmetic exists in the ruleset's *definition*, but the *triggering logic and running
count* live with the caller) is applied here to the -30% penalty. `Brp.Data`'s
`attack-defense-matrix-ruleset.json` records the -30% figure and the "cannot be parried" note
under `defensiveActionsDefinition` for provenance and under `deferred` for scope tracking, but
neither is read by `AttackDefenseResolver` or wired into any arithmetic in this piece.

**Also deferred, per `orc-scope-filter.md` and the issue** and likewise absent from this
resolver's code and data-consumption: "Attacks and Parries over 100%" (the over-100% splitting
rule) and "Dodging Missile Weapons" (the Difficult, first-missile-only dodge exception).

## Seam to piece D

Piece D (damage) calls `AttackDefenseResolver.Resolve` with the attacker's rolled grade, the
defense type used, and the defender's rolled grade (if any — already reflecting whatever -30%
penalties or "cannot be parried" ruling the caller applied), and reads the returned
`AttackDefenseOutcome` to know: whether any damage applies at all (`LandedGrade.Miss` means no),
which grade of hit to roll damage for, how armor applies, whether a weapon (and whose) takes
parry damage, and whether either side rolls on a fumble table. None of the matrix's 17 cells or
the undefended-outcome table are re-read by piece D.

## What this piece does not build

- **Damage numbers** — armor subtraction arithmetic, weapon damage dice, the special-success
  damage formula (`weaponMax + normalRoll + db`), unconscious/dead thresholds, knockout — piece
  D.
- **Wounds / First Aid** — piece E. **Spot rules** — piece F.
- **The fumble tables themselves** (Melee Weapon Attack/Parry, Missile Weapon Attack, Natural
  Weapon Attack and Parry) — piece F. This piece only sets the "rolls on fumble table" flags.
- **"Attacks and Parries over 100%"** and **"Dodging Missile Weapons"** — deferred, per above.
- **Shields** — cut, per above.
- **The -30% cumulative successive-defense penalty and "cannot be parried" ruling's triggering
  logic** — deferred to the caller, per above.

## Consequences

- If a later piece finds that the book's "bypassed" and "does not apply" armor phrasings do mean
  something functionally different (beyond the fumble-table difference already visible in their
  cells), `ArmorTreatment` already carries the distinction as separate enum members — no
  ruleset or resolver change would be needed, only piece D's handling of the two cases.
- The undefended-case mapping (reusing each attacker grade's vs-Failure cell) is a house
  decision. If a future close reading of the book, an errata, or a design-review note surfaces a
  different intended treatment for a defender who never rolled at all, revisit this record and
  `undefendedOutcomes` in the ruleset — not the matrix cells themselves, which remain a faithful
  transcription independent of this decision.
- Piece C (this piece) and whatever orchestrates the Action phase's turn resolution (a
  combination of #47's `CombatRound` and a later piece) share responsibility for the -30% seam:
  `CombatRoundRuleset`/`CombatRound` know *when* a combatant acts, this piece resolves *what
  happens* given already-rolled grades, and neither currently owns *how many defenses this
  combatant has already attempted this round* — that state needs a home in a future piece before
  the -30% rule can be implemented end to end.
