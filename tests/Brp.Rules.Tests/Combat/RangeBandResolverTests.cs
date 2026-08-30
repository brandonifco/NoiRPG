using Brp.Core.Modifiers;
using Brp.Core.Primitives;
using Brp.Rules.Combat;

namespace Brp.Rules.Tests.Combat;

/// <summary>
/// Covers the four range bands against Ch 6: Combat, "Missile Weapons" (p.154) and Ch 7: Spot
/// Rules, "Extended Range" (p.171), plus the throwing-weapon cutoff and targeting-equipment
/// dampening. See <c>docs/decisions/0014-range-bands.md</c>.
/// </summary>
public class RangeBandResolverTests
{
    private static readonly RangeBandRuleset Ruleset = new(
        pointBlankDexDivisor: 3,
        mediumRangeMultiplier: 2,
        longRangeChanceNumerator: 1,
        longRangeChanceDenominator: 5,
        throwingCutoffMultiplier: 2,
        targetingEquipmentDampeningNumerator: 1,
        targetingEquipmentDampeningDenominator: 2);

    private static readonly Modifier[] NoOtherModifiers = [];

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
        var outcome = RangeBandResolver.Resolve(
            RangeBand.PointBlank, Percent.Of(50), NoOtherModifiers, aimedWithTargetingEquipment: false, Ruleset);

        var composable = Assert.IsType<RangeBandOutcome.Composable>(outcome);
        var difficulty = Assert.Single(composable.Modifiers.OfType<DifficultyModifier>());
        Assert.Equal(DifficultyDirection.Easier, difficulty.Direction);
    }

    // ---- Normal: unmodified -----------------------------------------------------------------

    [Fact]
    public void Normal_range_contributes_no_modifier()
    {
        var outcome = RangeBandResolver.Resolve(
            RangeBand.Normal, Percent.Of(50), NoOtherModifiers, aimedWithTargetingEquipment: false, Ruleset);

        var composable = Assert.IsType<RangeBandOutcome.Composable>(outcome);
        Assert.Empty(composable.Modifiers);
    }

    // ---- Medium: Difficult, and it collapses with other Difficult conditions ---------------

    [Fact]
    public void Medium_range_is_a_difficult_grade()
    {
        var outcome = RangeBandResolver.Resolve(
            RangeBand.Medium, Percent.Of(50), NoOtherModifiers, aimedWithTargetingEquipment: false, Ruleset);

        var composable = Assert.IsType<RangeBandOutcome.Composable>(outcome);
        var difficulty = Assert.Single(composable.Modifiers.OfType<DifficultyModifier>());
        Assert.Equal(DifficultyDirection.Harder, difficulty.Direction);
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
        // Ch 7, "Firing into Combat" (p.173): "target are both within close combat range, the
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
        var outcome = RangeBandResolver.Resolve(
            RangeBand.LongRange, Percent.Of(65), NoOtherModifiers, aimedWithTargetingEquipment: false, Ruleset);

        var exclusive = Assert.IsType<RangeBandOutcome.ExclusiveOverride>(outcome);
        Assert.Equal(Percent.Of(13), exclusive.Chance); // ceil(65/5) = 13
    }

    [Fact]
    public void Long_range_resolves_against_the_unmodified_base_not_the_running_value()
    {
        var chain = RangeBandResolver.Evaluate(
            Percent.Of(65),
            RangeBand.LongRange,
            NoOtherModifiers,
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

    [Fact]
    public void Long_range_folds_in_a_permanent_modifier_before_dividing_by_five()
    {
        // Post-review fix on #21: Ch 5 (p.132) figures a permanent/integral modifier into the
        // rating *before* a Difficult/Easy grade -- and, by the same logic, before the long-range
        // override -- touches it: fold-then-divide, not divide-then-add.
        //
        // Discriminating fixture (base 65 + Permanent(+10) does NOT discriminate: both readings
        // give 15, since 65 and 10 are each already multiples of 5 and ceiling division is a
        // no-op on exact multiples -- ceil(75/5)=15 and ceil(65/5)+ceil(10/5)=13+2=15 coincide).
        // Base 66 + Permanent(+1) does discriminate:
        //   correct (fold then divide):  ceil((66+1)/5) = ceil(67/5) = 14
        //   wrong (divide then add):     ceil(66/5) + 1 = 14 + 1        = 15
        // These differ, so asserting 14 pins the fold-before-divide reading the book requires.
        var otherModifiers = new Modifier[] { new AdditiveModifier("specialized training", 1, AdditiveKind.Permanent) };

        var chain = RangeBandResolver.Evaluate(
            Percent.Of(66),
            RangeBand.LongRange,
            otherModifiers,
            aimedWithTargetingEquipment: false,
            Ruleset);

        Assert.Equal(Percent.Of(14), chain.EffectiveChance);
    }

    [Fact]
    public void Long_range_still_discards_a_situational_modifier_even_though_it_folds_in_permanent_ones()
    {
        // Only AdditiveKind.Permanent is folded in; a situational modifier alongside it (e.g.
        // darkness, -20%) must still be discarded for the override, not summed in.
        var otherModifiers = new Modifier[]
        {
            new AdditiveModifier("specialized training", 10, AdditiveKind.Permanent),
            new AdditiveModifier("darkness", -20, AdditiveKind.Situational),
        };

        var chain = RangeBandResolver.Evaluate(
            Percent.Of(65),
            RangeBand.LongRange,
            otherModifiers,
            aimedWithTargetingEquipment: false,
            Ruleset);

        Assert.Equal(Percent.Of(15), chain.EffectiveChance); // ceil((65+10)/5), darkness discarded
    }

    [Fact]
    public void No_grade_capping_is_applied_at_long_range()
    {
        // Owner's decision: a long-range shot keeps its normal five-grade resolution once the
        // base/5 override is computed -- critical and special thresholds are derived from the
        // *reduced* chance by the ordinary resolver, not capped at "special" as an upper bound.
        // RangeBandResolver only produces the effective chance fed to SkillResolver; grading is
        // untouched by #21. This test documents that no grade-capping was added here, not
        // SkillResolver's own behaviour (covered elsewhere).
        var chain = RangeBandResolver.Evaluate(
            Percent.Of(100),
            RangeBand.LongRange,
            NoOtherModifiers,
            aimedWithTargetingEquipment: false,
            Ruleset);

        // 100 / 5 = 20% effective chance -- an ordinary, uncapped Percent, free to be graded by
        // SkillResolver same as any other roll.
        Assert.Equal(Percent.Of(20), chain.EffectiveChance);
    }

    // ---- Throwing-weapon cutoff: a per-weapon fact, not a distance tier or a whole class ---

    [Theory]
    [InlineData(true, 41, 20, true)]   // hand-thrown (e.g. throwing knife), beyond double base range
    [InlineData(true, 40, 20, false)]  // hand-thrown, exactly double: still has a chance
    [InlineData(true, 10, 20, false)]  // hand-thrown, well within range
    [InlineData(false, 41, 20, false)] // not hand-thrown (e.g. sling, blowgun): no cutoff at all
    [InlineData(false, 1000, 20, false)]
    public void Only_hand_thrown_weapons_are_cut_off_beyond_double_base_range(
        bool isHandThrownWeapon, int distance, int listedRange, bool expected)
    {
        var cutOff = RangeBandResolver.IsBeyondThrowingCutoff(isHandThrownWeapon, distance, listedRange, Ruleset);

        Assert.Equal(expected, cutOff);
    }

    [Fact]
    public void A_sling_style_missile_weapon_in_the_same_book_class_as_a_throwing_knife_is_not_cut_off()
    {
        // Ch 8 (p.196) files both the throwing knife and the sling under the same "Missile"
        // weapon class, but Ch 7 (p.171) names only "small hand-propelled weapons such as the
        // throwing knife and the throwing axe" for the cutoff -- the sling and blowgun are
        // "entirely self-propelled" (Ch 3, p.47) and must not be cut off. Fixture: two
        // same-class Missile weapons, distinguished only by the hand-thrown fact, at a distance
        // beyond double their shared base range.
        const int distance = 41;
        const int listedRange = 20;

        var throwingKnife = RangeBandResolver.IsBeyondThrowingCutoff(
            isHandThrownWeapon: true, distance, listedRange, Ruleset);
        var sling = RangeBandResolver.IsBeyondThrowingCutoff(
            isHandThrownWeapon: false, distance, listedRange, Ruleset);

        Assert.True(throwingKnife);
        Assert.False(sling);
    }

    // ---- Targeting equipment: halves range modifiers after a round spent aiming -----------

    [Fact]
    public void Targeting_equipment_halves_the_severity_of_a_medium_range_penalty()
    {
        // Unaimed: Difficult, i.e. x1/2. Aimed: the shortfall from 1 (1/2) is itself halved,
        // giving x3/4 instead -- see RangeBandRuleset.TargetingEquipmentDampeningNumerator.
        var outcome = RangeBandResolver.Resolve(
            RangeBand.Medium, Percent.Of(60), NoOtherModifiers, aimedWithTargetingEquipment: true, Ruleset);

        var composable = Assert.IsType<RangeBandOutcome.Composable>(outcome);
        var multiplier = Assert.IsType<MultiplicativeModifier>(Assert.Single(composable.Modifiers));
        Assert.Equal(3, multiplier.Numerator);
        Assert.Equal(4, multiplier.Denominator);
    }

    [Fact]
    public void Targeting_equipment_halves_the_severity_of_the_long_range_override()
    {
        // Unaimed: 1/5 (a 4/5 shortfall from 1). Aimed: half that shortfall, giving 3/5.
        var outcome = RangeBandResolver.Resolve(
            RangeBand.LongRange, Percent.Of(100), NoOtherModifiers, aimedWithTargetingEquipment: true, Ruleset);

        var exclusive = Assert.IsType<RangeBandOutcome.ExclusiveOverride>(outcome);
        Assert.Equal(Percent.Of(60), exclusive.Chance); // 100 * 3/5
    }

    [Fact]
    public void Point_blank_and_normal_range_are_unaffected_by_targeting_equipment()
    {
        var pointBlank = Assert.IsType<RangeBandOutcome.Composable>(RangeBandResolver.Resolve(
            RangeBand.PointBlank, Percent.Of(50), NoOtherModifiers, aimedWithTargetingEquipment: true, Ruleset));
        var normal = Assert.IsType<RangeBandOutcome.Composable>(RangeBandResolver.Resolve(
            RangeBand.Normal, Percent.Of(50), NoOtherModifiers, aimedWithTargetingEquipment: true, Ruleset));

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
        // any composable band, only the band's own DifficultyModifier/MultiplicativeModifier.
        foreach (var band in Enum.GetValues<RangeBand>())
        {
            var outcome = RangeBandResolver.Resolve(
                band, Percent.Of(50), NoOtherModifiers, aimedWithTargetingEquipment: false, Ruleset);

            if (outcome is RangeBandOutcome.Composable composable)
            {
                Assert.DoesNotContain(composable.Modifiers, m => m is AdditiveModifier);
            }
        }
    }
}
