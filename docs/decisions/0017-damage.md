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
piece. **Two** successive corrections were needed to the special-success formula before it
matched the book — see "Special-success damage is weapon-type-dependent" below, which records
both, since the record of a wrong correction being caught is as load-bearing as the fix itself
(AGENTS.md's "an unmarked assertion is a defect even when it happens to be right").

## Decision

### Special-success damage is weapon-type-dependent — corrected twice against the ruleset draft

**First (wrong) draft** — `damage-ruleset.json`'s original `specialSuccessDamage.formula` read
`weaponMaxDieResult + normalDamageDiceRoll + damageBonus - armor`: the weapon's maximum result
plus a *second*, fresh roll of the same dice, for every weapon. Ch 6, p.147, footnote `**` (the
footnote the ruleset's own `printedFootnoteExplanation` field already quoted, without drawing the
conclusion its own prose demanded) disproves this: "This is the damage which that type of attack
would normally do. This is not the same as 'maximum damage'. **For a greatsword, full damage is
2D8 on a normal success, 2D8 bleeding damage on a special success**, and on a critical success it
does 16 damage ignoring armor." The greatsword's normal-success dice (2D8) and special-success
dice (2D8) are identical — no maximum-plus-fresh-roll combination anywhere.

**Second (also wrong) draft, made during this same issue's implementation** —
"Special repeats Normal's dice + db exactly, for every weapon," on the strength of Ch 6, p.146's
general "Special Success" entry ("Often, a special attack means that the weapon does normal
damage in addition to a special result") and the same p.147 footnote. This is **also wrong**,
independently caught by both `rules-conformance` and Codex: it is only true for the three special
types whose special *result* is a separable effect layered on unchanged damage. It is false for
the other two types, whose special success changes the damage *number* itself:

- **Impaling** (Ch 6, pp.149–150): "An impale doubles the dice and modifier for the weapon's
  normal rolled damage... a short sword normally does 1D6+1 points of damage, while an impale
  with the same weapon does twice that, or 2D6+2 points of damage... Only the weapon's damage is
  doubled. If the attacker has a damage modifier, the damage modifier is not doubled, but is
  instead rolled normally and added to the damage." Ch 6, p.148 (the type's own definition):
  "Firearms, arrows, and other pointed weapons inflict impaling damage."
- **Crushing** (Ch 6, p.149): "A crushing special success doubles the damage modifier normally
  applied to the attack. If the attacker has a negative damage modifier, this becomes no damage
  modifier, and if there is no damage modifier, it becomes +1D4... The weapon's damage is rolled
  normally, but the damage modifier is increased." Ch 6, p.148: "Clubs, unarmed strikes, and
  other blunt weapons can cause crushing damage."
- **Bleeding / Entangling / Knockback** (Ch 6, pp.149–151): each has a genuinely unchanged-damage
  special success — the *effect* (ongoing bleeding, pinning, being sent sprawling) is what
  differs, matching the second draft's premise. No weapon in the hand-picked gear subset uses any
  of these three types, so the earlier (wrong) blanket rule never actually surfaced in a shipped
  weapon's numbers — which is exactly why two independent falsification passes, not one, were
  needed to catch it.

**Corrected formula, as implemented in `DamageResolver.RollDamage` / `RollSpecialDamage`:**

| Landed grade | Damage | Armor |
|---|---|---|
| Miss | none | n/a |
| Normal | weapon dice + db | subtracted |
| Special — Impaling | 2 × weapon dice (dice **and** fixed modifier) + db (undoubled) | subtracted |
| Special — Crushing | weapon dice (normal) + doubled db (or +1D4 if none; negative db → 0) | subtracted |
| Special — Bleeding/Entangling/Knockback | weapon dice + db (identical to Normal); effect deferred | subtracted |
| Critical | weapon maximum + db | ignored |

**Doubling technique.** Rather than parsing and doubling a `DiceExpression`'s internal terms,
`DamageResolver` rolls the same expression *twice*, independently, and sums the raw totals. This
is not an approximation: for a weapon `NdM+C`, two independent rolls summed have exactly the
distribution of `2N`d`M`+`2C` — each of the `N`-dice groups draws its own `M`-sided faces, so
summing two independent `N`-dice draws is indistinguishable from drawing `2N` dice from the same
population, and the fixed constant doubles arithmetically either way. The book's own worked
example is consistent with this: a short sword's `1D6+1` impale becomes `2D6+2`, which is exactly
what summing two independent `1D6+1` rolls produces. `RollCrushingDamageBonus` reuses the
identical technique for a Crushing special's doubled damage bonus.

**Worked examples** (from `DamageResolverTests`, fixed entropy):

- *Medium Pistol* (`1D8`, no db, Impaling) special success with scripted dice `6, 7`: two
  independent `1D8` rolls, summed = `13`. **Not** a single `1D8` roll (which would cap at 8) —
  this is the test the coordinator's brief named directly
  (`Medium_pistol_special_success_doubles_1D8_not_a_single_1D8_roll`).
- *Club* (`1D8`, db `1D4`, Crushing) special success with scripted dice `5, 3, 2`: one normal
  weapon roll (`5`), then the db rolled *twice* and summed (`3 + 2 = 5`) for a doubled db, total
  `10`. A no-db Crushing club instead substitutes the ruleset's `+1D4` fallback for the (absent)
  db roll.

**Weapon classification** (`weapon-ruleset.json`'s `specialDamageType` field, all 18 shipped
weapons, each with an inline `specialDamageTypeSource` citation):

- **Impaling** — all 12 firearms (`pistolDerringer`, `pistolLight`, `pistolMedium`,
  `pistolHeavy`, `revolverLight`, `revolverMedium`, `revolverHeavy`, `rifleBoltAction`,
  `rifleSniper`, `shotgunDoubleBarreled`, `shotgunSawedOff`, `gunSubmachine`) and the 3 knives
  (`knifeButcher`, `knifePocket`, `knifeSwitchblade`) — 15 weapons. Ch 6, p.148: "Firearms,
  arrows, and other pointed weapons inflict impaling damage"; knives are pointed/thrusting
  weapons (Ch 8's Dagger class), not edged slashing weapons, so they classify as pointed rather
  than as the (unused) Bleeding type.
- **Crushing** — `brassKnuckles`, `clubHeavy`, `clubLight` — 3 weapons. Ch 6, p.148: "Clubs,
  unarmed strikes, and other blunt weapons can cause crushing damage."
- **Bleeding / Entangling / Knockback** — no shipped weapon (no edged slashing weapon, net,
  rope, or shield exists in the hand-picked subset); the enum values and their unchanged-damage
  formula exist for a future weapon addition, per `SpecialDamageType`'s own remarks.

15 + 3 = 18, covering every weapon in `weapon-ruleset.json`;
`NoirGearRulesetTests.The_special_damage_type_table_covers_every_shipped_weapon_exactly_once`
pins this as a table, not a sample.

`damage-ruleset.json` records both corrections inline
(`specialSuccessDamage.correctionHistory`, an ordered list of what was tried and why each attempt
failed) rather than silently overwriting the file a second time, so a future reader sees the
falsification history, not just the final answer.

Db is rolled once, separately, and added to the raw (pre-floor) weapon-dice total before the
floor-at-zero and armor subtraction are applied — Ch 6, p.147 footnote: "Damage modifier, in all
cases, is rolled separately and added afterwards." `WeaponDefinition.ApplyDamageBonus` gates
whether it applies at all (firearms do not receive it) — this still holds for Impaling (db added
once, undoubled) and is superseded only for Crushing's own doubling/substitution rule.

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
   in the same paragraph; not a knockout-specific armor override. Because this reuses
   `RollDamage` unchanged, an Impaling special's doubled damage (or a Crushing special's
   doubled/substituted damage modifier) already flows into the roll used for the next step —
   `ResolveKnockoutAttack` does not special-case this itself, and
   `Knockout_attack_with_an_impaling_special_uses_the_doubled_damage_for_the_major_wound_determination`
   pins that an undoubled roll that would otherwise fall short of a major wound crosses the
   threshold once Impaling's doubling is applied.
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
- **The special-success *effects*** (bleeding's ongoing 1 HP/round, crushing's
  Stamina-roll-or-stunned check, entangling's pinning, impaling's lodged-weapon/extraction rules,
  knockback's resisted shove, Ch 6 pp.149–151) — #52's scope names these explicitly out. Only the
  *damage number* each type produces is implemented (see above); `DamageRoll.SpecialDamageTypeApplied`
  and `SourceText` carry which type applied so a future piece can key its effect off of the same
  roll without re-deriving it.
- **Half damage bonus for thrown weapons** (Ch 6 p.147 / Ch 3 p.47's "entirely self-propelled"
  distinction) — no thrown weapon exists in the hand-picked gear subset yet (#42), so
  `WeaponDefinition.ApplyDamageBonus` stays a boolean rather than a three-way (none/half/full)
  value. `DamageResolver`'s private `RollDamageBonus` helper documents this as the seam a future
  thrown-weapon addition would extend.
- **Total Hit Points / Death's Door optional rules** — cut per `orc-scope-filter.md`.

## Consequences

- `DamageResolver` is the single seam piece C and future combat-loop code call to turn a landed
  hit into HP loss and a wound; `Brp.Rules.Combat` still has no game-engine dependency.
- `damage-ruleset.json`'s `specialSuccessDamage.correctionHistory` and `specialDamageByType`
  section, plus `weapon-ruleset.json`'s per-weapon `specialDamageType` field, are the load-bearing
  artifacts for future rules-conformance passes: if a later change reintroduces a uniform
  "special = normal" or "special = weaponMax + normalRoll" formula, either
  `Impaling_special_doubles_the_whole_weapon_damage_expression_and_adds_an_undoubled_damage_bonus`
  or `Crushing_special_rolls_normal_weapon_dice_but_doubles_a_positive_damage_bonus` in
  `DamageResolverTests` fails, by design — and `A_special_hit_deals_more_than_a_normal_hit_for_every_shipped_special_damage_type`
  fails if either type's damage number regresses to match Normal.
- The dead-at-end-of-following-round timing is a stateless predicate, not a scheduled event —
  piece B's combat-round loop (not yet extended to call it) and piece E's First Aid window are
  both future consumers of `DamageResolver.ResolvesToDeath`.
