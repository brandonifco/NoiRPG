using Brp.Rules.Combat;

namespace Brp.Rules.Tests.Combat;

/// <summary>
/// Covers <see cref="EffectiveDexRankCalculator"/> against Ch 6: Combat, "Move" and "Attack"
/// (p.144). See <c>docs/decisions/0015-combat-round.md</c>.
/// </summary>
public class EffectiveDexRankCalculatorTests
{
    private static readonly CombatRoundRuleset Ruleset = new(
        phases: [CombatRoundPhase.Statements, CombatRoundPhase.Action, CombatRoundPhase.Resolution],
        dexRankSourceCharacteristic: "DEX",
        dexRankOrderedDescending: true,
        weaponTypeTiebreakOrder: [WeaponTypeTier.Missile, WeaponTypeTier.LongWeapon, WeaponTypeTier.MediumWeapon, WeaponTypeTier.ShortOrUnarmed],
        movementTiers: [new MovementTier(6, 15, 1, 2), new MovementTier(16, 29, 1, 4)],
        drawWeaponDexRankPenalty: 5,
        multipleActionDexRankPenalty: 5,
        dexRankFloor: 0);

    [Theory]
    [InlineData(0, 15)]  // no movement: unmodified
    [InlineData(5, 15)]  // 5m or less folds into an ordinary attack's own allowance: unmodified
    [InlineData(6, 8)]   // lower edge of 6-15m: half of 15, rounded up -- ceil(15/2) = 8
    [InlineData(15, 8)]  // upper edge of 6-15m
    [InlineData(16, 4)]  // lower edge of 16-29m: quarter of 15, rounded up -- ceil(15/4) = 4
    [InlineData(29, 4)]  // upper edge of 16-29m
    [InlineData(30, 15)] // beyond every printed tier: unmodified (book defines no band past 29m)
    public void Movement_fractions_the_DEX_rank_at_the_printed_tiers_rounding_up(int movementMeters, int expected)
    {
        var rank = EffectiveDexRankCalculator.ApplyMovement(15, movementMeters, Ruleset);

        Assert.Equal(expected, rank);
    }

    [Fact]
    public void A_DEX_rank_one_combatant_moving_6_to_15_meters_still_rounds_up_to_rank_one()
    {
        // Ch 5: System, "Characteristic Increases" (p.140) is the book's only explicit rounding
        // convention for a "half of X" calculation -- "(round up)," worked as "1/2 of 13 rounds up
        // to 7." Truncating instead would send a DEX-rank-1 combatant moving 6-15m to rank 0 and
        // lose their action to an ordinary walk, which Ch 6 never names as a consequence of
        // movement (only of penalties stacking below the floor, p.144). Rounding up keeps rank 1.
        var rank = EffectiveDexRankCalculator.ApplyMovement(1, movementMeters: 10, Ruleset);

        Assert.Equal(1, rank);
    }

    [Fact]
    public void Movement_is_applied_before_a_flat_penalty()
    {
        // Ch 6, p.144: movement modifiers apply first. DEX 20, moving 10m (half -> 10), then a
        // flat -5 (e.g. drawing a weapon combined with an attack) -> 5. Applying the penalty
        // before halving would instead give (20-5)/2 = 7, a different, wrong result.
        var effective = EffectiveDexRankCalculator.Compute(20, 10, 5, Ruleset);

        Assert.Equal(5, effective);
    }

    [Fact]
    public void Drawing_a_weapon_combined_with_another_action_costs_five_DEX_ranks()
    {
        var effective = EffectiveDexRankCalculator.Compute(15, 0, Ruleset.DrawWeaponDexRankPenalty, Ruleset);

        Assert.Equal(10, effective);
    }

    [Theory]
    [InlineData(1, 0, null)]  // 1 - 5 = -4, below the floor: lost
    [InlineData(5, 0, null)]  // exactly at the floor (0): lost
    [InlineData(6, 0, 1)]     // just above the floor: survives at rank 1
    public void An_action_at_or_below_the_DEX_rank_floor_is_lost(int baseDexRank, int movementMeters, int? expected)
    {
        var effective = EffectiveDexRankCalculator.Compute(baseDexRank, movementMeters, flatDexRankPenalty: 5, Ruleset);

        Assert.Equal(expected, effective);
    }
}
