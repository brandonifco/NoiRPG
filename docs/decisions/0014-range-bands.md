# 0014. Missile/firearm range bands: the four bands, the long-range override, and reconciling three treatments of range

## Status

Accepted — 2026-08-30. Resolves #21. Amends ADR 0007's "Range bands are out of scope here"
section, which named this record's home but did not attempt the mechanics.

## Context

ADR 0007 records that an earlier draft specified range bands as a modifier-pipeline concern
with its own multiplicative tier, that a conformance pass found the draft wrong in every
particular (wrong thresholds, a fabricated cutoff, and a missing Easy grade), and that the
correct rules belong to the combat layer, not Layer 0. This record does that work.

Three distinct book passages touch "range," and they had to be reconciled rather than
implemented independently:

1. Ch 6: Combat, "Missile Weapons" (p.153-154) -- the band ladder itself.
2. Ch 7: Spot Rules, "Extended Range" (p.170) and "Point-blank Range" (p.175) -- corroborating
   text for the same ladder, used here to cross-check every threshold.
3. Ch 5: System, "Situational Modifiers" table (p.132) -- a generic, additive "Range" condition
   row ("Far beyond the normal range -50%", "Well within range +20%", etc.).

## Decision

### The four bands -- sourced

Ch 6 (p.153), "Missile Weapons":

| Band | Distance | Effect |
|---|---|---|
| Point Blank | within the attacker's DEX/3 meters (round up) | Easy |
| Normal Range | within the weapon's standard listed range | unmodified |
| Medium Range | at double the weapon's standard listed range | Difficult |
| Long Range | at quadruple the weapon's standard listed range | 1/5 normal skill chance |

Corroborated independently by Ch 7, "Extended Range" (p.170): "Within a weapon's base range,
the skill rating is unmodified. At medium range (double the basic range), it becomes Difficult,
and at long range (four times basic range) it becomes 1/5 the normal skill chance (equal to the
chance of a special success, though the result is a normal hit). The rules for point blank range
still apply: missile attacks are Easy at a target less than DEX/3 in meters." And by Ch 7,
"Point-blank Range" (p.175): "Your character's chance to hit with a missile weapon is Easy when
the range to the target is less than or equal to their DEX/3 in meters."

Every threshold in the table above, and the two errors the earlier draft introduced (a
"beyond three times range is impossible" cutoff, and a halving/quartering scheme instead of
Easy/Difficult/one-fifth) were checked against both citations, not carried over from the
superseded draft or from the Issue text.

**House shape, not sourced:** the book states the three penalty bands as thresholds at exact
multiples (1x, 2x, 4x) rather than explicit ranges. This implementation reads them as cumulative
tiers -- Normal up to 1x, Medium from just past 1x up to 2x, Long from just past 2x onward --
which is the only reading under which every distance has a defined band. The book states no
band past quadruple range, and this implementation does not invent one (the fabricated "beyond
3x is impossible" rule this record replaces is exactly that mistake); distances beyond quadruple
range remain Long Range rather than becoming un-hittable.

Point-blank distance is derived from DEX (`ceil(DEX / 3)`), not a fixed number of metres --
`RangeBandResolver.PointBlankDistanceMeters`.

### Point blank and medium use the modifier pipeline's difficulty state -- sourced, and deliberate

Point Blank (Easy) and Medium (Difficult) are ordinary `DifficultyModifier` contributions fed to
`Brp.Core.Modifiers.ModifierPipeline`, participating in ADR 0007's non-stacking collapse like any
other Easy/Difficult condition. This is sourced, not incidental: Ch 7, "Firing into Combat"
(p.169) states that when attacker and target are both within close combat range, "the attack is
Easy (for Point-blank Range), so the Difficult and Easy modifiers cancel one another" -- the book
itself treats point blank and medium range as members of the same difficulty ladder as every
other Difficult/Easy condition, which is exactly what ADR 0007's collapse rule requires and what
`RangeBandResolverTests.Medium_range_collapses_with_another_difficult_condition_...` and
`Point_blank_and_a_difficult_condition_cancel_pairwise` assert.

### Long range is a base ÷ 5 override, not a multiplier -- settled decision on #21

Ch 6/7 both describe long range as "1/5 normal skill chance... equal to the chance of a special
success." Ch 5's Special Success rule (p.128) computes that fraction from the *current* rating
being rolled against -- "a character with 70% in a skill who rolls 14 or lower" -- not from a
running value that has already absorbed other penalties for the same roll.

Modelled as a plain `MultiplicativeModifier` (1/5) composing alongside an unrelated Difficult
grade, the pipeline would apply both to the same running value: base -> base/2 (Difficult) ->
base/10 (the "long range" multiplier), or base -> base/5 -> base/10 in the other order. The book
never sanctions base ÷ 10; it states one number, 1/5, full stop.

**Decision:** `RangeBandResolver.Resolve` returns an `OverrideModifier` computed directly against
the base chance passed in (`baseChance.Scale(1, 5, RoundingMode.Up)` -- the same ceiling-division
shape `ResolutionPolicy.SpecialThreshold` already uses for special successes), and marks the
result `IsExclusive`. `RangeBandResolver.Evaluate` honours that flag by discarding every other
pending modifier for the shot and resolving the chain against the override alone --
`RangeBandResolverTests.Long_range_does_not_stack_with_another_penalty_to_become_one_tenth`
asserts this directly, with a Difficult condition from an unrelated source present in the call
and confirmed *not* to compound.

This uses ADR 0007's Override stage (built for exactly this "flat replacement chance" shape) as
the vehicle, rather than inventing a parallel path -- but the *composition rule* around it (which
other modifiers, if any, are still combined) is `Brp.Rules` combat-layer policy, not something
`Brp.Core.Modifiers` needed to change. `Brp.Core`/`Brp.Rules` architecture (AGENTS.md invariant 6)
is unaffected.

### Throwing-weapon cutoff -- sourced, weapon-class rule

Ch 7, "Extended Range" (p.170): "Small hand-propelled weapons such as the throwing knife and the
throwing axe have no chance to hit beyond double base range." Ch 8, "Weapon Classes" (p.196)
files both under the "Missile" class (alongside blowgun, bola, boomerang, dagger, dart, hand axe,
javelin, shuriken, sling).

Implemented as `RangeBandResolver.IsBeyondThrowingCutoff`, keyed to `WeaponClass.Missile` and the
ruleset's `ThrowingCutoffMultiplier` (2) -- a class-level cutoff checked independently of which
`RangeBand` the distance would otherwise fall into, not a fifth band. `WeaponClass.Missile` is
added to the enum by this issue even though the hand-picked gear subset (ADR 0013) has no thrown
weapon yet, specifically so the rule is correct the day one is added -- exercised by a fixture in
`RangeBandResolverTests`, since no shipped weapon currently carries this class.

### Targeting equipment halves range modifiers -- sourced text, house-rule arithmetic

Ch 6 (p.154), "Targeting Gear": "Using long-range goggles, a scope, laser sight, or other
targeting system divides range modifiers by 1/2 if one combat round is taken to aim."

**House rule:** read literally, "divides ... by 1/2" multiplies a penalty by two, which
contradicts the passage's evident purpose (aiming with a scope should help, not hurt, the shot).
This ruleset instead halves the *severity* of the range penalty: the shortfall of the raw
multiplier from 1 is itself halved, so a Difficult (×1/2) medium-range penalty becomes ×3/4, and
a long-range 1/5 override becomes ×3/5. The halving fraction (1/2) is ruleset data
(`RangeBandRuleset.TargetingEquipmentDampeningNumerator`/`Denominator`), not a hardcoded constant,
so a future campaign could tune it without touching code. Point Blank's Easy bonus and Normal
range are left alone, since there is no penalty there to halve.

Note this is a distinct rule from Ch 8's unconditional scope/laser-sight property ("effectively
double the base range" / "quadruple the base range" for scopes and laser sights respectively,
p.223), which changes what counts as a weapon's listed range rather than dampening a penalty
after the fact. This issue implements only the Ch 6 aiming rule; reconciling it with the Ch 8
gear property, if a scoped weapon needs both, is future combat-layer work.

### Bands govern; the additive Situational Modifiers "Range" row does not stack on top -- sourced reconciliation

Ch 5's Situational Modifiers table (p.132) prints a generic "Range" condition row, additive, for
gamemaster use on skills the book gives no dedicated range mechanic to (a Perception roll to spot
something at a distance, for instance). Ch 6/7 print a specific, multiplicative ladder for
missile and firearm *attacks*. Applying both to the same shot would double-count the same
distance under two different arithmetic shapes.

**Decision:** for a missile/firearm attack, the Ch 6/7 bands are authoritative and
`RangeBandResolver` never emits the generic additive row -- confirmed by
`RangeBandResolverTests.Resolving_a_band_never_emits_the_generic_situational_range_row`, which
checks every band produces no `AdditiveModifier`. The generic row remains available, unmodified,
for the non-attack skill checks it was written for; that is out of scope here.

## Consequences

- `Brp.Rules.Combat` gains `RangeBand`, `RangeBandRuleset`, `RangeBandModifiers`, and
  `RangeBandResolver`; `Brp.Data` gains `range-band-ruleset.json` / `NoirRangeBandRuleset`, all
  data-driven per AGENTS.md invariant 7 -- no threshold, multiplier, or divisor is a C# constant.
- `Brp.Rules.Gear.WeaponClass` gains `Missile`, sourced to Ch 8 (p.196), ahead of any weapon in
  the current subset using it.
- `RangeBandModifiers.IsExclusive` is a new concept this record introduces to express "this
  result must not be combined with anything else" without changing `Brp.Core.Modifiers`. Any
  future combat-layer rule with the same "override, full stop" shape should reuse it rather than
  re-deriving the exclusivity logic.
- Reconciling a scoped/laser-sighted weapon's Ch 8 range-doubling property with this issue's Ch 6
  aiming-and-targeting-gear rule is explicitly deferred; a caller combining both today must decide
  which "listed range" feeds `RangeBandResolver.DetermineBand` without help from this ruleset.
