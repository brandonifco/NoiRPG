using Brp.Core.Modifiers;
using Brp.Data;
using Brp.Rules.Combat;

namespace Brp.Rules.Tests.Combat;

/// <summary>
/// Ch 6: Combat, "Fighting Defensively" (p.151) -- an in-scope Ch 6 core mechanic never built
/// before #113. Also exercises the successive Dodge/parry -30% cumulative penalty this piece
/// gives its first implementation (Ch 6, "Parry"/"Dodge", p.144).
/// </summary>
public class FightingDefensivelyResolverTests
{
    private static readonly SpecialDamageEffectsRuleset Ruleset = NoirSpecialDamageEffectsRuleset.Load();

    [Fact]
    public void Declaring_fighting_defensively_forfeits_all_attacks_and_grants_one_free_dodge_page_151()
    {
        // p.151: "one free Dodge attempt" -- Dodge-only, never a Parry, regardless of
        // multi-attack capability.
        var declaration = FightingDefensivelyResolver.Declare(canMakeMultipleAttacksPerRound: false);

        Assert.Equal(DefenseType.Dodge, declaration.FirstFreeDefenseType);
        Assert.False(declaration.SecondFreeDefenseAvailable);
        Assert.Empty(declaration.SecondFreeDefenseAllowedTypes);
        Assert.True(declaration.ForfeitsAllAttacksThisRound);
        Assert.True(declaration.CannotCombineWithAnyOffensiveAction);
        Assert.True(declaration.CannotDodgeAndParryWithinTheSameDexRank);
    }

    [Fact]
    public void Multi_attack_capability_grants_a_second_free_dodge_or_parry_page_151()
    {
        // p.151: "If your character can normally make multiple attacks per round ..., they can
        // make a second free Dodge or parry."
        var declaration = FightingDefensivelyResolver.Declare(canMakeMultipleAttacksPerRound: true);

        Assert.Equal(DefenseType.Dodge, declaration.FirstFreeDefenseType);
        Assert.True(declaration.SecondFreeDefenseAvailable);
        Assert.Equal([DefenseType.Dodge, DefenseType.Parry], declaration.SecondFreeDefenseAllowedTypes);
    }

    [Fact]
    public void The_free_defense_count_is_capped_at_two_regardless_of_attacks_forgone_page_151()
    {
        // Forgoing three (or any number of) attacks by having multiple actions never yields a
        // third free defense -- the second is gated on multi-attack capability, not on the count
        // of attacks forgone, and there is no parameter to inflate the count past two.
        var declaration = FightingDefensivelyResolver.Declare(canMakeMultipleAttacksPerRound: true);

        var freeDefenseCount = 1 + (declaration.SecondFreeDefenseAvailable ? 1 : 0);
        Assert.Equal(2, freeDefenseCount);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, -30)]
    [InlineData(2, -60)]
    [InlineData(3, -90)]
    public void The_successive_defense_penalty_is_cumulative_at_30_percent_pages_144_151(
        int countedPriorAttempts, int expectedPercent)
    {
        Assert.Equal(
            expectedPercent, FightingDefensivelyResolver.SuccessiveDefensePenaltyPercent(countedPriorAttempts, Ruleset));
    }

    [Fact]
    public void A_free_fighting_defensively_attempt_is_not_counted_toward_the_penalty_page_151()
    {
        // A free attempt is simply never passed into countedPriorAttempts -- the next COUNTED
        // attempt after one free (and zero counted) attempts still carries no penalty.
        var penaltyAfterOneFreeAttempt = FightingDefensivelyResolver.SuccessiveDefensePenaltyPercent(0, Ruleset);

        Assert.Equal(0, penaltyAfterOneFreeAttempt);
    }

    [Fact]
    public void No_penalty_modifier_is_built_for_the_first_counted_attempt()
    {
        var modifier = FightingDefensivelyResolver.SuccessiveDefensePenaltyModifier(0, Ruleset, "test");

        Assert.Null(modifier);
    }

    [Fact]
    public void A_penalty_modifier_is_built_for_a_later_counted_attempt()
    {
        var modifier = FightingDefensivelyResolver.SuccessiveDefensePenaltyModifier(1, Ruleset, "test");

        var additive = Assert.IsType<AdditiveModifier>(modifier);
        Assert.Equal(-30, additive.Delta);
    }
}
