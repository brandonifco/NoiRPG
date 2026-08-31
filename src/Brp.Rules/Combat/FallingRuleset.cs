using Brp.Core.Dice;

namespace Brp.Rules.Combat;

/// <summary>
/// The data-defined values of Ch 7: Spot Rules, "Falling" (pp.171-172) (AGENTS.md invariant 7: rules
/// values are data, not constants). Loaded from <c>injury-ruleset.json</c> by
/// <c>Brp.Data.NoirInjuryRuleset.Load()</c>. See <c>docs/decisions/0019-injury-spot-rules.md</c>.
/// </summary>
public sealed class FallingRuleset
{
    /// <summary>Creates a falling ruleset from data-defined values.</summary>
    public FallingRuleset(
        DiceExpression baseDamagePerIncrement,
        int metersPerDamageIncrement,
        int forceMultiplier,
        int smallSizeThreshold,
        DiceExpression smallSizeReduction,
        int largeSizeThreshold,
        int largeSizeBand,
        DiceExpression largeSizeExtraDamage,
        int armorHalfProtectionMaxMeters,
        int armorProtectionNumerator,
        int armorProtectionDenominator)
    {
        ArgumentNullException.ThrowIfNull(baseDamagePerIncrement);
        ArgumentNullException.ThrowIfNull(smallSizeReduction);
        ArgumentNullException.ThrowIfNull(largeSizeExtraDamage);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(metersPerDamageIncrement);
        ArgumentOutOfRangeException.ThrowIfLessThan(forceMultiplier, 1);
        ArgumentOutOfRangeException.ThrowIfNegative(smallSizeThreshold);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(largeSizeBand);
        ArgumentOutOfRangeException.ThrowIfLessThan(largeSizeThreshold, smallSizeThreshold);
        ArgumentOutOfRangeException.ThrowIfNegative(armorHalfProtectionMaxMeters);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(armorProtectionNumerator);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(armorProtectionDenominator);

        BaseDamagePerIncrement = baseDamagePerIncrement;
        MetersPerDamageIncrement = metersPerDamageIncrement;
        ForceMultiplier = forceMultiplier;
        SmallSizeThreshold = smallSizeThreshold;
        SmallSizeReduction = smallSizeReduction;
        LargeSizeThreshold = largeSizeThreshold;
        LargeSizeBand = largeSizeBand;
        LargeSizeExtraDamage = largeSizeExtraDamage;
        ArmorHalfProtectionMaxMeters = armorHalfProtectionMaxMeters;
        ArmorProtectionNumerator = armorProtectionNumerator;
        ArmorProtectionDenominator = armorProtectionDenominator;
    }

    /// <summary>
    /// Ch 7, "Falling" (p.171): "A falling character takes 1D6 base damage for every three meters
    /// fallen." The dice pool contributed per <see cref="MetersPerDamageIncrement"/> of the fall.
    /// </summary>
    public DiceExpression BaseDamagePerIncrement { get; }

    /// <summary>
    /// Ch 7, "Falling" (p.171): "...for every three meters fallen." The distance one
    /// <see cref="BaseDamagePerIncrement"/> covers.
    /// </summary>
    public int MetersPerDamageIncrement { get; }

    /// <summary>
    /// Ch 7, "Falling" (p.171): "If thrown with considerable force, the dice rolled may be
    /// doubled." The factor the base distance dice count is multiplied by when thrown with force.
    /// </summary>
    public int ForceMultiplier { get; }

    /// <summary>
    /// Ch 7, "Falling" (p.171): "if SIZ is 5 or less, reduce the damage from falling by 1D6." A
    /// character whose SIZ is at or below this threshold takes <see cref="SmallSizeReduction"/> less.
    /// </summary>
    public int SmallSizeThreshold { get; }

    /// <summary>
    /// Ch 7, "Falling" (p.171): "reduce the damage from falling by 1D6." The amount subtracted from
    /// a small character's rolled falling damage.
    /// </summary>
    public DiceExpression SmallSizeReduction { get; }

    /// <summary>
    /// Ch 7, "Falling" (p.171): "adding an extra 1D6 damage if the character's SIZ is over 20." A
    /// character whose SIZ exceeds this threshold takes <see cref="LargeSizeExtraDamage"/> per band.
    /// </summary>
    public int LargeSizeThreshold { get; }

    /// <summary>
    /// Ch 7, "Falling" (p.171): "another 1D6 for every fraction of 20 after that." The width of one
    /// large-size band above <see cref="LargeSizeThreshold"/>; the number of bands started
    /// determines how many <see cref="LargeSizeExtraDamage"/> the fall adds.
    /// </summary>
    public int LargeSizeBand { get; }

    /// <summary>
    /// Ch 7, "Falling" (p.171): "adding an extra 1D6 damage... and another 1D6 for every fraction of
    /// 20 after that. This is cumulative with the modifier for force." The dice added per large-size
    /// band.
    /// </summary>
    public DiceExpression LargeSizeExtraDamage { get; }

    /// <summary>
    /// Ch 7, "Falling" (p.172): "Armor provides half protection against falling damage up to three
    /// meters." The maximum fall distance for which armor applies at all.
    /// </summary>
    public int ArmorHalfProtectionMaxMeters { get; }

    /// <summary>
    /// Ch 7, "Falling" (p.172): "half protection." Numerator of the fraction of the armor value that
    /// applies within <see cref="ArmorHalfProtectionMaxMeters"/> (1 of 2 = half).
    /// </summary>
    public int ArmorProtectionNumerator { get; }

    /// <summary>See <see cref="ArmorProtectionNumerator"/>.</summary>
    public int ArmorProtectionDenominator { get; }
}
