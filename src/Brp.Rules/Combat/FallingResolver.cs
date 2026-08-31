using Brp.Core.Contests;
using Brp.Core.Dice;
using Brp.Core.Primitives;
using Brp.Core.Randomness;

namespace Brp.Rules.Combat;

/// <summary>
/// Computes and applies Ch 7: Spot Rules, "Falling" (p.171) damage: a base dice pool by distance,
/// doubled if thrown with force, adjusted for a small or large faller's SIZ, then mitigated by
/// (half) armor and any gamemaster surface ruling, and finally applied to hit points through the
/// non-weapon overload of <see cref="DamageResolver.ApplyDamage(Core.Abilities.AbilitySet, Characters.WoundTrack, int, DamageRuleset, string)"/>.
/// One of the injury/effect spot rules (#96), the sibling of the situational-modifier spot rules
/// <see cref="SpotRuleResolver"/> (#50). See <c>docs/decisions/0019-injury-spot-rules.md</c>.
/// <para>
/// <strong>House reading of ambiguous prose (marked in ADR 0019):</strong> the book's large-size
/// clause -- "adding an extra 1D6 damage if the character's SIZ is over 20 and another 1D6 for
/// every fraction of 20 after that" -- is read as one extra die per started band of
/// <see cref="FallingRuleset.LargeSizeBand"/> above <see cref="FallingRuleset.LargeSizeThreshold"/>
/// (SIZ 21-40 = one die, 41-60 = two, ...), so the "over 20" die and the "fraction of 20" dice are
/// the same series rather than double-counted. Armor's "half protection... up to three meters" is
/// applied as half the armor value within that distance and <em>no</em> armor beyond it, the latter
/// being a house reading of the book's silence on armor for longer falls.
/// </para>
/// </summary>
public static class FallingResolver
{
    /// <summary>
    /// Rolls the pre-mitigation falling damage for a fall of <paramref name="metersFallen"/> by a
    /// character of the given <paramref name="size"/>. Consumes one entropy draw per die in the
    /// base pool, then the large-size dice, then the small-size reduction die -- in that order.
    /// </summary>
    /// <param name="metersFallen">The distance fallen, in meters. Non-negative.</param>
    /// <param name="size">The falling character's SIZ.</param>
    /// <param name="thrownWithForce">Whether the character was thrown with considerable force.</param>
    /// <param name="ruleset">The falling values.</param>
    /// <param name="entropy">The entropy source, per AGENTS.md invariant 5.</param>
    public static FallingDamageRoll RollFallingDamage(
        int metersFallen, int size, bool thrownWithForce, FallingRuleset ruleset, IEntropySource entropy)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(metersFallen);
        ArgumentNullException.ThrowIfNull(ruleset);
        ArgumentNullException.ThrowIfNull(entropy);

        var baseDiceCount = metersFallen / ruleset.MetersPerDamageIncrement;
        if (thrownWithForce)
        {
            baseDiceCount *= ruleset.ForceMultiplier;
        }

        var damage = RollPool(ruleset.BaseDamagePerIncrement, baseDiceCount, entropy);

        var largeSizeBands = 0;
        if (size > ruleset.LargeSizeThreshold)
        {
            // House reading: one band per started LargeSizeBand above the threshold.
            largeSizeBands = CeilingDivide(size - ruleset.LargeSizeThreshold, ruleset.LargeSizeBand);
            damage += RollPool(ruleset.LargeSizeExtraDamage, largeSizeBands, entropy);
        }

        var smallSizeApplied = size <= ruleset.SmallSizeThreshold;
        if (smallSizeApplied)
        {
            damage -= ruleset.SmallSizeReduction.Roll(entropy).Total;
        }

        return new FallingDamageRoll(
            Math.Max(0, damage), baseDiceCount, largeSizeBands, smallSizeApplied, thrownWithForce);
    }

    /// <summary>
    /// Mitigates a <see cref="FallingDamageRoll"/> by armor and a gamemaster surface ruling, giving
    /// the hit-point loss to apply. Armor applies at half value only within
    /// <see cref="FallingRuleset.ArmorHalfProtectionMaxMeters"/> (and not at all beyond it -- see
    /// the type remarks); the surface adjustment (<see cref="InjuryDecisionId.FallingSurface"/>) is
    /// then added, and the result floored at zero.
    /// </summary>
    public static int MitigateFallingDamage(
        FallingDamageRoll roll, int armorValue, int metersFallen, FallingSurfaceRuling surface, FallingRuleset ruleset)
    {
        ArgumentNullException.ThrowIfNull(roll);
        ArgumentNullException.ThrowIfNull(ruleset);
        ArgumentOutOfRangeException.ThrowIfNegative(armorValue);
        ArgumentOutOfRangeException.ThrowIfNegative(metersFallen);

        var effectiveArmor = metersFallen <= ruleset.ArmorHalfProtectionMaxMeters
            ? Rounding.Divide(
                armorValue * ruleset.ArmorProtectionNumerator, ruleset.ArmorProtectionDenominator, RoundingMode.Down)
            : 0;

        var afterArmor = Math.Max(0, roll.DamageBeforeMitigation - effectiveArmor);
        return Math.Max(0, afterArmor + surface.DamageAdjustment);
    }

    private static int RollPool(DiceExpression die, int count, IEntropySource entropy)
    {
        var total = 0;
        for (var i = 0; i < count; i++)
        {
            total += die.Roll(entropy).Total;
        }

        return total;
    }

    private static int CeilingDivide(int numerator, int denominator) => (numerator + denominator - 1) / denominator;
}

/// <summary>
/// The pre-mitigation result of a <see cref="FallingResolver.RollFallingDamage"/>, carrying the
/// rolled damage and the breakdown that produced it (for provenance and tests).
/// </summary>
/// <param name="DamageBeforeMitigation">The rolled falling damage before armor and surface, floored at zero.</param>
/// <param name="BaseDiceCount">The number of base distance dice rolled (already force-doubled if applicable).</param>
/// <param name="LargeSizeBands">The number of large-size extra dice added (0 for a non-large faller).</param>
/// <param name="SmallSizeReductionApplied">Whether the small-size reduction was subtracted.</param>
/// <param name="ThrownWithForce">Whether the base dice count was doubled for being thrown with force.</param>
public sealed record FallingDamageRoll(
    int DamageBeforeMitigation,
    int BaseDiceCount,
    int LargeSizeBands,
    bool SmallSizeReductionApplied,
    bool ThrownWithForce);
