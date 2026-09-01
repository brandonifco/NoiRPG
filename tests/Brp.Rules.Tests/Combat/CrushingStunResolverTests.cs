using Brp.Core.Abilities;
using Brp.Core.Resolution;
using Brp.Data;
using Brp.Rules.Combat;

namespace Brp.Rules.Tests.Combat;

/// <summary>
/// Ch 6: Combat, "Crushing" (pp.149-150)'s stunning effect -- deferred from #52
/// (<c>docs/decisions/0017-damage.md</c>) and built here for #113.
/// </summary>
public class CrushingStunResolverTests
{
    private static readonly SpecialDamageEffectsRuleset Ruleset = NoirSpecialDamageEffectsRuleset.Load();

    private static AbilitySet MakeTarget(int con)
    {
        var abilityRuleset = NoirAbilityRuleset.Load();
        var values = abilityRuleset.Characteristics.Keys.ToDictionary(id => id, _ => 10);
        values[new CharacteristicId("CON")] = con;
        return new AbilitySet(abilityRuleset, values);
    }

    [Fact]
    public void A_failed_stamina_roll_stuns_for_the_rolled_1D3_duration_page_149()
    {
        // CON 10 => Stamina (CON x5) = 50; roll 90 fails => stunned; duration roll 2.
        var outcome = CrushingStunResolver.ResolveStun(MakeTarget(con: 10), Ruleset, new FixedEntropySource(90, 2));

        Assert.False(outcome.StaminaRoll.Succeeded);
        Assert.True(outcome.Stunned);
        Assert.Equal(2, outcome.DurationRounds);
        Assert.NotNull(outcome.DurationRoll);
    }

    [Fact]
    public void A_successful_stamina_roll_avoids_the_stun_page_149()
    {
        // CON 10 => 50; roll 10 succeeds => not stunned, no duration roll consumed.
        var outcome = CrushingStunResolver.ResolveStun(MakeTarget(con: 10), Ruleset, new FixedEntropySource(10));

        Assert.True(outcome.StaminaRoll.Succeeded);
        Assert.False(outcome.Stunned);
        Assert.Null(outcome.DurationRounds);
        Assert.Null(outcome.DurationRoll);
    }

    [Fact]
    public void The_printed_stunned_target_penalties_are_all_present_pages_149_150()
    {
        var penalties = CrushingStunResolver.Penalties;

        Assert.True(penalties.CannotAttack);
        Assert.True(penalties.DefenseRequiresSuccessfulIdeaRoll);
        Assert.True(penalties.AttacksAgainstTargetAreEasy);
        Assert.True(penalties.FleeingRequiresIdeaThenAgilityRoll);
    }

    [Fact]
    public void The_idea_roll_reads_intelligence_and_the_agility_roll_reads_dexterity_page_150()
    {
        var target = MakeTarget(con: 10);

        var ideaRoll = CrushingStunResolver.IdeaRollFor(target);
        var agilityRoll = CrushingStunResolver.AgilityRollFor(target);

        Assert.Equal(new CharacteristicId("INT"), ideaRoll.Characteristic);
        Assert.Equal(new CharacteristicId("DEX"), agilityRoll.Characteristic);
    }
}
