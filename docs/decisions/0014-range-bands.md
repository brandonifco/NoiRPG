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

1. Ch 6: Combat, "Missile Weapons" (p.154) -- the band ladder itself.
2. Ch 7: Spot Rules, "Extended Range" (p.171) and "Point-blank Range" (p.176) -- corroborating
   text for the same ladder, used here to cross-check every threshold.
3. Ch 5: System, "Situational Modifiers" table (p.132) -- a generic, additive "Range" condition
   row ("Far beyond the normal range -50%", "Well within range +20%", etc.).

## Decision

### The four bands -- sourced

Ch 6 (p.154), "Missile Weapons":

| Band | Distance | Effect |
|---|---|---|
| Point Blank | within the attacker's DEX/3 meters (round up) | Easy |
| Normal Range | within the weapon's standard listed range | unmodified |
| Medium Range | at double the weapon's standard listed range | Difficult |
| Long Range | at quadruple the weapon's standard listed range | 1/5 normal skill chance |

Corroborated independently by Ch 7, "Extended Range" (p.171): "Within a weapon's base range,
the skill rating is unmodified. At medium range (double the basic range), it becomes Difficult,
and at long range (four times basic range) it becomes 1/5 the normal skill chance (equal to the
chance of a special success, though the result is a normal hit). The rules for point blank range
still apply: missile attacks are Easy at a target less than DEX/3 in meters." And by Ch 7,
"Point-blank Range" (p.176): "Your character's chance to hit with a missile weapon is Easy when
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

**Post-review fix -- dead configuration removed.** An earlier revision also carried a
`RangeBandRuleset.LongRangeMultiplier` field (the quadruple-range value, 4) that
`RangeBandResolver.DetermineBand` never read, because the cumulative-tier reading above has no
upper boundary for Long Range to check against. A cross-vendor conformance pass flagged the field
as inert configuration, against the spirit of AGENTS.md invariant 7 (rules values are data that is
actually used, not data for its own sake). Since the cumulative reading is the one this record
already settles on, the field was removed from `RangeBandRuleset` and `range-band-ruleset.json`
rather than wired in for a boundary the implementation does not have; the "quadruple range" figure
remains recorded in prose and in the Ch 6/7 citations above.

### Point blank and medium use the modifier pipeline's difficulty state -- sourced, and deliberate

Point Blank (Easy) and Medium (Difficult) are ordinary `DifficultyModifier` contributions fed to
`Brp.Core.Modifiers.ModifierPipeline`, participating in ADR 0007's non-stacking collapse like any
other Easy/Difficult condition. This is sourced, not incidental: Ch 7, "Firing into Combat"
(p.173) states that when attacker and target are both within close combat range, "the attack is
Easy (for Point-blank Range), so the Difficult and Easy modifiers cancel one another" -- the book
itself treats point blank and medium range as members of the same difficulty ladder as every
other Difficult/Easy condition, which is exactly what ADR 0007's collapse rule requires and what
`RangeBandResolverTests.Medium_range_collapses_with_another_difficult_condition_...` and
`Point_blank_and_a_difficult_condition_cancel_pairwise` assert.

**Observed, not a defect:** this collapse behaviour applies only to an *unaimed* Medium-range
shot, where the range effect is a plain `DifficultyModifier`. An *aimed*, targeting-equipment
-dampened Medium shot (see "Targeting equipment" below) is instead a `MultiplicativeModifier`
(×3/4) -- an independent rational multiplier, per ADR 0007, not a member of the Difficult/Easy
ladder -- and so it does *not* collapse with an unrelated Difficult condition the way an unaimed
Medium shot does. This is an inherent consequence of representing the dampened penalty as its own
multiplier rather than a difficulty grade, flagged here so a future reader does not mistake the
difference in behaviour between aimed and unaimed Medium range for a bug.

### Long range is a base ÷ 5 override, not a multiplier -- settled decision on #21

Ch 6/7 both describe long range as "1/5 normal skill chance... equal to the chance of a special
success." Ch 5's Special Success rule (p.128) computes that fraction from the *current* rating
being rolled against -- "a character with 70% in a skill who rolls 14 or lower" -- not from a
running value that has already absorbed other penalties for the same roll.

Modelled as a plain `MultiplicativeModifier` (1/5) composing alongside an unrelated Difficult
grade, the pipeline would apply both to the same running value: base -> base/2 (Difficult) ->
base/10 (the "long range" multiplier), or base -> base/5 -> base/10 in the other order. The book
never sanctions base ÷ 10; it states one number, 1/5, full stop.

**Decision:** `RangeBandResolver.Resolve` computes the override chance directly
(`adjustedBase.Scale(1, 5, RoundingMode.Up)` -- the same ceiling-division shape
`ResolutionPolicy.SpecialThreshold` already uses for special successes) and returns it as a
`RangeBandOutcome.ExclusiveOverride`. `RangeBandResolver.Evaluate` is the only place that turns
that into an `OverrideModifier`, and it does so with every other pending modifier discarded, so a
Difficult condition from elsewhere cannot halve the override again --
`RangeBandResolverTests.Long_range_does_not_stack_with_another_penalty_to_become_one_tenth`
asserts this directly.

**Permanent modifiers fold into the adjusted base first (post-review fix).** Ch 5 (p.132): "any
modifiers that are 'permanent' ... are figured into the skill rating before it is doubled or
halved. These sorts of modifiers are considered integral to the skill." The long-range override
is not a doubling or halving, but the same logic applies -- an integral bonus to the skill is part
of "normal skill chance," not a separate penalty being discarded alongside darkness or firing-into
-combat. So `Resolve` sums any `AdditiveKind.Permanent` entries in the shot's other modifiers,
adds that to `baseChance`, and *then* divides by five: base 65% with a permanent +10 yields
`ceil((65+10)/5) = 15%`, not `ceil(65/5) = 13%`
(`RangeBandResolverTests.Long_range_folds_in_a_permanent_modifier_before_dividing_by_five`).
Situational modifiers in the same list (darkness, firing into combat, and so on) are still
discarded, not folded in
(`Long_range_still_discards_a_situational_modifier_even_though_it_folds_in_permanent_ones`).

An earlier revision of this record used an advisory `IsExclusive` flag on a result that still
exposed a bare, composable `OverrideModifier` -- a caller could take that modifier out of the
result and combine it with an unrelated `DifficultyModifier` via `ModifierPipeline.Evaluate`
directly, recreating the forbidden base ÷ 10 outside `RangeBandResolver.Evaluate`'s own
discarding logic. A cross-vendor conformance pass found this hole. **Fix:** the exclusive
result now carries a plain `Percent` (`RangeBandOutcome.ExclusiveOverride.Chance`), not a
`Modifier` -- it cannot be placed in a `List<Modifier>` and fed to `ModifierPipeline.Evaluate` by
mistake, because it is not a `Modifier` at all. `OverrideModifier` for long range is now
constructed in exactly one place, inside `RangeBandResolver.Evaluate`, making the
non-compounding guarantee a property of the type system rather than of caller discipline.

**Owner's decision -- no change, confirmed against the book:** a long-range shot keeps its normal
five-grade resolution once the base/5 chance is computed. The reduced chance is handed to
`SkillResolver` exactly as any other effective chance would be; critical and special thresholds
are derived from *that* reduced number, not capped at "special" as an artificial ceiling. Nothing
in Ch 5's Special/Critical Success rules (p.128) caps a *roll* at special once its underlying
chance happens to be small, and Ch 6/7's "equal to the chance of a special success, though the
result is a normal hit" language describes only the odds of hitting at all, not a grading rule for
the resulting attack roll. `RangeBandResolverTests.No_grade_capping_is_applied_at_long_range`
documents this by construction rather than testing `SkillResolver` itself (out of scope here).

This uses ADR 0007's Override stage (built for exactly this "flat replacement chance" shape) as
the vehicle, rather than inventing a parallel path -- but the *composition rule* around it (which
other modifiers, if any, are still combined) is `Brp.Rules` combat-layer policy, not something
`Brp.Core.Modifiers` needed to change. `Brp.Core`/`Brp.Rules` architecture (AGENTS.md invariant 6)
is unaffected.

### Throwing-weapon cutoff -- sourced, but keyed to a per-weapon fact, not a whole weapon class

Ch 7, "Extended Range" (p.171): "Small hand-propelled weapons such as the throwing knife and the
throwing axe have no chance to hit beyond double base range."

**Post-review fix.** The first revision of this record keyed the cutoff to the entire
`WeaponClass.Missile` enum value, reasoning that Ch 8, "Weapon Classes" (p.196) files the throwing
knife and throwing axe under that class. It also files the sling and the blowgun under the same
class -- and Ch 3, "Missile Weapon" skill, System Notes (p.47) treats "entirely self-propelled
weapons (blowguns, crossbows, etc.)" as a distinct case for the (unrelated) damage-modifier
halving rule, evidence the book does not treat every "Missile"-class weapon as interchangeable
for range purposes. Keying the cutoff to the whole class would have wrongly denied a sling or
blowgun any chance to hit beyond double range, which the text never says. A rules-conformance
pass caught this before it shipped.

**Fix:** `RangeBandResolver.IsBeyondThrowingCutoff` now takes a plain `bool isHandThrownWeapon`
rather than a `WeaponClass` -- a narrower, per-weapon classification a caller establishes (e.g.
from a future `WeaponDefinition` flag, once a thrown weapon is added to the gear data) rather than
one this ruleset or the coarse skill-specialty `WeaponClass` enum can answer on its own. The cutoff
multiplier itself (`RangeBandRuleset.ThrowingCutoffMultiplier`, 2) is unchanged and still ruleset
data. No shipped weapon uses this yet (ADR 0013's hand-picked subset has no thrown weapon), so it
is exercised with fixtures: `Only_hand_thrown_weapons_are_cut_off_beyond_double_base_range` and
`A_sling_style_missile_weapon_in_the_same_book_class_as_a_throwing_knife_is_not_cut_off`, the
latter modelling both a throwing-knife-style and a sling-style weapon at the same distance to show
only the hand-thrown one loses its chance.

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

- `Brp.Rules.Combat` gains `RangeBand`, `RangeBandRuleset`, `RangeBandOutcome` (with its
  `Composable`/`ExclusiveOverride` cases), and `RangeBandResolver`; `Brp.Data` gains
  `range-band-ruleset.json` / `NoirRangeBandRuleset`, all data-driven per AGENTS.md invariant 7 --
  no threshold, multiplier, or divisor is a C# constant, and no field is carried that the code
  never reads (see the dead-configuration fix above).
- `Brp.Rules.Gear.WeaponClass` gains `Missile`, sourced to Ch 8 (p.196), ahead of any weapon in
  the current subset using it -- but, per the throwing-cutoff fix above, this enum value no
  longer drives any range-band behaviour on its own; it exists purely as the book's skill-specialty
  classification.
- `RangeBandOutcome.ExclusiveOverride` replaces the earlier `RangeBandModifiers.IsExclusive` flag
  as the mechanism for "this result must not be combined with anything else": the guarantee is now
  structural (the case carries a `Percent`, not a `Modifier`) rather than a flag a caller could
  ignore, without changing `Brp.Core.Modifiers`. Any future combat-layer rule with the same
  "override, full stop" shape should reuse this shape rather than re-deriving the guarantee.
- Reconciling a scoped/laser-sighted weapon's Ch 8 range-doubling property with this issue's Ch 6
  aiming-and-targeting-gear rule is explicitly deferred; a caller combining both today must decide
  which "listed range" feeds `RangeBandResolver.DetermineBand` without help from this ruleset.
- The hand-thrown classification the throwing cutoff now depends on is not yet backed by a
  `WeaponDefinition` field -- a caller must supply the `bool` itself. Adding that field to the
  Gear layer schema (ADR 0013) is deferred to whichever future issue first adds a thrown weapon to
  the data.
