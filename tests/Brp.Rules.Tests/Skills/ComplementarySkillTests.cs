using Brp.Core.Modifiers;
using Brp.Core.Primitives;
using Brp.Rules.Skills;

namespace Brp.Rules.Tests.Skills;

/// <summary>
/// Ch 3: Skills, "Augments and Complementary skills" (p.34) (Issue #114): the static +1/5
/// complementary-skill bonus. See <c>docs/decisions/NNNN-complementary-skills-and-augments.md</c>
/// for the sourced/house-rule breakdown.
/// </summary>
public class ComplementarySkillTests
{
    private static readonly ComplementarySkillRuleset Ruleset = new(bonusNumerator: 1, bonusDenominator: 5);

    [Fact]
    public void Reproduces_the_books_own_worked_example_page_34()
    {
        // "your character has a Medicine skill of 65% and a Science (Pharmacy) of 40%... they
        // can add 8% (1/5 of their Science (Pharmacy) rating) to the Medicine skill rating, for
        // a modified rating of 73%."
        var modifier = ComplementarySkill.Bonus("Science (Pharmacy)", helperRating: 40, Ruleset);

        Assert.Equal(8, modifier.Delta);
        Assert.Equal(AdditiveKind.Permanent, modifier.Kind);

        var medicine = Percent.Of(65);
        var chain = ModifierPipeline.Evaluate(medicine, [modifier]);
        Assert.Equal(73, chain.EffectiveChance!.Value.Value);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 0)] // rounds down to 0 -- the fraction never reaches a whole point.
    [InlineData(4, 0)]
    [InlineData(5, 1)]
    [InlineData(9, 1)] // rounds down, not to the nearest -- 9/5 = 1.8 -> 1, not 2.
    [InlineData(24, 4)]
    [InlineData(25, 5)]
    public void Bonus_rounds_down_a_remainder_house_rule(int helperRating, int expectedBonus)
    {
        // House choice: the book prints no rounding rule for the general case (only the
        // evenly-divisible worked example above). Rounds down, matching the identical 1/5
        // Science (Pharmacy) fraction already implemented for First Aid
        // (docs/decisions/0023-healing-and-recovery.md), so the two occurrences of "1/5 of a
        // skill rating" in this engine agree.
        var modifier = ComplementarySkill.Bonus("Helper", helperRating, Ruleset);
        Assert.Equal(expectedBonus, modifier.Delta);
    }

    [Fact]
    public void Bonus_is_applied_before_the_difficulty_multiplier_permanent()
    {
        // Ch 5, "Modifying Action Rolls" / ADR 0007: a modifier integral to the rating itself
        // (Permanent) is figured in before a Difficult/Easy grade doubles or halves the chance --
        // the complementary bonus is part of what the character brings to the roll, not a
        // situational condition, so it must land in that stage.
        var modifier = ComplementarySkill.Bonus("Helper", helperRating: 40, Ruleset);
        var chain = ModifierPipeline.Evaluate(
            Percent.Of(65), [modifier, DifficultyModifier.Difficult("test")]);

        // (65 + 8) / 2, rounded up per the standard policy -> 37, not 65/2 + 8 = 41.
        Assert.Equal(37, chain.EffectiveChance!.Value.Value);
    }

    [Fact]
    public void Zero_helper_rating_produces_a_zero_delta_modifier_not_a_null()
    {
        var modifier = ComplementarySkill.Bonus("Helper", helperRating: 0, Ruleset);
        Assert.Equal(0, modifier.Delta);
    }
}
