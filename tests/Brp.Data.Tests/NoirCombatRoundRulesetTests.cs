using Brp.Rules.Combat;

namespace Brp.Data.Tests;

/// <summary>
/// Confirms the shipped combat-round data loads and carries the values printed in Ch 6: Combat,
/// "Combat Round Phases" (p.142), "Action" (p.143), and "Move"/"Noncombat Action"/"Attack"
/// (p.144). See <c>docs/decisions/0015-combat-round.md</c>.
/// </summary>
public class NoirCombatRoundRulesetTests
{
    [Fact]
    public void The_shipped_ruleset_has_three_phases_with_powers_omitted()
    {
        var ruleset = NoirCombatRoundRuleset.Load();

        Assert.Equal(
            [CombatRoundPhase.Statements, CombatRoundPhase.Action, CombatRoundPhase.Resolution],
            ruleset.Phases);
    }

    [Fact]
    public void The_shipped_ruleset_orders_DEX_rank_descending()
    {
        var ruleset = NoirCombatRoundRuleset.Load();

        Assert.True(ruleset.DexRankOrderedDescending);
    }

    [Fact]
    public void The_shipped_ruleset_has_the_four_tier_weapon_type_tiebreak_order()
    {
        var ruleset = NoirCombatRoundRuleset.Load();

        Assert.Equal(
            [WeaponTypeTier.Missile, WeaponTypeTier.LongWeapon, WeaponTypeTier.MediumWeapon, WeaponTypeTier.ShortOrUnarmed],
            ruleset.WeaponTypeTiebreakOrder);
    }

    [Fact]
    public void The_shipped_ruleset_has_the_printed_movement_tiers()
    {
        var ruleset = NoirCombatRoundRuleset.Load();

        Assert.Equal(
            [new MovementTier(6, 15, 1, 2), new MovementTier(16, 29, 1, 4)],
            ruleset.MovementTiers);
    }

    [Fact]
    public void The_shipped_ruleset_has_the_printed_penalties_and_floor()
    {
        var ruleset = NoirCombatRoundRuleset.Load();

        Assert.Equal(5, ruleset.DrawWeaponDexRankPenalty);
        Assert.Equal(5, ruleset.MultipleActionDexRankPenalty);
        Assert.Equal(0, ruleset.DexRankFloor);
    }

    [Fact]
    public void The_shipped_ruleset_derives_DEX_rank_from_DEX()
    {
        var ruleset = NoirCombatRoundRuleset.Load();

        Assert.Equal("DEX", ruleset.DexRankSourceCharacteristic);
    }
}
