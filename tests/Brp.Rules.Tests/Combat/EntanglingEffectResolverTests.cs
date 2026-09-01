using Brp.Core.Abilities;
using Brp.Core.Resolution;
using Brp.Data;
using Brp.Rules.Combat;

namespace Brp.Rules.Tests.Combat;

/// <summary>
/// Ch 6: Combat, "Entangling" (pp.150-151) -- deferred from #52
/// (<c>docs/decisions/0017-damage.md</c>) and built here for #113. Dormant in the shipped weapon
/// subset (ADR 0017).
/// </summary>
public class EntanglingEffectResolverTests
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
    public void Immobilization_covers_the_rest_of_the_round_and_the_next_pages_150_151()
    {
        var immobilization = EntanglingEffectResolver.Immobilize(Ruleset);

        Assert.True(immobilization.RemainderOfCurrentRound);
        Assert.Equal(1, immobilization.FollowingRoundsImmobilized);
    }

    [Fact]
    public void A_successful_agility_roll_frees_the_target_page_151()
    {
        var outcome = EntanglingEffectResolver.AttemptAgilityEscape(MakeTarget(dex: 10), new FixedEntropySource(10));

        Assert.True(outcome.Succeeded);
    }

    [Fact]
    public void A_won_str_vs_str_roll_pulls_the_weapon_free_page_151()
    {
        // STR 15 target vs STR 5 attacker: chance 50+5*(15-5)=100; roll 50 succeeds.
        var outcome = EntanglingEffectResolver.AttemptStrengthEscape(
            targetStrength: 15, attackerStrength: 5, new FixedEntropySource(50));

        Assert.True(outcome.Succeeded);
    }

    [Theory]
    [InlineData(SuccessLevel.Critical, SuccessLevel.Critical, true, true)]
    [InlineData(SuccessLevel.Special, SuccessLevel.Critical, true, false)]
    [InlineData(SuccessLevel.Critical, SuccessLevel.Success, true, false)]
    public void A_parry_only_negates_a_critical_entangle_with_a_critical_parry_page_151(
        SuccessLevel entangleGrade, SuccessLevel counterGrade, bool counterIsParry, bool expectedNegated)
    {
        Assert.Equal(
            expectedNegated,
            EntanglingEffectResolver.NegatesEntangle(entangleGrade, counterGrade, counterIsParry));
    }

    [Theory]
    [InlineData(SuccessLevel.Success, false, true)]
    [InlineData(SuccessLevel.Special, false, true)]
    [InlineData(SuccessLevel.Critical, false, true)]
    [InlineData(SuccessLevel.Failure, false, false)]
    [InlineData(SuccessLevel.Fumble, false, false)]
    public void A_dodge_or_wrestle_success_or_better_negates_any_entangle_grade_page_151(
        SuccessLevel counterGrade, bool counterIsParry, bool expectedNegated)
    {
        Assert.Equal(
            expectedNegated,
            EntanglingEffectResolver.NegatesEntangle(SuccessLevel.Special, counterGrade, counterIsParry));
    }

    [Fact]
    public void The_allowable_grapple_follow_up_effects_match_the_printed_list_page_151()
    {
        Assert.Equal(
            new[]
            {
                GrappleFollowUpEffect.ImmobilizeLimb,
                GrappleFollowUpEffect.ImmobilizeTarget,
                GrappleFollowUpEffect.ThrowTarget,
                GrappleFollowUpEffect.KnockdownTarget,
                GrappleFollowUpEffect.DisarmTarget,
                GrappleFollowUpEffect.InjureTarget,
                GrappleFollowUpEffect.StrangleTarget,
            },
            EntanglingEffectResolver.AllowableFollowUpEffects);
    }
}
