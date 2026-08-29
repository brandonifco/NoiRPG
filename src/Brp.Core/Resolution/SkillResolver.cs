using Brp.Core.Primitives;
using Brp.Core.Randomness;

namespace Brp.Core.Resolution;

/// <summary>
/// Resolves a single <em>skill or action</em> roll into a <see cref="RollOutcome"/>, per
/// Ch 5: System, "Evaluating Success or Failure", "Skill Rolls", and the Skill Results
/// Table (BRP ORC Content Document, p.127-128). Combat attacks, experience checks, chases,
/// Passions, and Sanity all funnel through it.
/// <para>
/// <strong>Resistance rolls do not.</strong> Ch 5, "Failure" (p.127) exempts them from the
/// 96+ rule: with a ten-point characteristic advantage only a roll of 00 fails, and the
/// printed Resistance Table runs 05 to 95 with no entry beyond. Routing a resistance roll
/// through <see cref="Resolve(Percent, Percent, int, ResolutionPolicy?)"/> would wrongly
/// fail it on a 96-99 -- for example a 100% chance with a roll of 96 returns
/// <see cref="SuccessLevel.Failure"/> here, where the resistance rule says it succeeds.
/// Resistance is a separate path and is its own Issue.
/// </para>
/// </summary>
public static class SkillResolver
{
    /// <summary>
    /// Resolves an action roll against an already-drawn percentile result.
    /// </summary>
    /// <param name="baseChance">
    /// The skill's unmodified base chance. Used only for the 5%-floor rule (Ch 5, "Skill
    /// Rolls", p.128): "Any skill which normally has a base chance of 5% or higher always
    /// succeeds on a roll of 01-05... even if difficulty, conditional modifiers, or other
    /// factors reduce the skill rating below 5%." Note the floor is keyed on this
    /// <em>base</em> value, not on <paramref name="effectiveChance"/> -- that is the entire
    /// content of the rule.
    /// </param>
    /// <param name="effectiveChance">
    /// The modified chance of success to resolve the roll against. Where this comes from
    /// (situational modifiers, difficulty) is a later Issue's concern; here it is an input.
    /// </param>
    /// <param name="roll">A percentile result in <c>[1, 100]</c>, with <c>00</c> as <c>100</c>.</param>
    /// <param name="policy">
    /// The grading constants. Defaults to <see cref="ResolutionPolicy.Standard"/>, which holds
    /// the values printed in the book and is the only supported configuration.
    /// </param>
    public static RollOutcome Resolve(
        Percent baseChance, Percent effectiveChance, int roll, ResolutionPolicy? policy = null)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(roll, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(roll, ResolutionPolicy.MaximumRoll);

        policy ??= ResolutionPolicy.Standard;
        var chance = effectiveChance.Value;

        // Ch 5, "Critical Success" / "Special Success": 1/20th and 1/5th of the rating.
        // The printed table only ever steps a threshold up at a row boundary (e.g. chance 20
        // and chance 21 both round to critical=1 and critical=2 respectively), which is what
        // rounding up (ceiling) reproduces; rounding down would put the step one row early.
        var criticalThreshold = policy.CriticalThreshold(effectiveChance);
        var specialThreshold = policy.SpecialThreshold(effectiveChance);

        // Skill Results Table: the fumble band always ends at 100 (printed 00) and starts at
        // 96, narrowing by one for every step the critical threshold takes, down to the
        // single value 100 once critical reaches 5 (effective chance 81+). Clamping the
        // critical threshold to at least 1 here anchors the start at 96 even at chance 0,
        // matching the table's lowest printed row (01-05) rather than drifting below it.
        var fumbleThreshold = policy.FumbleThreshold(effectiveChance);

        var level = DetermineLevel(
            policy, baseChance.Value, chance, roll, criticalThreshold, specialThreshold, fumbleThreshold);

        return new RollOutcome(
            roll,
            baseChance,
            effectiveChance,
            Percent.Of(criticalThreshold),
            Percent.Of(specialThreshold),
            Percent.Of(fumbleThreshold),
            level);
    }

    /// <summary>
    /// Resolves an action roll by drawing a fresh percentile result from
    /// <paramref name="entropy"/>.
    /// </summary>
    public static RollOutcome Resolve(
        Percent baseChance, Percent effectiveChance, IEntropySource entropy, ResolutionPolicy? policy = null)
    {
        ArgumentNullException.ThrowIfNull(entropy);
        return Resolve(baseChance, effectiveChance, entropy.NextD100(), policy);
    }

    private static SuccessLevel DetermineLevel(
        ResolutionPolicy policy,
        int baseChance,
        int effectiveChance,
        int roll,
        int criticalThreshold,
        int specialThreshold,
        int fumbleThreshold)
    {
        // Ch 5, "Failure": "no matter how high the modified base chance, rolls fail on
        // results of 96 or higher." The same paragraph exempts resistance rolls, which is
        // why this type is scoped to skill and action rolls -- see the remarks on the class.
        // Checked first and unconditionally: it caps success at 95
        // regardless of how large the effective chance is. Ratings above 100% are supported
        // (the table continues past 120), so without this check a roll of, say, 97 against a
        // 150% chance would otherwise read as an ordinary success.
        if (roll >= policy.AlwaysFailsAtOrAbove)
        {
            // Ch 5, "Fumble": "A roll of 00 is always a fumble, no matter what the skill
            // rating is." fumbleThreshold is never greater than 100, so roll == 100 (00)
            // always satisfies this and falls through to Fumble.
            return roll >= fumbleThreshold ? SuccessLevel.Fumble : SuccessLevel.Failure;
        }

        // Skill Results Table, introductory note: "Whenever a roll result is in the range of
        // both a critical and special success, the results of the critical success... should
        // be applied, not both." The critical band sits inside the special band (1/20th <=
        // 1/5th), so testing it first gives exactly that precedence without double-counting.
        if (roll <= criticalThreshold)
        {
            return SuccessLevel.Critical;
        }

        if (roll <= specialThreshold)
        {
            return SuccessLevel.Special;
        }

        if (roll <= effectiveChance)
        {
            return SuccessLevel.Success;
        }

        // Ch 5, "Skill Rolls": the 5%-floor. Only reachable once an ordinary success against
        // the effective chance has already failed, and it grants a plain Success -- the book
        // says "always succeeds", nothing about upgrading to Special or Critical.
        if (baseChance >= policy.MinimumSuccessFloorChance
            && roll <= policy.MinimumSuccessFloorRoll)
        {
            return SuccessLevel.Success;
        }

        return SuccessLevel.Failure;
    }
}
