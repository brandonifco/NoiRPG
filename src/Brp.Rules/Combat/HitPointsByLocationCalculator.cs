using Brp.Core.Primitives;

namespace Brp.Rules.Combat;

/// <summary>
/// Derives per-location hit points from a total, per Ch 2: Characters, "Hit Points by Hit Location
/// (Option)" (p.14): "Use the following formula for humanoids, rounding up for each location:
/// Leg, Abdomen, Head: 1/3 total hit points. Chest: 4/10 total hit points. Arm: 1/4 total hit
/// points."
/// <para>
/// <strong>A printed-table erratum, not followed.</strong> The book also prints a lookup table of
/// this formula's results for totals 1-21 ("The humanoid hit point spread is provided below based
/// on Maximum Hit Points"). Verified against the PDF's glyph bounding boxes cell by cell: 44 of the
/// 45 printed cells across all three formulas match this closed form exactly, but the Arm column at
/// total hit points 16-17 prints a single value, 4, for both totals -- correct for 16
/// (ceiling(16/4) = 4) but not 17 (ceiling(17/4) = 5). The table is explicitly introduced as
/// "provided below based on" the formula (derived from it, not an independent source), and the
/// formula is what the prose instructs to "use" -- so this is the reverse of the usual
/// table-beats-prose case: the formula is normative and the one derived-table cell that disagrees
/// with it is the misprint. See <c>docs/decisions/0024-hit-locations.md</c> and
/// <c>HitPointsByLocationCalculatorTests</c>, which reproduces every one of the 45 printed cells
/// and separately pins this one anomaly.
/// </para>
/// </summary>
public static class HitPointsByLocationCalculator
{
    /// <summary>Computes every location's maximum hit points from a character's total.</summary>
    /// <param name="totalHitPoints">
    /// The character's total (<see cref="Core.Abilities.AbilitySet.MaximumHitPoints"/>).
    /// </param>
    /// <param name="ruleset">Supplies the three fractions.</param>
    public static HitPointsByLocation Compute(int totalHitPoints, HitLocationRuleset ruleset)
    {
        ArgumentNullException.ThrowIfNull(ruleset);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(totalHitPoints);

        var legAbdomenHead = Rounding.Divide(totalHitPoints, ruleset.LimbHeadAbdomenDivisor, RoundingMode.Up);

        // Chest pre-multiplies by the numerator (4) before dividing, unlike the other two
        // locations -- done in 64-bit arithmetic so that multiplication cannot overflow Int32
        // for totals beyond the printed 1-21 range (Rounding.Divide takes Int32 operands, so the
        // multiply-then-divide has to happen here, not inside it). Same ceiling-division formula
        // Rounding.Divide uses internally: (numerator + denominator - 1) / denominator.
        var chestNumeratorTotal = (long)totalHitPoints * ruleset.ChestNumerator;
        var chest = (int)((chestNumeratorTotal + ruleset.ChestDenominator - 1) / ruleset.ChestDenominator);

        var arm = Rounding.Divide(totalHitPoints, ruleset.ArmDivisor, RoundingMode.Up);

        return new HitPointsByLocation(
            RightLeg: legAbdomenHead,
            LeftLeg: legAbdomenHead,
            Abdomen: legAbdomenHead,
            Chest: chest,
            RightArm: arm,
            LeftArm: arm,
            Head: legAbdomenHead);
    }
}
