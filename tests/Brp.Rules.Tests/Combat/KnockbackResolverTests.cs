using Brp.Core.Abilities;
using Brp.Data;
using Brp.Rules.Combat;

namespace Brp.Rules.Tests.Combat;

/// <summary>
/// Ch 6: Combat, "Knockback" (p.151) -- deferred from #52 (<c>docs/decisions/0017-damage.md</c>)
/// and built here for #113. Dormant in the shipped weapon subset (ADR 0017).
/// </summary>
public class KnockbackResolverTests
{
    private static readonly SpecialDamageEffectsRuleset Ruleset = NoirSpecialDamageEffectsRuleset.Load();

    private static AbilitySet MakeTarget(int dex)
    {
        var abilityRuleset = NoirAbilityRuleset.Load();
        var values = abilityRuleset.Characteristics.Keys.ToDictionary(id => id, _ => 10);
        values[new CharacteristicId("DEX")] = dex;
        return new AbilitySet(abilityRuleset, values);
    }

    [Fact]
    public void A_won_resistance_roll_knocks_back_one_meter_per_5_damage_page_151()
    {
        // damage 13 vs SIZ 12: chance 50+5*(13-12)=55; roll 10 succeeds (damage overcomes SIZ).
        // distance = 13/5 = 2 (floor -- no "or fraction thereof" for this figure).
        // Agility (DEX 10 x5 = 50%); roll 10 succeeds => not knocked prone.
        var outcome = KnockbackResolver.Resolve(
            totalDamageRolled: 13, attackerSiz: 10, effectiveTargetSiz: 12,
            MakeTarget(dex: 10), Ruleset, new FixedEntropySource(10, 10));

        Assert.True(outcome.KnockedBack);
        Assert.Equal(2, outcome.DistanceMeters);
        Assert.Equal(0, outcome.AttackerStaggerMeters);
        Assert.NotNull(outcome.ProneCheck);
        Assert.False(outcome.KnockedProne);
    }

    [Fact]
    public void A_failed_agility_roll_after_knockback_falls_prone_page_151()
    {
        var outcome = KnockbackResolver.Resolve(
            totalDamageRolled: 13, attackerSiz: 10, effectiveTargetSiz: 12,
            MakeTarget(dex: 10), Ruleset, new FixedEntropySource(10, 90));

        Assert.True(outcome.KnockedBack);
        Assert.True(outcome.KnockedProne);
    }

    [Fact]
    public void A_lost_resistance_roll_is_not_moved_page_151()
    {
        // damage 5 vs SIZ 20: chance 50+5*(5-20) = -25 -> floored to 0% -> automatic failure zone,
        // so the roll always fails (SIZ resists) regardless of the scripted die -- "the target
        // wins the resistance roll" -> not moved.
        var outcome = KnockbackResolver.Resolve(
            totalDamageRolled: 5, attackerSiz: 10, effectiveTargetSiz: 20,
            MakeTarget(dex: 10), Ruleset, new FixedEntropySource(50));

        Assert.False(outcome.KnockedBack);
        Assert.Equal(0, outcome.DistanceMeters);
        Assert.Null(outcome.ProneCheck);
    }

    [Fact]
    public void The_attacker_staggers_back_one_meter_when_the_target_wins_with_higher_siz_page_151()
    {
        var outcome = KnockbackResolver.Resolve(
            totalDamageRolled: 5, attackerSiz: 10, effectiveTargetSiz: 20,
            MakeTarget(dex: 10), Ruleset, new FixedEntropySource(50));

        Assert.False(outcome.KnockedBack);
        Assert.Equal(1, outcome.AttackerStaggerMeters);
    }

    [Fact]
    public void No_stagger_when_the_target_wins_but_does_not_have_higher_siz_page_151()
    {
        var outcome = KnockbackResolver.Resolve(
            totalDamageRolled: 5, attackerSiz: 20, effectiveTargetSiz: 20,
            MakeTarget(dex: 10), Ruleset, new FixedEntropySource(50));

        Assert.False(outcome.KnockedBack);
        Assert.Equal(0, outcome.AttackerStaggerMeters);
    }

    [Theory]
    [InlineData(7, 3)] // ceil(7/3) = 3 increments
    [InlineData(6, 2)] // exact multiple, no "fraction thereof" bump
    [InlineData(1, 1)] // any partial meter still costs a full increment
    public void Obstacle_damage_rolls_one_1D6_per_three_meters_or_fraction_thereof_page_151(
        int metersRemaining, int expectedIncrements)
    {
        var faces = Enumerable.Repeat(4, expectedIncrements).ToArray();
        var outcome = KnockbackResolver.RollObstacleDamage(metersRemaining, Ruleset, new FixedEntropySource(faces));

        Assert.Equal(expectedIncrements, outcome.Increments);
        Assert.Equal(expectedIncrements * 4, outcome.TotalDamage);
    }
}
