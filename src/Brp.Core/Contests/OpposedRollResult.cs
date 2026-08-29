using Brp.Core.Resolution;

namespace Brp.Core.Contests;

/// <summary>
/// The full record of one resolved opposed skill roll, per Ch 5: System, "Opposed Skill Rolls"
/// (BRP ORC Content Document, p.130). Carries both participants' original
/// <see cref="Resolution.RollOutcome"/>s unmodified -- <see cref="WinningLevel"/> is the
/// separate, possibly-degraded degree the contest actually resolves to, so the record can
/// explain both "what each side rolled" and "what the contest decided" without collapsing one
/// into the other.
/// </summary>
/// <param name="ParticipantA">The first participant's own resolved skill roll, unmodified.</param>
/// <param name="ParticipantB">The second participant's own resolved skill roll, unmodified.</param>
/// <param name="Outcome">Who, if anyone, prevails.</param>
/// <param name="WinningLevel">
/// The prevailing side's degree of success after the degrade rule is applied, or
/// <see langword="null"/> when <see cref="Outcome"/> is <see cref="OpposedRollOutcome.NoContest"/>.
/// </param>
/// <param name="DegradeApplied">
/// The number of degrees <see cref="WinningLevel"/> was shifted down from the winner's own
/// rolled level, per Ch 5, "Opposed Skill Rolls", p.130-131 (the sentence crosses the page
/// break): "if the loser's skill roll was successful, they modify the winner's degree of
/// success, shifting it downward one degree for every degree of success they achieve above
/// failure." Zero when the loser did not succeed, or when the contest was decided by the
/// tiebreak rule instead.
/// </param>
public sealed record OpposedRollResult(
    RollOutcome ParticipantA,
    RollOutcome ParticipantB,
    OpposedRollOutcome Outcome,
    SuccessLevel? WinningLevel,
    int DegradeApplied);
