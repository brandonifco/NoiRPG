using Brp.Core.Abilities;
using Brp.Core.Contests;
using Brp.Data;
using Brp.Rules.Characters;
using Brp.Rules.Combat;

namespace Brp.Rules.Tests.Combat;

/// <summary>
/// Ch 7: Spot Rules, "Poison" and "Poison Antidotes" (pp.175-176). POT vs CON on the resistance
/// table (full POT if overcome, else half round-up), antidote subtraction, two-dose independence,
/// and drain routed through the characteristic-recompute path. See
/// <c>docs/decisions/0019-injury-spot-rules.md</c>.
/// </summary>
public class PoisonResolverTests
{
    private static readonly PoisonRuleset Poison = NoirInjuryRuleset.Load().Poison;
    private static readonly DamageRuleset Damage = NoirDamageRuleset.Load();

    private static AbilitySet MakeTarget(int con = 12, int siz = 12, int strength = 12)
    {
        var ruleset = NoirAbilityRuleset.Load();
        var values = ruleset.Characteristics.Keys.ToDictionary(id => id, _ => 12);
        values[new CharacteristicId("CON")] = con;
        values[new CharacteristicId("SIZ")] = siz;
        values[new CharacteristicId("STR")] = strength;
        return new AbilitySet(ruleset, values);
    }

    [Fact]
    public void Overcoming_con_deals_the_full_pot_page_175()
    {
        // POT 15 vs CON 10 => chance 75; roll 50 succeeds (poison overcomes CON).
        var outcome = PoisonResolver.ResolvePoison(
            poisonPotency: 15, constitution: 10, effectiveAntidotePotency: 0, Poison, new FixedEntropySource(50));

        Assert.True(outcome.Overcame);
        Assert.Equal(15, outcome.Damage);
    }

    [Fact]
    public void Failing_to_overcome_con_deals_half_pot_rounded_up_page_175()
    {
        // POT 15 vs CON 10 => chance 75; roll 90 fails; half of 15 rounds up to 8.
        var outcome = PoisonResolver.ResolvePoison(
            poisonPotency: 15, constitution: 10, effectiveAntidotePotency: 0, Poison, new FixedEntropySource(90));

        Assert.False(outcome.Overcame);
        Assert.Equal(8, outcome.Damage);
    }

    [Fact]
    public void An_in_window_same_type_antidote_subtracts_its_full_pot_page_176()
    {
        var effective = PoisonResolver.EffectiveAntidotePotency(
            antidotePotency: 8, turnsBeforePoisoning: 6, sameType: true, Poison, new DefaultInjuryAdjudicator());

        Assert.Equal(8, effective);
    }

    [Fact]
    public void An_antidote_taken_outside_the_six_turn_window_gives_no_benefit_page_176()
    {
        var effective = PoisonResolver.EffectiveAntidotePotency(
            antidotePotency: 8, turnsBeforePoisoning: 7, sameType: true, Poison, new DefaultInjuryAdjudicator());

        Assert.Equal(0, effective);
    }

    [Fact]
    public void A_cross_type_antidote_gives_the_gamemasters_lessened_benefit_page_176()
    {
        var adjudicator = new StubAdjudicator(crossTypePotency: 3);

        var effective = PoisonResolver.EffectiveAntidotePotency(
            antidotePotency: 8, turnsBeforePoisoning: 2, sameType: false, Poison, adjudicator);

        Assert.Equal(3, effective);
    }

    [Fact]
    public void The_antidote_pot_is_subtracted_before_the_resistance_roll_page_176()
    {
        // Poison POT 20, antidote 8 => effective POT 12 vs CON 10 => chance 60; roll 50 overcomes.
        var outcome = PoisonResolver.ResolvePoison(
            poisonPotency: 20, constitution: 10, effectiveAntidotePotency: 8, Poison, new FixedEntropySource(50));

        Assert.Equal(12, outcome.EffectivePotency);
        Assert.True(outcome.Overcame);
        Assert.Equal(12, outcome.Damage);
    }

    [Fact]
    public void A_fully_neutralizing_antidote_draws_no_roll_and_deals_no_damage()
    {
        var entropy = new FixedEntropySource();

        var outcome = PoisonResolver.ResolvePoison(
            poisonPotency: 10, constitution: 10, effectiveAntidotePotency: 10, Poison, entropy);

        Assert.Equal(0, outcome.EffectivePotency);
        Assert.Null(outcome.Resistance);
        Assert.Equal(0, outcome.Damage);
        Assert.Equal(0, entropy.DrawCount);
    }

    [Fact]
    public void Two_doses_are_two_separate_rolls_not_one_of_double_pot_page_175()
    {
        // Two POT 10 doses vs CON 10 => each chance 50; rolls 90 and 90 both fail => 5 + 5.
        var doses = new FixedEntropySource(90, 90);
        var first = PoisonResolver.ResolvePoison(10, 10, 0, Poison, doses);
        var second = PoisonResolver.ResolvePoison(10, 10, 0, Poison, doses);

        Assert.False(first.Overcame);
        Assert.False(second.Overcame);
        Assert.Equal(10, first.Damage + second.Damage);

        // One POT 20 dose vs CON 10 => chance 100, automatic success (a different result entirely).
        var single = PoisonResolver.ResolvePoison(20, 10, 0, Poison, new FixedEntropySource(90));
        Assert.True(single.Resistance!.IsAutomaticSuccess);
        Assert.Equal(20, single.Damage);
    }

    [Fact]
    public void Hit_point_damage_flows_through_the_damage_path()
    {
        var target = MakeTarget(con: 13, siz: 12);
        var wounds = new WoundTrack();
        // POT 8 vs CON 13 => chance 25; roll 20 overcomes => 8 damage.
        var outcome = PoisonResolver.ResolvePoison(8, 13, 0, Poison, new FixedEntropySource(20));

        var result = PoisonResolver.ApplyHitPointDamage(target, wounds, outcome, Damage, "Poison POT 8");

        Assert.Equal(8, outcome.Damage);
        Assert.Equal(target.MaximumHitPoints - 8, result.ResultingHitPoints);
        Assert.Single(wounds.Wounds);
    }

    [Fact]
    public void Characteristic_drain_recomputes_derived_values()
    {
        var target = MakeTarget(con: 13, siz: 12);
        Assert.Equal(13, target.MaximumHitPoints);

        // POT 8 vs CON 13 => chance 25; roll 20 overcomes => 8 points drained from CON.
        var outcome = PoisonResolver.ResolvePoison(8, 13, 0, Poison, new FixedEntropySource(20));
        var newValue = PoisonResolver.ApplyCharacteristicDrain(target, new CharacteristicId("CON"), outcome);

        Assert.Equal(5, newValue);
        Assert.Equal(5, target.ValueOf(new CharacteristicId("CON")));
        Assert.Equal(9, target.MaximumHitPoints); // ceil((5 + 12) / 2)
    }

    [Fact]
    public void Onset_uses_the_printed_default_for_the_gamemasters_speed_page_175()
    {
        var fast = PoisonResolver.ResolveOnset(new PoisonOnsetRuling(PoisonOnsetSpeed.FastActing, null), Poison);
        Assert.Equal(new PoisonOnset(3, PoisonOnsetUnit.CombatRounds), fast);

        var slow = PoisonResolver.ResolveOnset(new PoisonOnsetRuling(PoisonOnsetSpeed.SlowActing, null), Poison);
        Assert.Equal(new PoisonOnset(3, PoisonOnsetUnit.FullTurns), slow);

        var overridden = PoisonResolver.ResolveOnset(new PoisonOnsetRuling(PoisonOnsetSpeed.FastActing, 1), Poison);
        Assert.Equal(new PoisonOnset(1, PoisonOnsetUnit.CombatRounds), overridden);
    }

    private sealed class StubAdjudicator(int crossTypePotency) : IInjuryAdjudicator
    {
        public FallingSurfaceRuling DecideFallingSurface() => new(0);

        public PoisonOnsetRuling DecidePoisonOnset() => new(PoisonOnsetSpeed.FastActing, null);

        public int DecideAntidoteCrossTypePotency(int crossTypeAntidotePotency) => crossTypePotency;

        public CharacteristicId DecideDiseaseAffectedCharacteristic() => new("CON");
    }
}
