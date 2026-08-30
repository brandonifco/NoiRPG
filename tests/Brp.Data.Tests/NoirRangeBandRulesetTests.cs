using Brp.Rules.Gear;

namespace Brp.Data.Tests;

/// <summary>
/// Confirms the shipped range-band data loads and carries the values printed in Ch 6: Combat,
/// "Missile Weapons" (p.153-154) and Ch 7: Spot Rules, "Extended Range" (p.170).
/// </summary>
public class NoirRangeBandRulesetTests
{
    [Fact]
    public void The_shipped_ruleset_matches_the_printed_thresholds()
    {
        var ruleset = NoirRangeBandRuleset.Load();

        Assert.Equal(3, ruleset.PointBlankDexDivisor);
        Assert.Equal(2, ruleset.MediumRangeMultiplier);
        Assert.Equal(4, ruleset.LongRangeMultiplier);
        Assert.Equal(1, ruleset.LongRangeChanceNumerator);
        Assert.Equal(5, ruleset.LongRangeChanceDenominator);
        Assert.Equal(2, ruleset.ThrowingCutoffMultiplier);
        Assert.Equal([WeaponClass.Missile], ruleset.ThrowingWeaponClasses);
        Assert.Equal(1, ruleset.TargetingEquipmentDampeningNumerator);
        Assert.Equal(2, ruleset.TargetingEquipmentDampeningDenominator);
    }
}
