namespace Brp.Data.Tests;

/// <summary>
/// Confirms the shipped special-damage-effects ruleset data loads and carries the printed values
/// cited in Ch 6, pp.149-151. See #113 and <c>docs/decisions/0017-damage.md</c> (the deferral).
/// </summary>
public class NoirSpecialDamageEffectsRulesetTests
{
    [Fact]
    public void Load_returns_the_printed_crushing_stun_duration_dice()
    {
        // Ch 6, p.149: "be stunned for 1D3 rounds."
        var ruleset = NoirSpecialDamageEffectsRuleset.Load();

        Assert.Equal("1D3", ruleset.CrushingStunDuration.Notation);
    }

    [Fact]
    public void Load_returns_the_printed_impaling_self_extraction_failure_damage_dice()
    {
        // Ch 6, p.150: "an additional 1D3 hit points of damage."
        var ruleset = NoirSpecialDamageEffectsRuleset.Load();

        Assert.Equal("1D3", ruleset.ImpalingSelfExtractionFailureExtraDamage.Notation);
    }

    [Fact]
    public void Load_returns_the_printed_knockback_values()
    {
        // Ch 6, p.151: "one meter for every 5 points of damage" and "1D6 damage for every three
        // meters or fraction thereof."
        var ruleset = NoirSpecialDamageEffectsRuleset.Load();

        Assert.Equal(5, ruleset.KnockbackMetersPerDamagePoint);
        Assert.Equal("1D6", ruleset.KnockbackObstacleDamagePerIncrement.Notation);
        Assert.Equal(3, ruleset.KnockbackObstacleIncrementMeters);
    }

    [Fact]
    public void Load_returns_the_printed_bleeding_values()
    {
        // Ch 6, p.149: 1 HP/round, 1 fatigue point/round, stops on its own after 5 rounds staunched.
        var ruleset = NoirSpecialDamageEffectsRuleset.Load();

        Assert.Equal(1, ruleset.BleedingHitPointLossPerRound);
        Assert.Equal(1, ruleset.BleedingFatiguePointLossPerRound);
        Assert.Equal(5, ruleset.BleedingStaunchedRoundsUntilPermanentStop);
    }

    [Fact]
    public void Load_returns_the_printed_entangling_immobilization_window()
    {
        // Ch 6, pp.150-151: rest of the current round, plus the following round.
        var ruleset = NoirSpecialDamageEffectsRuleset.Load();

        Assert.True(ruleset.EntanglingImmobilizesRemainderOfCurrentRound);
        Assert.Equal(1, ruleset.EntanglingImmobilizedFollowingRounds);
    }

    [Fact]
    public void Load_returns_the_printed_successive_defense_penalty()
    {
        // Ch 6, p.144/p.151: "a cumulative -30% modifier."
        var ruleset = NoirSpecialDamageEffectsRuleset.Load();

        Assert.Equal(30, ruleset.SuccessiveDefensePenaltyPercent);
    }
}
