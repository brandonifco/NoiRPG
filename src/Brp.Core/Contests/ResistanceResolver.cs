using Brp.Core.Primitives;
using Brp.Core.Randomness;

namespace Brp.Core.Contests;

/// <summary>
/// Resolves a single resistance roll into a <see cref="ResistanceOutcome"/>, per Ch 5: System,
/// "Resistance Rolls" and "The Resistance Table" (BRP ORC Content Document, p.129).
/// <para>
/// <strong>This does not use <c>Resolution.SkillResolver</c>.</strong> Ch 5, "Failure" (p.127)
/// explicitly carves resistance rolls out of the skill kernel's 96+ automatic-failure rule: "The
/// exception are resistance rolls, where a difference of 10 characteristic points is enough to
/// make only a roll of 00 a failure." The printed Resistance Table's own caption agrees: cells
/// run 05 to 95 with nothing printed beyond -- differences of 10+ points are automatic, not a
/// continuation of the percentage scale. Routing a 10-point-advantage resistance roll through
/// <c>SkillResolver</c> would wrongly fail it on a 96-99, where this rule says it succeeds.
/// </para>
/// </summary>
public static class ResistanceResolver
{
    /// <summary>
    /// Resolves a resistance roll against an already-drawn percentile result.
    /// </summary>
    /// <param name="active">
    /// The active characteristic -- "the party or force trying to influence the passive factor"
    /// (Ch 5, "Resistance Rolls", p.129).
    /// </param>
    /// <param name="passive">The passive characteristic being resisted.</param>
    /// <param name="roll">A percentile result in <c>[1, 100]</c>, with <c>00</c> as <c>100</c>.</param>
    /// <param name="policy">
    /// The resistance constants. Defaults to <see cref="ResistancePolicy.Standard"/>, the only
    /// supported configuration.
    /// </param>
    public static ResistanceOutcome Resolve(
        int active, int passive, int roll, ResistancePolicy? policy = null)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(roll, 1);
        policy ??= ResistancePolicy.Standard;
        ArgumentOutOfRangeException.ThrowIfGreaterThan(roll, policy.MaximumRoll);

        // Ch 5, "Resistance Rolls", p.129: "The base chance of a resistance roll equals 50% +
        // (active characteristic x 5) - (passive characteristic x 5)." Algebraically identical
        // to parity plus the per-point step times the raw difference; Percent floors at zero,
        // which only matters deep in the automatic-failure zone where this value is not used as
        // a roll target anyway (see below).
        var rawChance = policy.ParityChance
            + policy.PercentPerPointOfDifference * (active - passive);
        var chance = Percent.Of(rawChance);

        // Table caption, p.130: "Changes below 05% are in the range of automatic failure and
        // over 95% in the range of automatic success." This is the same linear formula, just
        // read off its own printed bounds -- not a separately-tuned threshold.
        var isAutomaticSuccess = rawChance > policy.AutomaticSuccessAbove;
        var isAutomaticFailure = rawChance < policy.AutomaticFailureBelow;

        bool succeeded;
        if (isAutomaticSuccess)
        {
            // Ch 5, "Failure", p.127: "The exception are resistance rolls, where a difference of
            // 10 characteristic points is enough to make only a roll of 00 a failure." Automatic
            // success is not unconditional -- it is unconditional except for the top roll, which
            // still fails. p.127 states this as a standing rule. The Resistance Rolls section on
            // p.129 separately offers relief in *both* directions as gamemaster-optional --
            // "your gamemaster may allow a roll of 01... to succeed or 00 to fail, respectively,
            // where results would otherwise be automatic" -- so the book states this same 00-fails
            // behavior twice: once as optional (p.129), once as a standing rule (p.127). We follow
            // p.127, the stricter and unconditional statement.
            succeeded = roll < policy.MaximumRoll;
        }
        else if (isAutomaticFailure)
        {
            // No standing-rule exception is stated for this side: p.127 only carves out the
            // 00-fails case for the automatic-success zone above. p.129's "gamemaster may allow a
            // roll of 01... to succeed... where results would otherwise be automatic" covers this
            // zone symmetrically, but only as an explicitly optional variant, never elevated to a
            // standing rule the way the 00-fails case is -- so the default here is unconditional
            // failure.
            succeeded = false;
        }
        else
        {
            // Ordinary resistance roll: "For success, roll 1D100 equal to or less than the
            // indicated number" (table caption, p.130).
            succeeded = roll <= chance.Value;
        }

        return new ResistanceOutcome(roll, active, passive, chance, isAutomaticSuccess, isAutomaticFailure, succeeded);
    }

    /// <summary>
    /// Resolves a resistance roll by drawing a fresh percentile result from
    /// <paramref name="entropy"/>.
    /// </summary>
    public static ResistanceOutcome Resolve(
        int active, int passive, IEntropySource entropy, ResistancePolicy? policy = null)
    {
        ArgumentNullException.ThrowIfNull(entropy);
        return Resolve(active, passive, entropy.NextD100(), policy);
    }
}
