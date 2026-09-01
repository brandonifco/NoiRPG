using Brp.Core.Abilities;
using Brp.Core.Primitives;
using Brp.Core.Resolution;
using Brp.Data;
using Brp.Rules.Combat;

namespace Brp.Rules.Tests.Combat;

/// <summary>
/// Ch 6: Combat, "Bleeding" (p.149) -- deferred from #52 (<c>docs/decisions/0017-damage.md</c>)
/// and built here for #113. Dormant in the shipped weapon subset (ADR 0017).
/// </summary>
public class BleedingEffectResolverTests
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
    public void The_round_loss_is_1_hit_point_and_1_fatigue_point_page_149()
    {
        var loss = BleedingEffectResolver.RoundLoss(Ruleset);

        Assert.Equal(1, loss.HitPoints);
        Assert.Equal(1, loss.FatiguePoints);
    }

    [Fact]
    public void A_successful_stamina_roll_staunches_the_bleeding_page_149()
    {
        // CON 10 => Stamina (CON x5) = 50; roll 10 succeeds.
        var outcome = BleedingEffectResolver.AttemptStaunch(MakeTarget(con: 10), new FixedEntropySource(10));

        Assert.True(outcome.Succeeded);
    }

    [Fact]
    public void A_failed_stamina_roll_does_not_staunch_the_bleeding_page_149()
    {
        var outcome = BleedingEffectResolver.AttemptStaunch(MakeTarget(con: 10), new FixedEntropySource(90));

        Assert.False(outcome.Succeeded);
    }

    [Fact]
    public void The_staunching_consequences_match_the_printed_rule_page_149()
    {
        var consequences = BleedingEffectResolver.StaunchConsequences;

        Assert.True(consequences.OtherActionsAreDifficult);
        Assert.True(consequences.DodgingCancelsTheAttempt);
        Assert.True(consequences.StrenuousActivityRestartsBleedingAfterSuccess);
    }

    [Theory]
    [InlineData(4, false)]
    [InlineData(5, true)]
    [InlineData(6, true)]
    public void Bleeding_stops_permanently_after_five_consecutive_staunched_rounds_page_149(
        int consecutiveStaunchedRounds, bool expectedStopped)
    {
        Assert.Equal(expectedStopped, BleedingEffectResolver.StopsPermanently(consecutiveStaunchedRounds, Ruleset));
    }

    private static RollOutcome MakeFirstAidRoll(SuccessLevel level) => new(
        Roll: 1,
        BaseChance: Percent.Zero,
        EffectiveChance: Percent.Zero,
        CriticalThreshold: Percent.Zero,
        SpecialThreshold: Percent.Zero,
        FumbleThreshold: Percent.Of(100),
        Level: level);

    [Fact]
    public void A_successful_first_aid_roll_stops_the_bleeding_permanently_page_149()
    {
        // p.149: "Success means that the bleeding stops and will not begin anew" -- stronger than
        // a successful Stamina staunch, which strenuous activity can restart.
        var outcome = BleedingEffectResolver.ApplyFirstAid(MakeFirstAidRoll(SuccessLevel.Success));

        Assert.True(outcome.StoppedPermanently);
        Assert.False(outcome.ContinuesUntilMedicalAttentionOrDeath);
    }

    [Fact]
    public void A_failed_first_aid_roll_leaves_the_bleeding_continuing_until_medical_attention_or_death_page_149()
    {
        // p.149: "Failure for this First Aid roll means that the bleeding continues until the
        // target receives successful medical attention (... another skill like Medicine) or dies
        // from blood loss when they reach 0 hit points."
        var outcome = BleedingEffectResolver.ApplyFirstAid(MakeFirstAidRoll(SuccessLevel.Failure));

        Assert.False(outcome.StoppedPermanently);
        Assert.True(outcome.ContinuesUntilMedicalAttentionOrDeath);
    }

    [Fact]
    public void A_fumbled_first_aid_roll_also_leaves_the_bleeding_continuing_page_149()
    {
        var outcome = BleedingEffectResolver.ApplyFirstAid(MakeFirstAidRoll(SuccessLevel.Fumble));

        Assert.False(outcome.StoppedPermanently);
        Assert.True(outcome.ContinuesUntilMedicalAttentionOrDeath);
    }
}
