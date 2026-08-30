namespace Brp.Rules.Combat;

/// <summary>
/// One row of the movement-distance table that fractions a combatant's effective DEX rank, per
/// Ch 6: Combat, "Move" (p.144): "Moving between 6-15 meters means that your character acts at
/// 1/2 their normal DEX rank. Moving between 16-29 meters in a combat round means that your
/// character acts at 1/4 their normal DEX rank."
/// </summary>
/// <param name="MinMeters">The tier's lower bound in meters, inclusive.</param>
/// <param name="MaxMeters">The tier's upper bound in meters, inclusive.</param>
/// <param name="FractionNumerator">The numerator of the DEX-rank fraction this tier applies.</param>
/// <param name="FractionDenominator">The denominator of the DEX-rank fraction this tier applies.</param>
public readonly record struct MovementTier(
    int MinMeters, int MaxMeters, int FractionNumerator, int FractionDenominator);
