using Brp.Core.Contests;
using Brp.Core.Modifiers;
using Brp.Core.Primitives;
using Brp.Core.Resolution;
using Brp.Rules.Combat;

namespace Brp.Rules.Tests.Combat;

/// <summary>
/// Covers the five in-scope situational combat spot rules against Ch 7: Spot Rules -- Ambushes
/// (p.162), Backstabs and Helpless Opponents (p.164), Cover (p.169), Darkness (p.169), and Firing
/// Into Combat (p.173) -- as ordinary modifier producers feeding ADR 0007's pipeline. See
/// <c>docs/decisions/0018-spot-rules.md</c>.
/// </summary>
public class SpotRuleResolverTests
{
    // Constructed inline from the book's literal values so a change to the shipped data cannot
    // silently rewrite what these logic tests assert (the data itself is pinned by
    // NoirSpotRuleRulesetTests).
    private static readonly SpotRuleRuleset Ruleset = new(
        firingIntoCombatModifier: -20,
        darknessSemiDarknessModifier: -20,
        darknessPitchBlackModifier: -50,
        darknessDetectionHalvingNumerator: 1,
        darknessDetectionHalvingDenominator: 2);

    private static readonly Modifier[] NoOtherModifiers = [];

    // ---- Ambushes (Ch 7, p.162) -------------------------------------------------------------

    [Theory]
    // (kind, role, expectsEasy, expectsDifficult, expectsImpossibleGate, expectsEmpty)
    [InlineData(AmbushKind.MissileUnseen, SpotRuleRole.Attacker, true, false, false, false)]
    [InlineData(AmbushKind.MissileSeen, SpotRuleRole.Attacker, true, false, false, false)]
    [InlineData(AmbushKind.HandToHandTargetUnaware, SpotRuleRole.Attacker, true, false, false, false)]
    [InlineData(AmbushKind.HandToHandTargetAware, SpotRuleRole.Attacker, false, false, false, true)]
    [InlineData(AmbushKind.MissileUnseen, SpotRuleRole.Defender, false, false, true, false)]
    [InlineData(AmbushKind.MissileSeen, SpotRuleRole.Defender, false, false, false, true)]
    [InlineData(AmbushKind.HandToHandTargetUnaware, SpotRuleRole.Defender, false, true, false, false)]
    [InlineData(AmbushKind.HandToHandTargetAware, SpotRuleRole.Defender, false, false, false, true)]
    public void Ambush_produces_the_book_modifier_per_case_and_role(
        AmbushKind kind, SpotRuleRole role, bool expectsEasy, bool expectsDifficult, bool expectsImpossibleGate, bool expectsEmpty)
    {
        var modifiers = SpotRuleResolver.Ambush(kind, role);

        AssertGrade(modifiers, expectsEasy, expectsDifficult, expectsImpossibleGate, expectsEmpty);
    }

    [Fact]
    public void Ambush_missile_unseen_makes_the_attack_easy_and_forbids_the_defense()
    {
        // Ch 7, "Ambushes" (p.162): "the attackers get a free round of Easy attacks. The target(s)
        // cannot dodge or parry this initial round of attacks."
        var attack = SpotRuleResolver.Ambush(AmbushKind.MissileUnseen, SpotRuleRole.Attacker);
        var defense = SpotRuleResolver.Ambush(AmbushKind.MissileUnseen, SpotRuleRole.Defender);

        Assert.Equal(DifficultyDirection.Easier, Assert.Single(attack.OfType<DifficultyModifier>()).Direction);

        var defenseChain = SpotRuleResolver.Evaluate(Percent.Of(60), defense, NoOtherModifiers);
        Assert.True(defenseChain.IsGated);
        Assert.Equal(GateKind.Impossible, defenseChain.Gate);
    }

    // ---- Backstabs and Helpless Opponents (Ch 7, p.164) -------------------------------------

    [Theory]
    [InlineData(BackstabKind.UnprotectedBack, true)]
    [InlineData(BackstabKind.Helpless, true)]
    public void Backstab_attack_is_always_easy(BackstabKind kind, bool expectsEasy)
    {
        // Ch 7, "Backstabs and Helpless Opponents" (p.164): the unprotected-back attack and the
        // helpless-target attack are both Easy. "No additional damage is done by such an attack" --
        // the Easy grade is the whole benefit, not a damage bonus (damage is out of scope).
        var modifiers = SpotRuleResolver.Backstab(kind, SpotRuleRole.Attacker);

        Assert.Equal(expectsEasy, modifiers.OfType<DifficultyModifier>().Any(d => d.Direction == DifficultyDirection.Easier));
    }

    [Fact]
    public void Backstab_unprotected_back_gives_a_difficult_defense_only_when_the_target_detects_the_attacker()
    {
        // Ch 7 (p.164): "If the target succeeds in a Difficult Listen or Sense roll, they can make a
        // Difficult Dodge or parry attempt" -- otherwise the undetected target gets no defense.
        var detected = SpotRuleResolver.Backstab(BackstabKind.UnprotectedBack, SpotRuleRole.Defender, defenderDetectedAttacker: true);
        var undetected = SpotRuleResolver.Backstab(BackstabKind.UnprotectedBack, SpotRuleRole.Defender, defenderDetectedAttacker: false);

        Assert.Equal(DifficultyDirection.Harder, Assert.Single(detected.OfType<DifficultyModifier>()).Direction);
        Assert.Equal(GateKind.Impossible, Assert.Single(undetected.OfType<GateModifier>()).Kind);
    }

    [Fact]
    public void Backstab_helpless_target_cannot_defend_regardless_of_detection()
    {
        // Ch 7 (p.164): a helpless target "cannot make a dodge or parry attempt against the attack."
        var detected = SpotRuleResolver.Backstab(BackstabKind.Helpless, SpotRuleRole.Defender, defenderDetectedAttacker: true);
        var undetected = SpotRuleResolver.Backstab(BackstabKind.Helpless, SpotRuleRole.Defender, defenderDetectedAttacker: false);

        Assert.Equal(GateKind.Impossible, Assert.Single(detected.OfType<GateModifier>()).Kind);
        Assert.Equal(GateKind.Impossible, Assert.Single(undetected.OfType<GateModifier>()).Kind);
    }

    // ---- Cover (Ch 7, p.169) ----------------------------------------------------------------

    [Fact]
    public void Cover_makes_the_attack_difficult_and_the_book_example_halves_a_72_percent_rating_to_36()
    {
        // Ch 7, "Cover" (p.169): "any attacks on that target are Difficult... Their normal skill
        // rating is 72%, reduced by half to 36% because the task is Difficult." Cover is not a
        // defensive roll, so the defender contributes nothing.
        var attack = SpotRuleResolver.Cover(SpotRuleRole.Attacker);
        var defense = SpotRuleResolver.Cover(SpotRuleRole.Defender);

        Assert.Equal(DifficultyDirection.Harder, Assert.Single(attack.OfType<DifficultyModifier>()).Direction);
        Assert.Empty(defense);

        var chain = SpotRuleResolver.Evaluate(Percent.Of(72), attack, NoOtherModifiers);
        Assert.Equal(Percent.Of(36), chain.EffectiveChance); // the book's worked example
    }

    // ---- Darkness (Ch 7, p.169; Ch 5 Situational Modifiers, p.133) --------------------------

    [Theory]
    // (severity, opponentDetected, expectedDelta)
    [InlineData(DarknessSeverity.SemiDarkness, false, -20)] // Ch 5 Environment tier, p.133
    [InlineData(DarknessSeverity.PitchBlack, false, -50)]   // Ch 5 Environment tier, p.133
    [InlineData(DarknessSeverity.SemiDarkness, true, -10)]  // Ch 7 p.169: "reduce the darkness modifier by half"
    [InlineData(DarknessSeverity.PitchBlack, true, -25)]
    public void Darkness_produces_the_situational_penalty_and_halves_it_when_the_opponent_is_detected(
        DarknessSeverity severity, bool opponentDetected, int expectedDelta)
    {
        var modifiers = SpotRuleResolver.Darkness(severity, opponentDetected, Ruleset);

        var additive = Assert.Single(modifiers.OfType<AdditiveModifier>());
        Assert.Equal(expectedDelta, additive.Delta);
        Assert.Equal(AdditiveKind.Situational, additive.Kind); // applied after any difficulty grade (Ch 5, p.132)
    }

    [Fact]
    public void Darkness_penalty_is_situational_so_it_is_not_halved_by_a_difficult_grade_in_play()
    {
        // ADR 0007 / Ch 5 (p.132): a situational modifier is applied AFTER the difficulty grade, so
        // a -20% darkness penalty stays -20 even when the same roll is also Difficult (e.g. cover).
        // 65 -> ceil(65/2)=33 (Difficult) -> 33-20=13, not 65-20=45 then halved to 23.
        var darkness = SpotRuleResolver.Darkness(DarknessSeverity.SemiDarkness, opponentDetected: false, Ruleset);
        var cover = SpotRuleResolver.Cover(SpotRuleRole.Attacker);

        var chain = SpotRuleResolver.Evaluate(Percent.Of(65), darkness, cover);

        Assert.Equal(Percent.Of(13), chain.EffectiveChance);
    }

    // ---- Firing Into Combat (Ch 7, p.173) ---------------------------------------------------

    [Theory]
    // (firingIntoMelee, firingWhileEngaged, expectsMinus20, expectsDifficult)
    [InlineData(true, false, true, false)]  // into a melee: -20%
    [InlineData(false, true, false, true)]  // while engaged: Difficult
    [InlineData(true, true, true, true)]    // both conditions at once
    [InlineData(false, false, false, false)]
    public void Firing_into_combat_distinguishes_into_a_melee_from_firing_while_engaged(
        bool firingIntoMelee, bool firingWhileEngaged, bool expectsMinus20, bool expectsDifficult)
    {
        var modifiers = SpotRuleResolver.FiringIntoCombat(firingIntoMelee, firingWhileEngaged, Ruleset);

        Assert.Equal(expectsMinus20, modifiers.OfType<AdditiveModifier>().Any(a => a.Delta == -20));
        Assert.Equal(
            expectsDifficult,
            modifiers.OfType<DifficultyModifier>().Any(d => d.Direction == DifficultyDirection.Harder));
    }

    [Fact]
    public void Firing_while_engaged_difficult_is_cancelled_by_a_point_blank_easy_when_both_in_close_range()
    {
        // Ch 7, "Firing Into Combat" (p.173): "if the attacker and the target are both within close
        // combat range, the attack is Easy (for Point-blank Range), so the Difficult and Easy
        // modifiers cancel one another." This resolver PRODUCES the while-engaged Difficult; the
        // point-blank Easy is RangeBandResolver's contribution (RangeBand.PointBlank). Composed
        // together, ADR 0007's non-stacking collapse cancels the pair -- reconciling the same
        // mechanic RangeBandResolverTests.Point_blank_and_a_difficult_condition_cancel_pairwise
        // hand-rolls, without duplicating it.
        var firing = SpotRuleResolver.FiringIntoCombat(firingIntoMelee: false, firingWhileEngaged: true, Ruleset);
        var pointBlank = RangeBandResolver.Resolve(
            RangeBand.PointBlank, Percent.Of(65), NoOtherModifiers, aimedWithTargetingEquipment: false,
            new RangeBandRuleset(3, 2, 1, 5, 2, 1, 2));
        var pointBlankModifiers = Assert.IsType<RangeBandOutcome.Composable>(pointBlank).Modifiers;

        var chain = SpotRuleResolver.Evaluate(Percent.Of(65), firing, pointBlankModifiers);

        Assert.Equal(Percent.Of(65), chain.EffectiveChance); // Difficult and Easy cancel
    }

    [Fact]
    public void Firing_into_a_melee_minus_20_is_the_situational_penalty_the_pipeline_applies_last()
    {
        var firing = SpotRuleResolver.FiringIntoCombat(firingIntoMelee: true, firingWhileEngaged: false, Ruleset);

        var chain = SpotRuleResolver.Evaluate(Percent.Of(65), firing, NoOtherModifiers);

        Assert.Equal(Percent.Of(45), chain.EffectiveChance); // 65 - 20
    }

    // ---- Composition and deterministic resolution -------------------------------------------

    [Fact]
    public void A_spot_rule_modified_chain_resolves_a_fixed_roll_deterministically()
    {
        // Wire the whole path: a darkness situational penalty through the pipeline, then a single
        // scripted percentile via FixedEntropySource (AGENTS.md invariant 5: seeded, replayable).
        var darkness = SpotRuleResolver.Darkness(DarknessSeverity.SemiDarkness, opponentDetected: false, Ruleset);
        var chain = SpotRuleResolver.Evaluate(Percent.Of(65), darkness, NoOtherModifiers);
        Assert.Equal(Percent.Of(45), chain.EffectiveChance);

        var entropy = new FixedEntropySource(10);
        var outcome = chain.Resolve(printedBaseChance: Percent.Of(25), entropy);

        Assert.NotNull(outcome);
        Assert.Equal(SuccessLevel.Success, outcome!.Level); // 10 <= 45, a normal success
        Assert.Equal(1, entropy.DrawCount);
    }

    private static void AssertGrade(
        IReadOnlyList<Modifier> modifiers, bool expectsEasy, bool expectsDifficult, bool expectsImpossibleGate, bool expectsEmpty)
    {
        if (expectsEmpty)
        {
            Assert.Empty(modifiers);
            return;
        }

        Assert.Equal(expectsEasy, modifiers.OfType<DifficultyModifier>().Any(d => d.Direction == DifficultyDirection.Easier));
        Assert.Equal(expectsDifficult, modifiers.OfType<DifficultyModifier>().Any(d => d.Direction == DifficultyDirection.Harder));
        Assert.Equal(expectsImpossibleGate, modifiers.OfType<GateModifier>().Any(g => g.Kind == GateKind.Impossible));
    }
}
