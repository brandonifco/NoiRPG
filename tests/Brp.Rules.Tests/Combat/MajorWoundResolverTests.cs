using Brp.Core.Abilities;
using Brp.Core.Contests;
using Brp.Data;
using Brp.Rules.Combat;

namespace Brp.Rules.Tests.Combat;

/// <summary>
/// Ch 6: Combat, "Major Wounds" and "Fatal Wounds" (pp.155-156). The major-wound trigger at the
/// half-hit-point threshold, the shock effect (fight on for rounds equal to remaining hit points;
/// collapse for an hour at 2 or fewer), the immediate Luck roll (success = clean heal, failure =
/// permanent drain routed through the characteristic-recompute path), the cumulative-minor-wound
/// Luck-or-unconscious rule, and the fatal-wound rescue window. See
/// <c>docs/decisions/0021-major-wounds.md</c>.
/// </summary>
public class MajorWoundResolverTests
{
    private static readonly MajorWoundRuleset MajorWounds = NoirMajorWoundRuleset.Load();
    private static readonly DamageRuleset Damage = NoirDamageRuleset.Load();

    private static AbilitySet MakeTarget(int con = 12, int siz = 12, int pow = 12)
    {
        var ruleset = NoirAbilityRuleset.Load();
        var values = ruleset.Characteristics.Keys.ToDictionary(id => id, _ => 12);
        values[new CharacteristicId("CON")] = con;
        values[new CharacteristicId("SIZ")] = siz;
        values[new CharacteristicId("POW")] = pow;
        return new AbilitySet(ruleset, values);
    }

    private sealed class StubAdjudicator(BodySide limbSide, IReadOnlyList<CharacteristicId> characteristics)
        : IMajorWoundAdjudicator
    {
        public BodySide DecideLimbSide() => limbSide;

        public IReadOnlyList<CharacteristicId> DecideCharacteristics(int count) => characteristics;
    }

    private static StubAdjudicator Adjudicator(
        BodySide limbSide = BodySide.Left, IReadOnlyList<CharacteristicId>? characteristics = null) =>
        new(limbSide, characteristics ?? []);

    [Fact]
    public void A_wound_of_half_total_hit_points_or_more_is_a_major_wound_page_155()
    {
        // CON 12 + SIZ 12 => 12 max HP, major wound level = 6 (half, rounded up).
        var target = MakeTarget();
        Assert.Equal(6, target.MajorWoundLevel);

        Assert.True(MajorWoundResolver.IsMajorWound(6, target));
        Assert.True(MajorWoundResolver.IsMajorWound(7, target));
        Assert.False(MajorWoundResolver.IsMajorWound(5, target));
    }

    [Fact]
    public void Resolve_rejects_a_wound_below_the_major_wound_threshold_page_155()
    {
        var target = MakeTarget();

        Assert.Throws<ArgumentOutOfRangeException>(() => MajorWoundResolver.Resolve(
            target, woundDamage: 5, Damage, MajorWounds, Adjudicator(), new FixedEntropySource(10)));
    }

    [Fact]
    public void Shock_lets_the_character_fight_on_for_rounds_equal_to_remaining_hit_points_page_155()
    {
        // 8 hit points remaining after a major wound => fights on for 8 rounds, then unconscious.
        var target = MakeTarget();
        target.SetCurrentHitPoints(8);

        // POW 12 => Luck 60; roll 10 succeeds (no permanent loss), so only the Luck roll is drawn.
        var outcome = MajorWoundResolver.Resolve(
            target, woundDamage: 6, Damage, MajorWounds, Adjudicator(), new FixedEntropySource(10));

        Assert.Equal(8, outcome.Shock.FightingRounds);
        Assert.False(outcome.Shock.CollapsesImmediately);
        Assert.False(outcome.Shock.UnconsciousForAnHour);
    }

    [Fact]
    public void Two_or_fewer_hit_points_after_a_major_wound_collapses_for_an_hour_page_155()
    {
        var target = MakeTarget();
        target.SetCurrentHitPoints(2);

        var outcome = MajorWoundResolver.Resolve(
            target, woundDamage: 6, Damage, MajorWounds, Adjudicator(), new FixedEntropySource(10));

        Assert.True(outcome.Shock.CollapsesImmediately);
        Assert.True(outcome.Shock.UnconsciousForAnHour);
        Assert.Equal(0, outcome.Shock.FightingRounds);
    }

    [Fact]
    public void A_successful_luck_roll_heals_cleanly_with_no_permanent_loss_page_155()
    {
        var target = MakeTarget();
        target.SetCurrentHitPoints(6);
        var conBefore = target.ValueOf(new CharacteristicId("CON"));

        // POW 12 => Luck 60; roll 10 succeeds. No table roll, no drain.
        var outcome = MajorWoundResolver.Resolve(
            target, woundDamage: 6, Damage, MajorWounds, Adjudicator(), new FixedEntropySource(10));

        Assert.True(outcome.LuckRoll.Succeeded);
        Assert.False(outcome.PermanentInjury);
        Assert.Null(outcome.TableRoll);
        Assert.Null(outcome.Row);
        Assert.Empty(outcome.CharacteristicLosses);
        Assert.True(outcome.AbleToFight);
        Assert.Equal(0, outcome.MovementReduction);
        Assert.Equal(conBefore, target.ValueOf(new CharacteristicId("CON")));
    }

    [Fact]
    public void A_failed_luck_roll_applies_the_table_drain_and_recomputes_derived_values_page_155()
    {
        var target = MakeTarget();
        target.SetCurrentHitPoints(6);
        Assert.Equal(12, target.MaximumHitPoints);

        // POW 12 => Luck 60; roll 90 fails. Table roll 35 => 31-40 row (lose 1D3 CON, reduce MOV by
        // the same). Loss die 3 => CON 12 -> 9. Entropy order: Luck, table, loss die.
        var outcome = MajorWoundResolver.Resolve(
            target, woundDamage: 6, Damage, MajorWounds, Adjudicator(), new FixedEntropySource(90, 35, 3));

        Assert.False(outcome.LuckRoll.Succeeded);
        Assert.True(outcome.PermanentInjury);
        Assert.Equal(35, outcome.TableRoll);
        Assert.Single(outcome.CharacteristicLosses);

        var loss = outcome.CharacteristicLosses[0];
        Assert.Equal(new CharacteristicId("CON"), loss.Characteristic);
        Assert.Equal(3, loss.PointsLost);
        Assert.Equal(9, loss.ResultingValue);
        Assert.Equal(9, target.ValueOf(new CharacteristicId("CON")));

        // Live recompute: CON 9 + SIZ 12 => 11 max HP (was 12) -- not baked.
        Assert.Equal(11, target.MaximumHitPoints);

        // 31-40 reduces MOV by the CON loss (3).
        Assert.Equal(3, outcome.MovementReduction);
        Assert.True(outcome.AbleToFight);
    }

    [Fact]
    public void A_no_fight_row_reports_the_character_cannot_fight_page_156()
    {
        var target = MakeTarget();
        target.SetCurrentHitPoints(6);

        // Luck 60 fails on 90; table roll 55 => 51-60 row (lose 1D6 DEX, unable to fight). Loss die 4.
        var outcome = MajorWoundResolver.Resolve(
            target, woundDamage: 6, Damage, MajorWounds, Adjudicator(), new FixedEntropySource(90, 55, 4));

        Assert.False(outcome.AbleToFight);
        Assert.Equal(new CharacteristicId("DEX"), outcome.CharacteristicLosses[0].Characteristic);
        Assert.Equal(4, outcome.MovementReduction);
    }

    [Fact]
    public void A_limb_row_reports_the_gamemaster_ruled_side_page_155()
    {
        var target = MakeTarget();
        target.SetCurrentHitPoints(6);

        // Table roll 95 => 95-96 row ("left or right arm"), needs a limb-side ruling. Loss die 4.
        var outcome = MajorWoundResolver.Resolve(
            target, woundDamage: 6, Damage, MajorWounds,
            Adjudicator(limbSide: BodySide.Right), new FixedEntropySource(90, 95, 4));

        Assert.Equal(BodySide.Right, outcome.LimbSide);
    }

    [Fact]
    public void A_non_limb_row_reports_no_side_page_155()
    {
        var target = MakeTarget();
        target.SetCurrentHitPoints(6);

        // Table roll 35 (31-40) is not a limb row.
        var outcome = MajorWoundResolver.Resolve(
            target, woundDamage: 6, Damage, MajorWounds,
            Adjudicator(limbSide: BodySide.Right), new FixedEntropySource(90, 35, 3));

        Assert.Null(outcome.LimbSide);
    }

    [Fact]
    public void The_99_row_drains_three_characteristics_page_156()
    {
        var target = MakeTarget();
        target.SetCurrentHitPoints(6);

        // Table roll 99 => lose 1D3 each from CHA, DEX, CON. Loss dice 2, 2, 2 in row order.
        var outcome = MajorWoundResolver.Resolve(
            target, woundDamage: 6, Damage, MajorWounds, Adjudicator(), new FixedEntropySource(90, 99, 2, 2, 2));

        Assert.Equal(3, outcome.CharacteristicLosses.Count);
        Assert.Equal(
            new[] { new CharacteristicId("CHA"), new CharacteristicId("DEX"), new CharacteristicId("CON") },
            outcome.CharacteristicLosses.Select(l => l.Characteristic));
        Assert.False(outcome.AbleToFight);
    }

    [Fact]
    public void The_00_row_removes_1D4_each_from_four_gamemaster_chosen_characteristics_page_156()
    {
        var target = MakeTarget();
        target.SetCurrentHitPoints(6);
        var chosen = new[]
        {
            new CharacteristicId("STR"), new CharacteristicId("CON"),
            new CharacteristicId("DEX"), new CharacteristicId("INT"),
        };

        // Table roll 100 (printed 00) => remove 1D4 each from four GM-chosen characteristics.
        // Entropy: Luck 90, table 100, then four 1D4 loss dice (2 each).
        var outcome = MajorWoundResolver.Resolve(
            target, woundDamage: 6, Damage, MajorWounds,
            Adjudicator(characteristics: chosen), new FixedEntropySource(90, 100, 2, 2, 2, 2));

        Assert.Equal(4, outcome.CharacteristicLosses.Count);
        Assert.Equal(chosen, outcome.CharacteristicLosses.Select(l => l.Characteristic));
        Assert.All(outcome.CharacteristicLosses, l => Assert.Equal(2, l.PointsLost));
        Assert.Equal(10, target.ValueOf(new CharacteristicId("STR")));
        Assert.False(outcome.AbleToFight);
    }

    [Fact]
    public void Cumulative_minor_wounds_reaching_a_major_equivalent_force_a_luck_roll_page_155()
    {
        var target = MakeTarget();
        target.SetCurrentHitPoints(6);

        // Total minor loss 6 == major wound level; Luck 60 fails on 90 => falls unconscious.
        var failed = MajorWoundResolver.ResolveCumulativeMinorWounds(
            target, totalMinorHitPointsLostToday: 6, Damage, new FixedEntropySource(90));

        Assert.True(failed.ReachedMajorWoundEquivalent);
        Assert.NotNull(failed.LuckRoll);
        Assert.True(failed.FallsUnconscious);

        // A successful Luck roll stays conscious. (Do NOT roll on the Major Wounds Table for this.)
        var target2 = MakeTarget();
        target2.SetCurrentHitPoints(6);
        var passed = MajorWoundResolver.ResolveCumulativeMinorWounds(
            target2, totalMinorHitPointsLostToday: 6, Damage, new FixedEntropySource(10));

        Assert.True(passed.ReachedMajorWoundEquivalent);
        Assert.False(passed.FallsUnconscious);
    }

    [Fact]
    public void Cumulative_minor_wounds_below_the_equivalent_roll_nothing_page_155()
    {
        var target = MakeTarget();
        target.SetCurrentHitPoints(7);

        // Total 5 < major wound level 6, and 7 HP is above the knockout band. No entropy consumed.
        var entropy = new FixedEntropySource();
        var outcome = MajorWoundResolver.ResolveCumulativeMinorWounds(
            target, totalMinorHitPointsLostToday: 5, Damage, entropy);

        Assert.False(outcome.ReachedMajorWoundEquivalent);
        Assert.Null(outcome.LuckRoll);
        Assert.False(outcome.FallsUnconscious);
        Assert.False(outcome.KnockedOutForAnHour);
        Assert.Equal(0, entropy.DrawCount);
    }

    [Fact]
    public void Minor_wounds_reducing_the_character_to_one_or_two_hit_points_knock_them_out_page_155()
    {
        var target = MakeTarget();
        target.SetCurrentHitPoints(2);

        // 5 lost is below the major-wound equivalent (no Luck roll), but 2 HP => knocked out an hour.
        var outcome = MajorWoundResolver.ResolveCumulativeMinorWounds(
            target, totalMinorHitPointsLostToday: 5, Damage, new FixedEntropySource());

        Assert.False(outcome.ReachedMajorWoundEquivalent);
        Assert.True(outcome.KnockedOutForAnHour);
    }

    [Fact]
    public void A_fatally_wounded_character_survives_only_with_in_window_aid_that_restores_hit_points_page_156()
    {
        // In the wound round (0), aid to 1 HP => survives.
        Assert.True(MajorWoundResolver.SurvivesFatalWound(
            hitPointsAfterAid: 1, roundsSinceFatalWound: 0, MajorWounds, Damage));

        // The round immediately after (1) is still in the window.
        Assert.True(MajorWoundResolver.SurvivesFatalWound(
            hitPointsAfterAid: 3, roundsSinceFatalWound: 1, MajorWounds, Damage));

        // Two rounds later is too late, even at healthy hit points.
        Assert.False(MajorWoundResolver.SurvivesFatalWound(
            hitPointsAfterAid: 6, roundsSinceFatalWound: 2, MajorWounds, Damage));

        // In the window but not brought above 0 => death still resolves.
        Assert.False(MajorWoundResolver.SurvivesFatalWound(
            hitPointsAfterAid: 0, roundsSinceFatalWound: 0, MajorWounds, Damage));
    }
}
