using Brp.Core.Modifiers;
using Brp.Core.Primitives;
using Brp.Rules.Combat;
using Brp.Rules.Gear;

namespace Brp.Rules.Tests.Combat;

/// <summary>
/// Covers the four range bands against Ch 6: Combat, "Missile Weapons" (p.153-154) and Ch 7:
/// Spot Rules, "Extended Range" (p.170), plus the throwing-weapon cutoff and targeting-equipment
/// dampening. See <c>docs/decisions/0014-range-bands.md</c>.
/// </summary>
public class RangeBandResolverTests
{
    private static readonly RangeBandRuleset Ruleset = new(
        pointBlankDexDivisor: 3,
        mediumRangeMultiplier: 2,
        longRangeMultiplier: 4,
        longRangeChanceNumerator: 1,
        longRangeChanceDenominator: 5,
        throwingCutoffMultiplier: 2,
        throwingWeaponClasses: [WeaponClass.Missile],
        targetingEquipmentDampeningNumerator: 1,
        targetingEquipmentDampeningDenominator: 2);

    // ---- Band thresholds (the printed table, reproduced in full) --------------------------

    [Theory]
    // DEX 12 -> point blank at ceil(12/3) = 4 meters. Listed range 20.
    [InlineData(1, 12, 20, RangeBand.PointBlank)]
    [InlineData(4, 12, 20, RangeBand.PointBlank)] // exactly DEX/3, still Easy
    [InlineData(5, 12, 20, RangeBand.Normal)]
    [InlineData(20, 12, 20, RangeBand.Normal)] // exactly the listed range, still unmodified
    [InlineData(21, 12, 20, RangeBand.Medium)]
    [InlineData(40, 12, 20, RangeBand.Medium)] // exactly double, still Difficult
    [InlineData(41, 12, 20, RangeBand.LongRange)]
    [InlineData(80, 12, 20, RangeBand.LongRange)] // exactly quadruple
    [InlineData(500, 12, 20, RangeBand.LongRange)] // book defines no band past quadruple; we do not invent one
    public void Distance_maps_to_the_correct_band(int distance, int dexterity, int listedRange, RangeBand expected)
    {
        var band = RangeBandResolver.DetermineBand(distance, dexterity, listedRange, Ruleset);

        Assert.Equal(expected, band);
    }

    [Theory]
    [InlineData(1, 1)]  // ceil(1/3) = 1
    [InlineData(3, 1)]  // ceil(3/3) = 1
    [InlineData(4, 2)]  // ceil(4/3) = 2
    [InlineData(12, 4)] // ceil(12/3) = 4
    [InlineData(13, 5)] // ceil(13/3) = 5
    public void Point_blank_distance_derives_from_DEX_divided_by_three_rounded_up(int dexterity, int expected)
    {
        var meters = RangeBandResolver.PointBlankDistanceMeters(dexterity, Ruleset);

        Assert.Equal(expected, meters);
    }

    // ---- Point blank: Easy ------------------------------------------------------------------

    [Fact]
    public void Point_blank_is_an_easy_grade()
    {
        var result = RangeBandResolver.Resolve(RangeBand.PointBlank, Percent.Of(50), aimedWithTargetingEquipment: false, Ruleset);

        var difficulty = Assert.Single(result.Modifiers.OfType<DifficultyModifier>());
        Assert.Equal(DifficultyDirection.Easier, difficulty.Direction);
        Assert.False(result.IsExclusive);
    }

    // ---- Normal: unmodified -----------------------------------------------------------------

    [Fact]
    public void Normal_range_contributes_no_modifier()
    {
        var result = RangeBandResolver.Resolve(RangeBand.Normal, Percent.Of(50), aimedWithTargetingEquipment: false, Ruleset);

        Assert.Empty(result.Modifiers);
        Assert.False(result.IsExclusive);
    }

    // ---- Medium: Difficult, and it collapses with other Difficult conditions ---------------

    [Fact]
    public void Medium_range_is_a_difficult_grade()
    {
        var result = RangeBandResolver.Resolve(RangeBand.Medium, Percent.Of(50), aimedWithTargetingEquipment: false, Ruleset);

        var difficulty = Assert.Single(result.Modifiers.OfType<DifficultyModifier>());
        Assert.Equal(DifficultyDirection.Harder, difficulty.Direction);
        Assert.False(result.IsExclusive);
    }

    [Fact]
    public void Medium_range_collapses_with_another_difficult_condition_under_ADR_0007_non_stacking()
    {
        // ADR 0007: "Any number of sources of 'Difficult' produce one halving." Medium range is
        // an ordinary Difficult grade, not a separate multiplicative tier, so firing at a target
        // in medium range while also, say, firing into a melee (also Difficult) must halve once,
        // not twice -- 65% -> 33%, never 17%.
        var otherModifiers = new Modifier[] { DifficultyModifier.Difficult("firing into combat") };

        var chain = RangeBandResolver.Evaluate(
            Percent.Of(65),
            RangeBand.Medium,
            otherModifiers,
            aimedWithTargetingEquipment: false,
            Ruleset);

        Assert.Equal(Percent.Of(33), chain.EffectiveChance); // ceil(65/2), one halving only
    }

    [Fact]
    public void Point_blank_and_a_difficult_condition_cancel_pairwise()
    {
        // Ch 7, "Firing into Combat" (p.169): "target are both within close combat range, the
        // attack is Easy (for Point-blank Range), so the Difficult and Easy modifiers cancel
        // one another." ADR 0007 models this as the same non-stacking collapse.
        var otherModifiers = new Modifier[] { DifficultyModifier.Difficult("firing into combat") };

        var chain = RangeBandResolver.Evaluate(
            Percent.Of(65),
            RangeBand.PointBlank,
            otherModifiers,
            aimedWithTargetingEquipment: false,
            Ruleset);

        Assert.Equal(Percent.Of(65), chain.EffectiveChance);
    }

    // ---- Long range: base ÷ 5 override, not a multiplier on the running value --------------

    [Fact]
    public void Long_range_is_one_fifth_of_the_base_rating_rounded_up()
    {
        var result = RangeBandResolver.Resolve(RangeBand.LongRange, Percent.Of(65), aimedWithTargetingEquipment: false, Ruleset);

        Assert.True(result.IsExclusive);
        var over = Assert.IsType<OverrideModifier>(Assert.Single(result.Modifiers));
        Assert.Equal(Percent.Of(13), over.Value); // ceil(65/5) = 13
    }

    [Fact]
    public void Long_range_resolves_against_the_unmodified_base_not_the_running_value()
    {
        var chain = RangeBandResolver.Evaluate(
            Percent.Of(65),
            RangeBand.LongRange,
            otherModifiers: [],
            aimedWithTargetingEquipment: false,
            Ruleset);

        Assert.Equal(Percent.Of(13), chain.EffectiveChance);
    }

    [Fact]
    public void Long_range_does_not_stack_with_another_penalty_to_become_one_tenth()
    {
        // The settled decision on #21: long range's 1/5 is an override, full stop. Modelled as a
        // plain multiplier alongside a Difficult grade it would wrongly yield base / 10 (65 -> 33
        // -> 7, or 65 -> 13 -> 7); the book never sanctions that. A Difficult condition from
        // elsewhere (e.g. firing into combat) must not compound with the long-range override.
        var otherModifiers = new Modifier[] { DifficultyModifier.Difficult("firing into combat") };

        var chain = RangeBandResolver.Evaluate(
            Percent.Of(65),
            RangeBand.LongRange,
            otherModifiers,
            aimedWithTargetingEquipment: false,
            Ruleset);

        Assert.Equal(Percent.Of(13), chain.EffectiveChance);
        Assert.NotEqual(Percent.Of(7), chain.EffectiveChance); // the forbidden base / 10 result
    }

    // ---- Throwing-weapon cutoff: a weapon-class rule, not a distance tier ------------------

    [Theory]
    [InlineData(WeaponClass.Missile, 41, 20, true)]  // beyond double base range (20*2=40)
    [InlineData(WeaponClass.Missile, 40, 20, false)] // exactly double: still has a chance
    [InlineData(WeaponClass.Missile, 10, 20, false)] // well within range
    [InlineData(WeaponClass.Pistol, 41, 20, false)]  // not a throwing-class weapon: no cutoff at all
    [InlineData(WeaponClass.Pistol, 1000, 20, false)]
    public void Only_throwing_weapon_classes_are_cut_off_beyond_double_base_range(
        WeaponClass weaponClass, int distance, int listedRange, bool expected)
    {
        var cutOff = RangeBandResolver.IsBeyondThrowingCutoff(weaponClass, distance, listedRange, Ruleset);

        Assert.Equal(expected, cutOff);
    }

    // ---- Targeting equipment: halves range modifiers after a round spent aiming -----------

    [Fact]
    public void Targeting_equipment_halves_the_severity_of_a_medium_range_penalty()
    {
        // Unaimed: Difficult, i.e. x1/2. Aimed: the shortfall from 1 (1/2) is itself halved,
        // giving x3/4 instead -- see RangeBandRuleset.TargetingEquipmentDampeningNumerator.
        var result = RangeBandResolver.Resolve(RangeBand.Medium, Percent.Of(60), aimedWithTargetingEquipment: true, Ruleset);

        var multiplier = Assert.IsType<MultiplicativeModifier>(Assert.Single(result.Modifiers));
        Assert.Equal(3, multiplier.Numerator);
        Assert.Equal(4, multiplier.Denominator);
        Assert.False(result.IsExclusive);
    }

    [Fact]
    public void Targeting_equipment_halves_the_severity_of_the_long_range_override()
    {
        // Unaimed: 1/5 (a 4/5 shortfall from 1). Aimed: half that shortfall, giving 3/5.
        var result = RangeBandResolver.Resolve(RangeBand.LongRange, Percent.Of(100), aimedWithTargetingEquipment: true, Ruleset);

        var over = Assert.IsType<OverrideModifier>(Assert.Single(result.Modifiers));
        Assert.Equal(Percent.Of(60), over.Value); // 100 * 3/5
        Assert.True(result.IsExclusive);
    }

    [Fact]
    public void Point_blank_and_normal_range_are_unaffected_by_targeting_equipment()
    {
        var pointBlank = RangeBandResolver.Resolve(RangeBand.PointBlank, Percent.Of(50), aimedWithTargetingEquipment: true, Ruleset);
        var normal = RangeBandResolver.Resolve(RangeBand.Normal, Percent.Of(50), aimedWithTargetingEquipment: true, Ruleset);

        Assert.Equal(DifficultyDirection.Easier, Assert.Single(pointBlank.Modifiers.OfType<DifficultyModifier>()).Direction);
        Assert.Empty(normal.Modifiers);
    }

    // ---- The generic Situational Modifiers "Range" row is not double-applied --------------

    [Fact]
    public void Resolving_a_band_never_emits_the_generic_situational_range_row()
    {
        // Ch 5: System, "Situational Modifiers" table (p.132) prints a generic, additive "Range"
        // condition row ("Far beyond the normal range -50%", etc.). For a missile/firearm attack
        // the Ch 6/7 multiplicative bands are authoritative and this row must not be layered on
        // top of them -- so RangeBandResolver never produces a generic range AdditiveModifier at
        // any band, only the band's own DifficultyModifier/MultiplicativeModifier/OverrideModifier.
        foreach (var band in Enum.GetValues<RangeBand>())
        {
            var result = RangeBandResolver.Resolve(band, Percent.Of(50), aimedWithTargetingEquipment: false, Ruleset);

            Assert.DoesNotContain(result.Modifiers, m => m is AdditiveModifier);
        }
    }
}
