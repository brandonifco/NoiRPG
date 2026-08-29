using Brp.Core.Resolution;

namespace Brp.Core.Contests;

/// <summary>
/// A named gamemaster-adjudication point: a decision the source book explicitly leaves to the
/// gamemaster's judgment rather than defining mechanically.
/// </summary>
public enum OpposedRollDecisionId
{
    /// <summary>
    /// Both participants in an opposed skill roll landed on the <em>same</em> failing degree --
    /// Fumble vs Fumble, or Failure vs Failure. This is <em>not</em> the general "nobody
    /// succeeded" case: Ch 5, "Evaluating Success or Failure" (p.127) ranks all five degrees
    /// worst-to-best with Fumble below Failure, and "Opposed Skill Rolls" (p.130) says the
    /// highest degree of success wins the contest over that same ladder -- so Failure vs Fumble
    /// is mechanically determined (Failure wins) and is resolved directly, never routed here.
    /// <para>
    /// What genuinely has no answer is two participants tied on the <em>same</em> failing
    /// degree. The book's tiebreak for a shared degree -- "the higher die roll wins the contest,
    /// giving the advantage to characters with higher skill ratings" (p.131) -- relies on a
    /// roll-under system where a higher roll (while still within a band) reflects a higher
    /// skill. That rationale inverts for two failures: a higher roll there is simply a worse
    /// miss, not a sign of higher skill, so the stated reason for the tiebreak gives no guidance
    /// about who "wins" a tie between two failures. That is the actual gap the book leaves to
    /// the table, and it is routed here instead of picked silently.
    /// See <see cref="Contests.OpposedSkillResolver"/>.
    /// </para>
    /// </summary>
    BothPartiesFailed,

    /// <summary>
    /// Both participants achieved the same degree of success and rolled the identical
    /// percentile result. The book's stated tiebreak -- "the higher die roll wins the contest"
    /// (p.131) -- has nothing left to compare in this case; the book does not say what happens
    /// next.
    /// </summary>
    RollsIdenticalAfterTiebreak,
}

/// <summary>
/// The result of a gamemaster-adjudication decision: who, if anyone, prevails.
/// </summary>
public enum OpposedRollOutcome
{
    /// <summary>The first participant prevails.</summary>
    ParticipantAWins,

    /// <summary>The second participant prevails.</summary>
    ParticipantBWins,

    /// <summary>Neither participant prevails.</summary>
    NoContest,
}

/// <summary>
/// A gamemaster-discretion decision point, modeled as a first-class port rather than an
/// omission -- the source book says "at the gamemaster's discretion" throughout, and a rules
/// engine that hardcodes those calls silently is lying about what it implements. A GM tool can
/// prompt a human through this interface; an unattended simulation or video game can supply an
/// authored policy; tests can supply a deterministic stub. See
/// <c>engine-implementation-plan.md</c>, decision D5.
/// </summary>
public interface IAdjudicator
{
    /// <summary>
    /// Decides the outcome of a contest the rules do not resolve mechanically. Implementations
    /// may consult anything outside the two roll outcomes themselves (narrative stakes, a human
    /// gamemaster, a house rule); the caller only needs an answer.
    /// </summary>
    OpposedRollOutcome Decide(
        OpposedRollDecisionId decisionId, RollOutcome participantA, RollOutcome participantB);
}

/// <summary>
/// The documented default policy for every <see cref="OpposedRollDecisionId"/>: nobody
/// prevails. Chosen because it is the most narratively neutral answer to "the book does not say"
/// -- it asserts nothing the book didn't -- and it is what a table typically means by "you both
/// fail" or "call it a draw." Callers with a house rule (or a human gamemaster) should supply
/// their own <see cref="IAdjudicator"/> instead of relying on this default.
/// </summary>
public sealed class DefaultAdjudicator : IAdjudicator
{
    /// <inheritdoc />
    public OpposedRollOutcome Decide(
        OpposedRollDecisionId decisionId, RollOutcome participantA, RollOutcome participantB) =>
        OpposedRollOutcome.NoContest;
}
