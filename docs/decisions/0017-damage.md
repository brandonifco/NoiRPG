# 0017. Damage: normal/special/critical arithmetic, hit-point conditions, and knockout attacks

## Status

Accepted — 2026-08-30. Resolves #52 (Layer 4 piece D). Consumes #49 (piece C,
`AttackDefenseOutcome`), #42 (gear, `WeaponDefinition`), and Layer 1/3 (`AbilitySet`,
`WoundTrack`). Feeds piece E (First Aid, Major Wounds) and the injury spot-rules issue.

## Context

Piece C (#49) says *whether and how well* an attack lands; nothing yet turned that into hit
points lost. This record adds `DamageResolver`: it rolls damage for a landed hit, applies it to
a target's hit points (tracking negative HP), records the blow as a wound for piece E, and
determines the resulting condition — unconscious, fatally wounded, or (for a declared knockout
attack) knocked out.

Ch 6: Combat, pp.146–156, and Ch 7: Spot Rules, "Knockout Attacks" (p.174), are the sole sources
consulted. `src/Brp.Data/damage-ruleset.json` was drafted by a prior extraction pass before this
issue's implementation began; per AGENTS.md ("the extractor confirmed them but you own
correctness"), every formula in it was re-verified against the printed text while building this
piece, and one was found and corrected — see "The special-success formula" below.

## Decision

### Normal and Special hits share identical damage arithmetic — corrected against the ruleset draft

`damage-ruleset.json`'s original `specialSuccessDamage.formula` read
`weaponMaxDieResult + normalDamageDiceRoll + damageBonus - armor` — the weapon's maximum result
plus a *second*, fresh roll of the same dice. This is not what the book says.

Ch 6, p.147, footnote `**` (the footnote the ruleset's own `printedFootnoteExplanation` field
already quoted, without drawing the conclusion its own prose demanded): "This is the damage
which that type of attack would normally do. This is not the same as 'maximum damage'. **For a
greatsword, full damage is 2D8 on a normal success, 2D8 bleeding damage on a special success**,
and on a critical success it does 16 damage ignoring armor." The greatsword's normal-success
dice (2D8) and special-success dice (2D8) are identical — the "bleeding" label names the
special-effect *type* (Ch 6, "Special Successes and Damage," pp.148–149: bleeding, crushing,
entangling, impaling, knockback — all out of scope, see below), not extra dice.

Ch 6, p.146's general "Special Success" entry independently confirms this: "An exceptional
roll... Often, a special attack means that **the weapon does normal damage** in addition to a
special result based on the weapon's type," with a worked example: "with Firearm 60%, your
character achieves a special success... **This does normal damage (1D8, for example)**, but in
the case of a firearm, also does impaling damage." No maximum-plus-normal-roll combination
appears anywhere in either passage. Only Critical uses the weapon's maximum (p.146: "the maximum
possible damage for the weapon used... plus the normal rolled damage modifier").

**Corrected formula, as implemented in `DamageResolver.RollDamage`:**

| Landed grade | Damage | Armor |
|---|---|---|
| Miss | none | n/a |
| Normal | weapon dice + db | subtracted |
| Special | weapon dice + db (same as Normal) | subtracted |
| Critical | weapon maximum + db | ignored |

`damage-ruleset.json` was updated in the same change to record the correction inline
(`specialSuccessDamage.correctionNote`) rather than silently overwritten, so the discrepancy and
its citation stay visible to future readers of the data file, not only this record.
`rules-conformance` and Codex's cross-check were both told, before this correction, to
falsify exactly this formula — this is the falsification they should confirm caught something
real, not a formality that passed by construction.

Db is rolled once, separately, and added to the raw (pre-floor) weapon-dice total before the
floor-at-zero and armor subtraction are applied — Ch 6, p.147 footnote: "Damage modifier, in all
cases, is rolled separately and added afterwards." `WeaponDefinition.ApplyDamageBonus` gates
whether it applies at all (firearms do not receive it).

### Armor treatment collapse

`ArmorTreatment.Bypassed` and `ArmorTreatment.DoesNotApply` both mean "ignore armor entirely" for
damage purposes, per ADR 0016's note that the book uses two distinct phrases for what may be the
same rule. `DamageResolver` treats both identically; a landed hit with `ArmorTreatment.Subtracted`
subtracts the applicable armor value (supplied by the caller — this resolver does not roll hit
locations, per #52's scope). `ArmorTreatment.NotApplicable` is valid only for a Miss; passing it
alongside any other landed grade throws, since the matrix never pairs them (ADR 0016's 17 cells).

### Hit-point conditions — data-driven thresholds, `AbilitySet.MajorWoundLevel` reused

- **Unconscious**: current HP at or below `DamageRuleset.UnconsciousHitPointLevel` (2), Ch 2,
  p.13: "Your character loses consciousness when their hit points are reduced to 2 or less."
- **Fatally wounded** (the *flag*, not resolved death): current HP at or below
  `DamageRuleset.DeadHitPointLevel` (0). Ch 2, p.13 / Ch 6, p.156 ("Fatal Wound"): "if their hit
  points reach 0, they die at the end of the following round"; "Your character is immediately
  knocked prone but unable to take any action of any type."
- Both thresholds are loaded from `damage-ruleset.json` by `Brp.Data.NoirDamageRuleset.Load()`,
  not hardcoded (AGENTS.md invariant 7).
- Negative HP is tracked: `DamageResolver.Apply` calls `AbilitySet.SetCurrentHitPoints` with the
  unclamped (possibly negative) result, matching Ch 6, p.156's "0 or negative hit points."

**Knockout's major-wound threshold reuses `AbilitySet.MajorWoundLevel`** (Layer 1,
`majorWoundDivisor` = 2, rounded up) rather than a second copy of the same "half of maximum hit
points" figure in `damage-ruleset.json`. Ch 6, p.156 defines a major wound as "equal to or more
than half the character's total hit points" — `>=`, not the ruleset draft's looser "exceeds half"
prose — so `DamageResolver` compares with `>=` against `target.MajorWoundLevel`, and
`damage-ruleset.json`'s `knockoutRule.damageThresholdNote` records why this is intentional and
where the authoritative figure lives.

### Dead-at-end-of-following-round — modeled as a caller-driven seam, not a round loop

Ch 2 p.13 and Ch 6 p.156 both describe death as *timed*, not instantaneous: a character reduced
to 0 HP is fatally wounded immediately but does not die until the end of the *following* round,
and Ch 6's fuller "Fatal Wound" spot rule (p.156) gives a window in that round or the one after
for medical attention (First Aid, Medicine, a power, an item) to restore them above 0 first.

This piece deliberately does not build a round-tracking mechanism to enforce that timing itself
— that would either duplicate piece B's `CombatRound` inappropriately or reach into piece E's
First Aid window, both out of scope for #52. Instead:

- `ApplyDamage` returns `HitPointCondition.FatallyWounded` the instant HP crosses the threshold
  — the flag Ch 6 p.156 describes ("immediately knocked prone").
- `DamageResolver.ResolvesToDeath(int hitPointsAtEndOfFollowingRound, DamageRuleset ruleset)` is
  a pure, stateless function a future combat-round loop calls *at the actual end of the following
  round*, with whatever HP the target has by then (after any First Aid piece E might have
  applied). It returns whether death resolves.

This is a thin seam, not a full implementation of the spot rule's medical-attention window —
piece E and the injury spot rules own that logic; this resolver only guarantees the timing
question has a place to be asked from data-driven thresholds rather than an instant kill.

### Knockout attacks (Ch 7, p.174) — the real two-branch rule, not a bare fraction check

The printed rule has more structure than "damage > half HP knocks out":

> A Difficult attack roll is made as if targeting a particular body part... The attack is
> non-lethal and is not intended to do damage, though damage is rolled to determine the potential
> for a knockout. **Armor defends normally in all cases.** If the damage is equivalent to a minor
> wound, the original damage rolled is ignored and **the target is dealt the minimum damage for
> the weapon (after armor) but is not knocked out**. If the attack is successful and the rolled
> damage is equivalent to a major wound (after armor), **the target takes 1 damage and is knocked
> out for 1D10+10 rounds**... The effects of special or critical successes (such as extra damage
> or bypassing armor) apply in all cases, while other special effects (slashing damage, knockback,
> etc.) do not apply to knockout attempts.

`DamageResolver.ResolveKnockoutAttack` implements exactly this:

1. Roll damage for the landed grade using the ordinary `RollDamage` path — "armor defends
   normally in all cases" is read as "apply the grade's ordinary armor treatment" (still ignored
   on a Critical), which is also what "special or critical successes... apply in all cases" says
   in the same paragraph; not a knockout-specific armor override.
2. Compare that rolled damage against `target.MajorWoundLevel` (`>=` is major, `<` is minor).
3. **Minor branch**: the rolled damage is discarded; the target instead takes
   `weapon.Damage.MinimumPossible()` (a new method added to `DiceExpression` for this — see
   below), after the same armor treatment, and is not knocked out.
4. **Major branch**: the target takes a flat 1 damage and is knocked out for
   `DamageRuleset.KnockoutDuration.Roll(entropy)` (`1D10+10`, from data).
5. A Miss deals no damage and does not knock out (the attack simply failed).

What this piece does **not** validate: that the declared attack roll was actually made at
Difficult odds, that it targeted a valid head-bearing creature, or that the declaration happened
at the start of the round. Those are the attack-roll and declaration mechanics of the round loop
and skill-resolution layers, not this resolver's concern; `ResolveKnockoutAttack`'s parameter
docs name this as the caller's responsibility.

### `DiceExpression.MaximumPossible()` / `MinimumPossible()` — a small Core addition

Neither method existed before this piece, and both Critical damage (needs the weapon's maximum)
and the knockout minor-wound branch (needs the weapon's minimum) require them. Added to
`Brp.Core.Dice.DiceExpression` as pure, entropy-free computations over the parsed terms — signed
dice terms contribute their highest face when positively signed and lowest (1) when negatively
signed (the reverse for the minimum), constants contribute their fixed value either way. Both
throw `NotSupportedException` for a `db`/`db/2` term: weapon damage notation never embeds one (db
is rolled and added separately, per the p.147 footnote), so no in-scope caller can trigger this,
and no rule in Ch 6 or Ch 7 defines what a context-free bound on a rolled-elsewhere value would
even mean.

## Explicitly out of scope (seams for other pieces)

- **First Aid / healing per wound, the Major-Wound *effect*** (shock collapse, unconscious for
  an hour) — piece E. `AbilitySet.MajorWoundLevel` already exists; this piece only reads it for
  the knockout comparison, it does not implement what happens when a *non-knockout* attack
  crosses it.
- **Hit-location rolling / targeting** — the applicable armor value is supplied by the caller to
  every `DamageResolver` method; which location was struck and how per-location armor is selected
  is a separate, not-yet-built concern.
- **The injury/environmental spot rules** (falling, poison, disease) — a sibling issue. They will
  produce damage that flows through `DamageResolver.Apply`'s HP-application path, but are not
  authored here.
- **Fumble tables** — piece C already carries the flags (`DefenderRollsOnFumbleTable` /
  `AttackerRollsOnFumbleTable`); the tables themselves are a separate piece.
- **The five special-success damage *types*** (bleeding, crushing, entangling, impaling,
  knockback, Ch 6 pp.148–149) and their mechanical effects (e.g. bleeding's ongoing 1 HP/round,
  crushing's doubled damage modifier and Stamina-roll-or-stunned check) — #52's scope names these
  explicitly out; `DamageRoll`'s `SourceText` notes where a Special hit's grade is available for a
  future piece to key off of, but no special-effect logic exists here.
- **Half damage bonus for thrown weapons** (Ch 6 p.147 / Ch 3 p.47's "entirely self-propelled"
  distinction) — no thrown weapon exists in the hand-picked gear subset yet (#42), so
  `WeaponDefinition.ApplyDamageBonus` stays a boolean rather than a three-way (none/half/full)
  value. `DamageResolver`'s private `RollDamageBonus` helper documents this as the seam a future
  thrown-weapon addition would extend.
- **Total Hit Points / Death's Door optional rules** — cut per `orc-scope-filter.md`.

## Consequences

- `DamageResolver` is the single seam piece C and future combat-loop code call to turn a landed
  hit into HP loss and a wound; `Brp.Rules.Combat` still has no game-engine dependency.
- `damage-ruleset.json`'s corrected `specialSuccessDamage` section and its `correctionNote` are
  the load-bearing artifact for future rules-conformance passes: if a later change reintroduces
  "weaponMax + normalRoll" for Special, `Special_success_uses_the_same_dice_arithmetic_as_a_normal_hit_not_weaponMax_plus_a_fresh_roll`
  in `DamageResolverTests` fails, by design.
- The dead-at-end-of-following-round timing is a stateless predicate, not a scheduled event —
  piece B's combat-round loop (not yet extended to call it) and piece E's First Aid window are
  both future consumers of `DamageResolver.ResolvesToDeath`.
