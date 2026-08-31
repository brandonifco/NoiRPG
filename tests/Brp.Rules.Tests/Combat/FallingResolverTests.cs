using Brp.Core.Abilities;
using Brp.Core.Contests;
using Brp.Data;
using Brp.Rules.Characters;
using Brp.Rules.Combat;

namespace Brp.Rules.Tests.Combat;

/// <summary>
/// Ch 7: Spot Rules, "Falling" (p.171). Deterministic falling damage by distance, force, and SIZ,
/// then armor/surface mitigation, applied through the non-weapon <see cref="DamageResolver"/>
/// overload. See <c>docs/decisions/0019-injury-spot-rules.md</c>.
/// </summary>
public class FallingResolverTests
{
    private static readonly FallingRuleset Falling = NoirInjuryRuleset.Load().Falling;
    private static readonly DamageRuleset Damage = NoirDamageRuleset.Load();

    private static AbilitySet MakeTarget(int con, int siz)
    {
        var ruleset = NoirAbilityRuleset.Load();
        var values = ruleset.Characteristics.Keys.ToDictionary(id => id, _ => 12);
        values[new CharacteristicId("CON")] = con;
        values[new CharacteristicId("SIZ")] = siz;
        return new AbilitySet(ruleset, values);
    }

    [Fact]
    public void Base_damage_is_one_die_per_three_meters_page_171()
    {
        // 9 m => 3 base dice; SIZ 12 is neither small nor large; no force.
        var entropy = new FixedEntropySource(4, 5, 6);

        var roll = FallingResolver.RollFallingDamage(metersFallen: 9, size: 12, thrownWithForce: false, Falling, entropy);

        Assert.Equal(3, roll.BaseDiceCount);
        Assert.Equal(15, roll.DamageBeforeMitigation);
        Assert.Equal(0, roll.LargeSizeBands);
        Assert.False(roll.SmallSizeReductionApplied);
    }

    [Fact]
    public void Thrown_with_considerable_force_doubles_the_dice_page_171()
    {
        // 6 m => 2 base dice, doubled to 4 by force.
        var entropy = new FixedEntropySource(1, 1, 1, 1);

        var roll = FallingResolver.RollFallingDamage(metersFallen: 6, size: 12, thrownWithForce: true, Falling, entropy);

        Assert.Equal(4, roll.BaseDiceCount);
        Assert.Equal(4, roll.DamageBeforeMitigation);
        Assert.True(roll.ThrownWithForce);
    }

    [Fact]
    public void Small_size_subtracts_one_die_page_171()
    {
        // 9 m => 3 base dice (2+2+2=6); SIZ 5 subtracts a 1D6 (5) => 1.
        var entropy = new FixedEntropySource(2, 2, 2, 5);

        var roll = FallingResolver.RollFallingDamage(metersFallen: 9, size: 5, thrownWithForce: false, Falling, entropy);

        Assert.True(roll.SmallSizeReductionApplied);
        Assert.Equal(1, roll.DamageBeforeMitigation);
    }

    [Fact]
    public void Small_size_reduction_floors_the_damage_at_zero()
    {
        // 3 m => 1 base die (2); SIZ 5 subtracts a 1D6 (6) => -4, floored to 0.
        var entropy = new FixedEntropySource(2, 6);

        var roll = FallingResolver.RollFallingDamage(metersFallen: 3, size: 5, thrownWithForce: false, Falling, entropy);

        Assert.Equal(0, roll.DamageBeforeMitigation);
    }

    [Theory]
    [InlineData(21, 1)]
    [InlineData(40, 1)]
    [InlineData(41, 2)]
    [InlineData(60, 2)]
    [InlineData(61, 3)]
    public void Large_size_adds_one_die_per_started_band_of_twenty_page_171(int size, int expectedBands)
    {
        // 3 m => 1 base die (fixed at 1), then expectedBands large dice (each fixed at 1).
        var faces = Enumerable.Repeat(1, 1 + expectedBands).ToArray();
        var entropy = new FixedEntropySource(faces);

        var roll = FallingResolver.RollFallingDamage(metersFallen: 3, size, thrownWithForce: false, Falling, entropy);

        Assert.Equal(expectedBands, roll.LargeSizeBands);
        Assert.Equal(1 + expectedBands, roll.DamageBeforeMitigation);
    }

    [Fact]
    public void Large_size_is_cumulative_with_the_force_doubling_page_171()
    {
        // 6 m => 2 base dice doubled to 4 by force (all 1s = 4), plus one large band (6) for SIZ 21.
        var entropy = new FixedEntropySource(1, 1, 1, 1, 6);

        var roll = FallingResolver.RollFallingDamage(metersFallen: 6, size: 21, thrownWithForce: true, Falling, entropy);

        Assert.Equal(4, roll.BaseDiceCount);
        Assert.Equal(1, roll.LargeSizeBands);
        Assert.Equal(10, roll.DamageBeforeMitigation);
    }

    [Fact]
    public void Armor_gives_half_protection_up_to_three_meters_page_171()
    {
        var roll = new FallingDamageRoll(DamageBeforeMitigation: 6, BaseDiceCount: 1, LargeSizeBands: 0, false, false);

        // armor 4 => half is 2; 6 - 2 = 4.
        var final = FallingResolver.MitigateFallingDamage(
            roll, armorValue: 4, metersFallen: 3, new FallingSurfaceRuling(0), Falling);

        Assert.Equal(4, final);
    }

    [Fact]
    public void Armor_does_not_apply_beyond_three_meters()
    {
        var roll = new FallingDamageRoll(DamageBeforeMitigation: 6, BaseDiceCount: 2, LargeSizeBands: 0, false, false);

        var final = FallingResolver.MitigateFallingDamage(
            roll, armorValue: 10, metersFallen: 6, new FallingSurfaceRuling(0), Falling);

        Assert.Equal(6, final);
    }

    [Theory]
    [InlineData(-10, 0)]
    [InlineData(2, 8)]
    public void The_falling_surface_ruling_adjusts_the_final_damage_and_floors_at_zero(int adjustment, int expected)
    {
        var roll = new FallingDamageRoll(DamageBeforeMitigation: 6, BaseDiceCount: 2, LargeSizeBands: 0, false, false);

        var final = FallingResolver.MitigateFallingDamage(
            roll, armorValue: 0, metersFallen: 6, new FallingSurfaceRuling(adjustment), Falling);

        Assert.Equal(expected, final);
    }

    [Fact]
    public void Falling_damage_flows_through_the_hit_point_path_and_records_a_wound()
    {
        var target = MakeTarget(con: 14, siz: 12);
        var startingHitPoints = target.CurrentHitPoints;
        var wounds = new WoundTrack();
        var entropy = new FixedEntropySource(5, 5, 5); // 9 m => 3 dice = 15.

        var roll = FallingResolver.RollFallingDamage(metersFallen: 9, size: 12, thrownWithForce: false, Falling, entropy);
        var final = FallingResolver.MitigateFallingDamage(roll, armorValue: 0, metersFallen: 9, new FallingSurfaceRuling(0), Falling);
        var result = DamageResolver.ApplyDamage(target, wounds, final, Damage, "Falling: 9 m");

        Assert.Equal(15, final);
        Assert.Equal(15, result.DamageDealt);
        Assert.Equal(startingHitPoints - 15, target.CurrentHitPoints);
        Assert.Single(wounds.Wounds);
    }
}
