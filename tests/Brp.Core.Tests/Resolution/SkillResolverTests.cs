using Brp.Core.Primitives;
using Brp.Core.Randomness;
using Brp.Core.Resolution;

namespace Brp.Core.Tests.Resolution;

public class SkillResolverTests
{
    [Fact]
    public void SuccessLevel_is_ordered_worst_to_best()
    {
        // Ch 5, "Evaluating Success or Failure": "Ranked from worst to best, they are as
        // follows: Fumble, Failure, Success, Special Success, Critical Success." Opposed
        // rolls (a later Issue) decide their winner by comparing two grades, so this order
        // has to hold under ordinary comparison, not just be a coincidence of declaration.
        Assert.True(SuccessLevel.Fumble < SuccessLevel.Failure);
        Assert.True(SuccessLevel.Failure < SuccessLevel.Success);
        Assert.True(SuccessLevel.Success < SuccessLevel.Special);
        Assert.True(SuccessLevel.Special < SuccessLevel.Critical);
    }

    [Fact]
    public void A_roll_within_both_the_critical_and_special_range_resolves_as_critical_only()
    {
        // Skill Results Table, introductory note: "Whenever a roll result is in the range of
        // both a critical and special success, the results of the critical success (if
        // appropriate) should be applied, not both." At chance 50, critical = ceil(50/20) = 3
        // and special = ceil(50/5) = 10, so a roll of 3 is in both ranges.
        var outcome = SkillResolver.Resolve(Percent.Of(50), Percent.Of(50), roll: 3);

        Assert.Equal(SuccessLevel.Critical, outcome.Level);
        Assert.NotEqual(SuccessLevel.Special, outcome.Level);
    }

    [Theory]
    [InlineData(96)]
    [InlineData(97)]
    [InlineData(98)]
    [InlineData(99)]
    public void Rolls_of_96_and_above_never_succeed_no_matter_how_high_the_chance(int roll)
    {
        // Ch 5, "Failure": "no matter how high the modified base chance, rolls fail on
        // results of 96 or higher." Using a chance far above 100% to make sure nothing about
        // "ratings above 100% are supported" is read as overriding this cap.
        var outcome = SkillResolver.Resolve(Percent.Of(500), Percent.Of(500), roll);

        Assert.True(outcome.Level is SuccessLevel.Failure or SuccessLevel.Fumble);
    }

    [Fact]
    public void A_roll_of_100_always_fumbles_regardless_of_chance()
    {
        // Ch 5, "Fumble": "A roll of 00 is always a fumble, no matter what the skill rating
        // is." 00 is represented as 100 (see IEntropySource.NextD100). Chance of 500% is used
        // to show this holds even for a rating nowhere near the printed table's range.
        var outcome = SkillResolver.Resolve(Percent.Of(500), Percent.Of(500), roll: 100);

        Assert.Equal(SuccessLevel.Fumble, outcome.Level);
    }

    [Fact]
    public void A_roll_of_100_fumbles_even_at_a_zero_chance()
    {
        var outcome = SkillResolver.Resolve(Percent.Of(0), Percent.Of(0), roll: 100);

        Assert.Equal(SuccessLevel.Fumble, outcome.Level);
    }

    [Fact]
    public void The_5_percent_floor_rescues_a_roll_of_01_to_05_when_the_base_chance_qualifies()
    {
        // Ch 5, "Skill Rolls": "Any skill which normally has a base chance of 5% or higher
        // always succeeds on a roll of 01-05... even if difficulty, conditional modifiers, or
        // other factors reduce the skill rating below 5%." Base chance 20, modified down to 1
        // by (unmodeled) penalties; a roll of 4 would ordinarily fail against a chance of 1.
        var outcome = SkillResolver.Resolve(Percent.Of(20), Percent.Of(1), roll: 4);

        Assert.Equal(SuccessLevel.Success, outcome.Level);
    }

    [Fact]
    public void The_5_percent_floor_is_keyed_on_the_base_chance_not_the_effective_one()
    {
        // The rule reads "a base chance of 5% or higher", not "an effective chance of 5% or
        // higher" -- this is the one behavior that would look identical to the ordinary
        // success check if the floor were (wrongly) keyed on the effective chance instead.
        // Here the *base* chance is below 5%, so even though the roll is in 01-05, the floor
        // must not apply, and an effective chance of 1 leaves roll 4 an ordinary failure.
        var outcome = SkillResolver.Resolve(Percent.Of(4), Percent.Of(1), roll: 4);

        Assert.Equal(SuccessLevel.Failure, outcome.Level);
    }

    [Fact]
    public void The_5_percent_floor_does_not_apply_outside_the_01_to_05_roll_range()
    {
        // Base chance qualifies (10), but the floor only ever covers rolls 01-05.
        var outcome = SkillResolver.Resolve(Percent.Of(10), Percent.Of(1), roll: 6);

        Assert.Equal(SuccessLevel.Failure, outcome.Level);
    }

    [Fact]
    public void The_5_percent_floor_never_upgrades_a_roll_past_plain_success()
    {
        // The book says the roll "always succeeds", not that it upgrades to Special or
        // Critical. Effective chance 1 gives critical = special = 1 (ceil(1/20) = ceil(1/5) =
        // 1), so roll 1 would already be Critical without any floor involved; roll 3 is past
        // both thresholds and should land on plain Success via the floor, not better.
        var outcome = SkillResolver.Resolve(Percent.Of(20), Percent.Of(1), roll: 3);

        Assert.Equal(SuccessLevel.Success, outcome.Level);
    }

    [Theory]
    [InlineData(150, 5, SuccessLevel.Critical)] // critical = ceil(150/20) = 8, so roll 5 qualifies
    [InlineData(150, 20, SuccessLevel.Special)] // special = ceil(150/5) = 30, so roll 20 qualifies
    [InlineData(150, 95, SuccessLevel.Success)] // below the always-fails 96 line, above special's 30
    public void Ratings_above_100_percent_are_supported_natively(int chance, int roll, SuccessLevel expected)
    {
        // Ch 5, Skill Results Table: printed rows continue past 100% (101-105 ... 116-120,
        // "Each +5 | Etc. | Etc. | 00"), so chances above 100 are a normal state, not an error.
        var outcome = SkillResolver.Resolve(Percent.Of(chance), Percent.Of(chance), roll);

        Assert.Equal(expected, outcome.Level);
    }

    [Fact]
    public void RollOutcome_exposes_every_input_that_produced_the_grade()
    {
        var outcome = SkillResolver.Resolve(Percent.Of(45), Percent.Of(40), roll: 7);

        Assert.Equal(7, outcome.Roll);
        Assert.Equal(Percent.Of(45), outcome.BaseChance);
        Assert.Equal(Percent.Of(40), outcome.EffectiveChance);
        Assert.Equal(Percent.Of(2), outcome.CriticalThreshold); // ceil(40/20)
        Assert.Equal(Percent.Of(8), outcome.SpecialThreshold); // ceil(40/5)
        Assert.Equal(Percent.Of(97), outcome.FumbleThreshold); // row 36-40
        Assert.Equal(SuccessLevel.Special, outcome.Level); // 2 < 7 <= 8
    }

    [Theory]
    [InlineData(SuccessLevel.Fumble, false)]
    [InlineData(SuccessLevel.Failure, false)]
    [InlineData(SuccessLevel.Success, true)]
    [InlineData(SuccessLevel.Special, true)]
    [InlineData(SuccessLevel.Critical, true)]
    public void Succeeded_is_true_for_success_or_better(SuccessLevel level, bool expected)
    {
        var outcome = new RollOutcome(
            Roll: 1,
            BaseChance: Percent.Zero,
            EffectiveChance: Percent.Zero,
            CriticalThreshold: Percent.Zero,
            SpecialThreshold: Percent.Zero,
            FumbleThreshold: Percent.Of(100),
            Level: level);

        Assert.Equal(expected, outcome.Succeeded);
    }

    [Fact]
    public void Resolve_with_an_entropy_source_draws_exactly_one_D100()
    {
        var entropy = new Xoshiro256StarStar(seed: 42);
        var before = entropy.DrawCount;

        SkillResolver.Resolve(Percent.Of(50), Percent.Of(50), entropy);

        Assert.Equal(before + 1, entropy.DrawCount);
    }

    [Fact]
    public void Resolve_rejects_a_null_entropy_source()
    {
        Assert.Throws<ArgumentNullException>(
            () => SkillResolver.Resolve(Percent.Of(50), Percent.Of(50), (IEntropySource)null!));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public void Resolve_rejects_a_roll_outside_1_to_100(int roll)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => SkillResolver.Resolve(Percent.Of(50), Percent.Of(50), roll));
    }
}
