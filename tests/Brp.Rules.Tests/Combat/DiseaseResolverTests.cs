using Brp.Core.Abilities;
using Brp.Core.Contests;
using Brp.Core.Resolution;
using Brp.Data;
using Brp.Rules.Combat;

namespace Brp.Rules.Tests.Combat;

/// <summary>
/// Ch 7: Spot Rules, "Disease" (p.170). The Stamina contraction roll, the CON×N recovery
/// ladder (rising multiplier, fumble and strenuous-condition reductions), and the Illness Severity
/// Table drain routed through the characteristic-recompute path. See
/// <c>docs/decisions/0019-injury-spot-rules.md</c>.
/// </summary>
public class DiseaseResolverTests
{
    private static readonly DiseaseRuleset Disease = NoirInjuryRuleset.Load().Disease;

    private static AbilitySet MakeTarget(int con = 10, int siz = 12)
    {
        var ruleset = NoirAbilityRuleset.Load();
        var values = ruleset.Characteristics.Keys.ToDictionary(id => id, _ => 12);
        values[new CharacteristicId("CON")] = con;
        values[new CharacteristicId("SIZ")] = siz;
        return new AbilitySet(ruleset, values);
    }

    [Fact]
    public void A_failed_stamina_roll_contracts_the_disease_page_170()
    {
        // CON 10 => Stamina (CON×5) = 50; roll 90 fails => contracted.
        var contraction = DiseaseResolver.RollContraction(MakeTarget(con: 10), new FixedEntropySource(90));

        Assert.True(contraction.Contracted);
    }

    [Fact]
    public void A_successful_stamina_roll_avoids_the_disease_page_170()
    {
        // CON 10 => 50; roll 10 succeeds => avoided.
        var contraction = DiseaseResolver.RollContraction(MakeTarget(con: 10), new FixedEntropySource(10));

        Assert.False(contraction.Contracted);
    }

    [Fact]
    public void Recovering_on_the_first_day_yields_no_failures_page_170()
    {
        // Day 0 = CON×2 = 20; roll 10 succeeds.
        var ladder = DiseaseResolver.ResolveRecoveryLadder(
            MakeTarget(con: 10), strenuousConditionCount: 0, Disease, new FixedEntropySource(10), maxDays: 5);

        Assert.True(ladder.Recovered);
        Assert.Equal(0, ladder.Failures);
        Assert.Single(ladder.Days);
    }

    [Fact]
    public void The_multiplier_rises_by_one_each_day_until_recovery_page_170()
    {
        // Day 0 CON×2=20 roll 50 fails; day 1 CON×3=30 roll 10 succeeds.
        var ladder = DiseaseResolver.ResolveRecoveryLadder(
            MakeTarget(con: 10), strenuousConditionCount: 0, Disease, new FixedEntropySource(50, 10), maxDays: 5);

        Assert.True(ladder.Recovered);
        Assert.Equal(1, ladder.Failures);
        Assert.Equal(2, ladder.Days[0].EffectiveMultiplier);
        Assert.Equal(3, ladder.Days[1].EffectiveMultiplier);
    }

    [Fact]
    public void A_fumble_reduces_the_multiplier_by_one_page_170()
    {
        // Day 0 CON×2=20 roll 100 fumbles; day 1 base ×3 minus the fumble penalty => ×2; roll 10 succeeds.
        var ladder = DiseaseResolver.ResolveRecoveryLadder(
            MakeTarget(con: 10), strenuousConditionCount: 0, Disease, new FixedEntropySource(100, 10), maxDays: 5);

        Assert.Equal(SuccessLevel.Fumble, ladder.Days[0].Roll.Level);
        Assert.Equal(2, ladder.Days[1].EffectiveMultiplier);
        Assert.True(ladder.Recovered);
    }

    [Fact]
    public void Each_strenuous_condition_reduces_the_multiplier_by_one_page_170()
    {
        // Day 0 base ×2 minus one strenuous condition => ×1 = 10; roll 5 succeeds.
        var ladder = DiseaseResolver.ResolveRecoveryLadder(
            MakeTarget(con: 10), strenuousConditionCount: 1, Disease, new FixedEntropySource(5), maxDays: 5);

        Assert.Equal(1, ladder.Days[0].EffectiveMultiplier);
        Assert.True(ladder.Recovered);
        Assert.Equal(0, ladder.Failures);
    }

    [Fact]
    public void The_ladder_gives_up_after_the_day_budget_without_recovering()
    {
        // CON 3: day 0 ×2=6, day 1 ×3=9; rolls 50, 50 both fail within a two-day budget.
        var ladder = DiseaseResolver.ResolveRecoveryLadder(
            MakeTarget(con: 3), strenuousConditionCount: 0, Disease, new FixedEntropySource(50, 50), maxDays: 2);

        Assert.False(ladder.Recovered);
        Assert.Equal(2, ladder.Failures);
    }

    [Theory]
    [InlineData(0, IllnessDegree.None, IllnessLossPeriod.None)]
    [InlineData(1, IllnessDegree.Mild, IllnessLossPeriod.Week)]
    [InlineData(2, IllnessDegree.Acute, IllnessLossPeriod.Day)]
    [InlineData(3, IllnessDegree.Severe, IllnessLossPeriod.Hour)]
    [InlineData(5, IllnessDegree.Terminal, IllnessLossPeriod.Minute)]
    public void Severity_is_a_rate_of_one_point_per_period_not_a_baked_quantity_page_170(
        int failures, IllnessDegree expectedDegree, IllnessLossPeriod expectedPeriod)
    {
        var target = MakeTarget(con: 12, siz: 12);
        var before = target.ValueOf(new CharacteristicId("STR"));

        var severity = DiseaseResolver.ResolveSeverity(failures, Disease);

        Assert.Equal(expectedDegree, severity.Degree);
        Assert.Equal(expectedPeriod, severity.LossPeriod);

        // ResolveSeverity reports the loss rate only -- it drains nothing itself; the wall-clock
        // point loss is a clock-aware caller's job (ApplyCharacteristicLoss).
        Assert.Equal(before, target.ValueOf(new CharacteristicId("STR")));
    }

    [Fact]
    public void A_lost_point_drained_by_a_clock_aware_caller_recomputes_derived_values_page_170()
    {
        var target = MakeTarget(con: 16, siz: 14);
        Assert.Equal(15, target.MaximumHitPoints);

        // Acute is 1 point/day; a caller two days in has accrued 2 CON points. Applying them via
        // AbilitySet.Set recomputes hit points -- the loss is not baked at severity-lookup time.
        var severity = DiseaseResolver.ResolveSeverity(failures: 2, Disease);
        Assert.Equal(IllnessLossPeriod.Day, severity.LossPeriod);

        var newValue = DiseaseResolver.ApplyCharacteristicLoss(target, new CharacteristicId("CON"), points: 2);

        Assert.Equal(14, newValue);
        Assert.Equal(14, target.ValueOf(new CharacteristicId("CON")));
        Assert.Equal(14, target.MaximumHitPoints); // ceil((14 + 14) / 2)
    }

    [Fact]
    public void The_gamemaster_chosen_characteristic_is_the_one_drained()
    {
        var target = MakeTarget(con: 12, siz: 12);
        var adjudicator = new DefaultInjuryAdjudicator();
        var affected = adjudicator.DecideDiseaseAffectedCharacteristic();

        DiseaseResolver.ApplyCharacteristicLoss(target, affected, points: 3);

        Assert.Equal(new CharacteristicId("CON"), affected);
        Assert.Equal(9, target.ValueOf(new CharacteristicId("CON")));
    }

    [Fact]
    public void A_minor_disease_costs_hit_points_and_fatigue_page_170()
    {
        // 1D2 hit points then 1D6 fatigue.
        var effect = DiseaseResolver.RollMinorDiseaseEffect(Disease, new FixedEntropySource(2, 4));

        Assert.Equal(2, effect.HitPointLoss);
        Assert.Equal(4, effect.FatigueLoss);
    }
}
