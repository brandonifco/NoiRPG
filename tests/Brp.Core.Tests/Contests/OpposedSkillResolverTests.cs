using Brp.Core.Contests;
using Brp.Core.Primitives;
using Brp.Core.Resolution;
using Brp.Core.Tests.Dice;

namespace Brp.Core.Tests.Contests;

/// <summary>
/// Exercises <see cref="OpposedSkillResolver"/> against Ch 5: System, "Opposed Skill Rolls"
/// (BRP ORC Content Document, p.130-131): the highest-degree-wins rule, the degrade rule, the
/// same-degree tiebreak, and the gamemaster-adjudication points the book leaves undefined.
/// Every one of the 5x5 = 25 possible degree pairings is covered, not a sample -- 20 by
/// direct/degraded win (this includes Failure beating Fumble, which the book resolves
/// mechanically -- see <see cref="OpposedRollDecisionId.BothPartiesFailed"/>), 3 by the roll
/// tiebreak, and 2 by both-parties-failed adjudication (only a tied Fumble-vs-Fumble or
/// Failure-vs-Failure, where the book is genuinely silent).
/// </summary>
public class OpposedSkillResolverTests
{
    private static RollOutcome Roll(int roll, SuccessLevel level) =>
        new(roll, Percent.Of(50), Percent.Of(50), Percent.Of(2), Percent.Of(10), Percent.Of(96), level);

    /// <summary>
    /// Every degree pairing where the book's plain highest-degree-wins rule decides it outright
    /// (loser did not succeed, so no degrade applies) or where the degrade rule shifts the
    /// winner's degree down by the loser's degrees above Failure.
    /// </summary>
    public static TheoryData<SuccessLevel, SuccessLevel, bool, SuccessLevel, int> DecisiveOrDegradedPairings() => new()
    {
        // levelA, levelB, aWins, expectedWinningLevel, expectedDegrade

        // A fails outright (Fumble or Failure), B succeeds: B wins outright, no degrade.
        { SuccessLevel.Fumble, SuccessLevel.Success, false, SuccessLevel.Success, 0 },
        { SuccessLevel.Fumble, SuccessLevel.Special, false, SuccessLevel.Special, 0 },
        { SuccessLevel.Fumble, SuccessLevel.Critical, false, SuccessLevel.Critical, 0 },
        { SuccessLevel.Failure, SuccessLevel.Success, false, SuccessLevel.Success, 0 },
        { SuccessLevel.Failure, SuccessLevel.Special, false, SuccessLevel.Special, 0 },
        { SuccessLevel.Failure, SuccessLevel.Critical, false, SuccessLevel.Critical, 0 },

        // Mirror image: B fails outright, A succeeds.
        { SuccessLevel.Success, SuccessLevel.Fumble, true, SuccessLevel.Success, 0 },
        { SuccessLevel.Special, SuccessLevel.Fumble, true, SuccessLevel.Special, 0 },
        { SuccessLevel.Critical, SuccessLevel.Fumble, true, SuccessLevel.Critical, 0 },
        { SuccessLevel.Success, SuccessLevel.Failure, true, SuccessLevel.Success, 0 },
        { SuccessLevel.Special, SuccessLevel.Failure, true, SuccessLevel.Special, 0 },
        { SuccessLevel.Critical, SuccessLevel.Failure, true, SuccessLevel.Critical, 0 },

        // Both succeed, different degrees: winner's degree shifts down by the loser's degrees
        // above Failure (Success = 1, Special = 2, Critical = 3).
        { SuccessLevel.Success, SuccessLevel.Special, false, SuccessLevel.Success, 1 },
        { SuccessLevel.Success, SuccessLevel.Critical, false, SuccessLevel.Special, 1 },
        { SuccessLevel.Special, SuccessLevel.Success, true, SuccessLevel.Success, 1 },
        { SuccessLevel.Special, SuccessLevel.Critical, false, SuccessLevel.Success, 2 },
        { SuccessLevel.Critical, SuccessLevel.Success, true, SuccessLevel.Special, 1 },
        { SuccessLevel.Critical, SuccessLevel.Special, true, SuccessLevel.Success, 2 },

        // Different failing degrees: the book ranks Fumble below Failure on the same ladder it
        // compares successes on (Ch 5, "Evaluating Success or Failure", p.127), and "highest
        // degree wins" (p.130) does not carve out an exception for failing degrees. Failure
        // mechanically beats Fumble, with no degrade (the loser never succeeded).
        { SuccessLevel.Failure, SuccessLevel.Fumble, true, SuccessLevel.Failure, 0 },
        { SuccessLevel.Fumble, SuccessLevel.Failure, false, SuccessLevel.Failure, 0 },
    };

    [Theory]
    [MemberData(nameof(DecisiveOrDegradedPairings))]
    public void Highest_degree_wins_with_the_degrade_rule_applied(
        SuccessLevel levelA, SuccessLevel levelB, bool aWins, SuccessLevel expectedWinningLevel, int expectedDegrade)
    {
        // Roll values chosen arbitrarily but distinct; this theory is about degree, not roll.
        var a = Roll(roll: 40, levelA);
        var b = Roll(roll: 41, levelB);

        var result = OpposedSkillResolver.Resolve(a, b);

        Assert.Equal(
            aWins ? OpposedRollOutcome.ParticipantAWins : OpposedRollOutcome.ParticipantBWins,
            result.Outcome);
        Assert.Equal(expectedWinningLevel, result.WinningLevel);
        Assert.Equal(expectedDegrade, result.DegradeApplied);
    }

    [Theory]
    [InlineData(SuccessLevel.Success)]
    [InlineData(SuccessLevel.Special)]
    [InlineData(SuccessLevel.Critical)]
    public void Same_degree_is_broken_by_the_higher_die_roll(SuccessLevel level)
    {
        var higherRollWins = OpposedSkillResolver.Resolve(Roll(70, level), Roll(30, level));
        Assert.Equal(OpposedRollOutcome.ParticipantAWins, higherRollWins.Outcome);
        Assert.Equal(level, higherRollWins.WinningLevel);
        Assert.Equal(0, higherRollWins.DegradeApplied);

        var otherSideHigherRollWins = OpposedSkillResolver.Resolve(Roll(30, level), Roll(70, level));
        Assert.Equal(OpposedRollOutcome.ParticipantBWins, otherSideHigherRollWins.Outcome);
    }

    /// <summary>
    /// Only a tied failing degree is genuinely undefined by the book -- Fumble vs Fumble, or
    /// Failure vs Failure. Failure vs Fumble (and its mirror) is mechanically decided by the
    /// highest-degree-wins rule and is covered by <see cref="DecisiveOrDegradedPairings"/>
    /// instead; see <see cref="OpposedRollDecisionId.BothPartiesFailed"/> for why.
    /// </summary>
    public static TheoryData<SuccessLevel, SuccessLevel> BothPartiesFailedPairings() => new()
    {
        { SuccessLevel.Fumble, SuccessLevel.Fumble },
        { SuccessLevel.Failure, SuccessLevel.Failure },
    };

    [Theory]
    [MemberData(nameof(BothPartiesFailedPairings))]
    public void Both_parties_failing_is_routed_to_the_adjudicator_not_decided_silently(
        SuccessLevel levelA, SuccessLevel levelB)
    {
        var spy = new SpyAdjudicator(OpposedRollOutcome.NoContest);

        var result = OpposedSkillResolver.Resolve(Roll(97, levelA), Roll(98, levelB), spy);

        Assert.Equal(OpposedRollDecisionId.BothPartiesFailed, Assert.Single(spy.Decisions));
        Assert.Equal(OpposedRollOutcome.NoContest, result.Outcome);
        Assert.Null(result.WinningLevel);
        Assert.Equal(0, result.DegradeApplied);
    }

    [Fact]
    public void Default_adjudicator_calls_both_parties_failed_no_contest()
    {
        // A genuine tie on a failing degree -- not Failure vs Fumble, which is now resolved
        // mechanically (Failure wins) per the fix to OpposedRollDecisionId.BothPartiesFailed.
        var result = OpposedSkillResolver.Resolve(
            Roll(97, SuccessLevel.Failure), Roll(100, SuccessLevel.Failure));

        Assert.Equal(OpposedRollOutcome.NoContest, result.Outcome);
    }

    [Fact]
    public void A_supplied_adjudicator_can_override_the_both_failed_default()
    {
        var spy = new SpyAdjudicator(OpposedRollOutcome.ParticipantAWins);

        var result = OpposedSkillResolver.Resolve(
            Roll(97, SuccessLevel.Failure), Roll(98, SuccessLevel.Failure), spy);

        Assert.Equal(OpposedRollOutcome.ParticipantAWins, result.Outcome);
    }

    [Fact]
    public void Identical_degree_and_identical_roll_is_also_routed_to_the_adjudicator()
    {
        var spy = new SpyAdjudicator(OpposedRollOutcome.NoContest);

        var result = OpposedSkillResolver.Resolve(
            Roll(40, SuccessLevel.Success), Roll(40, SuccessLevel.Success), spy);

        Assert.Equal(OpposedRollDecisionId.RollsIdenticalAfterTiebreak, Assert.Single(spy.Decisions));
        Assert.Equal(OpposedRollOutcome.NoContest, result.Outcome);
        Assert.Null(result.WinningLevel);
    }

    [Fact]
    public void Entropy_overload_draws_both_rolls_and_resolves_through_SkillResolver()
    {
        // Participant A: chance 90, roll 50 -> Success (above the special band, at or below chance).
        // Participant B: chance 20, roll 50 -> an ordinary Failure (above chance, below 96).
        var entropy = new FixedEntropySource(50, 50);

        var result = OpposedSkillResolver.Resolve(
            Percent.Of(90), Percent.Of(90),
            Percent.Of(20), Percent.Of(20),
            entropy);

        Assert.Equal(SuccessLevel.Success, result.ParticipantA.Level);
        Assert.Equal(SuccessLevel.Failure, result.ParticipantB.Level);
        Assert.Equal(OpposedRollOutcome.ParticipantAWins, result.Outcome);
        Assert.Equal(2, entropy.DrawCount);
    }

    private sealed class SpyAdjudicator(OpposedRollOutcome toReturn) : IAdjudicator
    {
        public List<OpposedRollDecisionId> Decisions { get; } = [];

        public OpposedRollOutcome Decide(
            OpposedRollDecisionId decisionId, RollOutcome participantA, RollOutcome participantB)
        {
            Decisions.Add(decisionId);
            return toReturn;
        }
    }
}
