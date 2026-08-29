using Brp.Core.Primitives;
using Brp.Core.Randomness;
using Brp.Core.Resolution;

namespace Brp.Core.Contests;

/// <summary>
/// Resolves an <em>opposed skill roll</em> -- two ordinary skill rolls compared against each
/// other -- per Ch 5: System, "Opposed Skill Rolls" (BRP ORC Content Document, p.130-131; the
/// quote below crosses the page break): "When two skills are opposed, both characters roll
/// against their respective skills. The character that achieves the highest degree of success
/// wins the contest. However, if the loser's skill roll was successful, they modify the winner's
/// degree of success, shifting it downward one degree for every degree of success they achieve
/// above failure. If both parties achieve the same degree of success, the higher die roll wins
/// the contest, giving the advantage to characters with higher skill ratings."
/// <para>
/// Both rolls are ordinary skill rolls -- unlike a resistance roll, an opposed skill roll is not
/// exempt from the 96+ automatic-failure rule, so each side is resolved through
/// <see cref="SkillResolver"/> exactly as a solo skill roll would be. The book documents this
/// method as its default; it separately lists alternate opposed-roll systems (highest successful
/// result, opposed skill subtraction, the resistance table) as gamemaster options -- out of
/// scope here.
/// </para>
/// </summary>
public static class OpposedSkillResolver
{
    /// <summary>
    /// Resolves an opposed roll from two already-resolved skill rolls.
    /// </summary>
    /// <param name="participantA">The first participant's resolved skill roll.</param>
    /// <param name="participantB">The second participant's resolved skill roll.</param>
    /// <param name="adjudicator">
    /// Supplies the answer for contests the book leaves to gamemaster interpretation (see
    /// <see cref="OpposedRollDecisionId"/>). Defaults to <see cref="DefaultAdjudicator"/>.
    /// </param>
    public static OpposedRollResult Resolve(
        RollOutcome participantA, RollOutcome participantB, IAdjudicator? adjudicator = null)
    {
        ArgumentNullException.ThrowIfNull(participantA);
        ArgumentNullException.ThrowIfNull(participantB);
        adjudicator ??= new DefaultAdjudicator();

        if (participantA.Level == participantB.Level)
        {
            if (participantA.Level < SuccessLevel.Success)
            {
                // Tied on the *same* failing degree (both Fumble, or both Failure). This is the
                // one case the book truly leaves open -- see OpposedRollDecisionId.BothPartiesFailed
                // for why the tiebreak's stated rationale does not transfer here. It is not picked
                // silently.
                var bothFailedOutcome = adjudicator.Decide(
                    OpposedRollDecisionId.BothPartiesFailed, participantA, participantB);
                return new OpposedRollResult(participantA, participantB, bothFailedOutcome, null, 0);
            }

            // Same degree, both >= Success: "the higher die roll wins the contest, giving the
            // advantage to characters with higher skill ratings" (p.131). No degrade is mentioned
            // for this branch -- the winner keeps their rolled level as-is.
            if (participantA.Roll != participantB.Roll)
            {
                var aRollWins = participantA.Roll > participantB.Roll;
                return new OpposedRollResult(
                    participantA,
                    participantB,
                    aRollWins ? OpposedRollOutcome.ParticipantAWins : OpposedRollOutcome.ParticipantBWins,
                    participantA.Level,
                    0);
            }

            // Same degree and the identical roll: the stated tiebreak has nothing left to compare.
            // The book does not say what happens next. See OpposedRollDecisionId.RollsIdenticalAfterTiebreak.
            var tieOutcome = adjudicator.Decide(
                OpposedRollDecisionId.RollsIdenticalAfterTiebreak, participantA, participantB);
            return new OpposedRollResult(participantA, participantB, tieOutcome, null, 0);
        }

        // Different degrees: Ch 5, "Evaluating Success or Failure" (p.127) ranks all five degrees
        // on one ladder (Fumble < Failure < Success < Special < Critical), and "Opposed Skill
        // Rolls" (p.130) says the highest degree wins the contest over that same ladder -- so this
        // covers Failure beating Fumble exactly as mechanically as Special beating Success. No
        // adjudication is needed: the book is not silent here, it is silent only when the shared
        // degree is a tied failure (handled above).
        var aWins = participantA.Level > participantB.Level;
        var winner = aWins ? participantA : participantB;
        var loser = aWins ? participantB : participantA;

        // "if the loser's skill roll was successful, they modify the winner's degree of
        // success, shifting it downward one degree for every degree of success they achieve
        // above failure." Failure is the zero point of "degrees above failure", so a
        // Success loser (one degree above Failure) shifts the winner down by one, a Special
        // loser by two, a Critical loser by three. No degrade at all if the loser did not
        // succeed -- including when the loser merely Fumbled while the winner only Failed.
        var degrade = loser.Level >= SuccessLevel.Success
            ? (int)loser.Level - (int)SuccessLevel.Failure
            : 0;

        // The winner strictly outranks the loser here, and a loser can only earn a degrade
        // by having succeeded (>= Success, value 2), so the degraded result is provably never
        // pushed below Success -- the clamp is a defensive floor, not a reachable branch.
        var winningLevel = (SuccessLevel)Math.Max((int)SuccessLevel.Fumble, (int)winner.Level - degrade);

        return new OpposedRollResult(
            participantA,
            participantB,
            aWins ? OpposedRollOutcome.ParticipantAWins : OpposedRollOutcome.ParticipantBWins,
            winningLevel,
            degrade);
    }

    /// <summary>
    /// Resolves an opposed roll by drawing both sides' percentile results from
    /// <paramref name="entropy"/> and resolving each through <see cref="SkillResolver"/> before
    /// comparing them.
    /// </summary>
    public static OpposedRollResult Resolve(
        Percent baseChanceA,
        Percent effectiveChanceA,
        Percent baseChanceB,
        Percent effectiveChanceB,
        IEntropySource entropy,
        IAdjudicator? adjudicator = null,
        ResolutionPolicy? policy = null)
    {
        ArgumentNullException.ThrowIfNull(entropy);
        var participantA = SkillResolver.Resolve(baseChanceA, effectiveChanceA, entropy, policy);
        var participantB = SkillResolver.Resolve(baseChanceB, effectiveChanceB, entropy, policy);
        return Resolve(participantA, participantB, adjudicator);
    }
}
