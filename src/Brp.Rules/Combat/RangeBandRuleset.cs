namespace Brp.Rules.Combat;

/// <summary>
/// The data-defined thresholds and multipliers the range-band mechanic reads (AGENTS.md
/// invariant 7: rules values are data, not constants). Every value is sourced or marked as a
/// house interpretation on its own member below. Loaded from <c>range-band-ruleset.json</c> by
/// <c>Brp.Data.NoirRangeBandRuleset.Load()</c>.
/// <para>
/// Carries no weapon-class list: the throwing-weapon cutoff (see
/// <see cref="RangeBandResolver.IsBeyondThrowingCutoff"/>) is a per-weapon fact -- "is this a
/// small hand-thrown weapon" -- not a class-level one, per the post-review fix to Issue #21 (the
/// book's "Missile" weapon class, Ch 8 p.196, also contains mechanism-launched weapons such as
/// the sling and blowgun, which must not be cut off). This ruleset owns only the multiplier the
/// cutoff applies once a caller has established that fact.
/// </para>
/// </summary>
public sealed class RangeBandRuleset
{
    /// <summary>Creates a range-band ruleset from data-defined values.</summary>
    public RangeBandRuleset(
        int pointBlankDexDivisor,
        int mediumRangeMultiplier,
        int longRangeChanceNumerator,
        int longRangeChanceDenominator,
        int throwingCutoffMultiplier,
        int targetingEquipmentDampeningNumerator,
        int targetingEquipmentDampeningDenominator)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(pointBlankDexDivisor, 0);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(mediumRangeMultiplier, 0);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(longRangeChanceNumerator, 0);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(longRangeChanceDenominator, 0);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(throwingCutoffMultiplier, 0);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(targetingEquipmentDampeningNumerator, 0);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(targetingEquipmentDampeningDenominator, 0);

        PointBlankDexDivisor = pointBlankDexDivisor;
        MediumRangeMultiplier = mediumRangeMultiplier;
        LongRangeChanceNumerator = longRangeChanceNumerator;
        LongRangeChanceDenominator = longRangeChanceDenominator;
        ThrowingCutoffMultiplier = throwingCutoffMultiplier;
        TargetingEquipmentDampeningNumerator = targetingEquipmentDampeningNumerator;
        TargetingEquipmentDampeningDenominator = targetingEquipmentDampeningDenominator;
    }

    /// <summary>
    /// Ch 6: Combat, "Missile Weapons" (p.154): "Point Blank: If the target is within the
    /// attacker's DEX/3 meters (round up)... the skill is Easy." Corroborated word-for-word by
    /// Ch 7: Spot Rules, "Point-blank Range" (p.176) and "Extended Range" (p.171).
    /// </summary>
    public int PointBlankDexDivisor { get; }

    /// <summary>
    /// Ch 6 (p.154): "Medium Range: If the target is at double the weapon's standard listed
    /// range, the attack is Difficult." Ch 7, "Extended Range" (p.171) corroborates.
    /// <para>
    /// This is the only distance multiplier the ladder needs as data: Normal range is within the
    /// listed range (multiplier 1, not worth naming), and Long range is everything past this
    /// multiplier's boundary, with no upper cutoff the book ever states (see
    /// <see cref="RangeBandResolver.DetermineBand"/>'s remarks for why a separate "quadruple
    /// range" field would be dead configuration under that reading).
    /// </para>
    /// </summary>
    public int MediumRangeMultiplier { get; }

    /// <summary>
    /// Ch 6 (p.154) / Ch 7 (p.171): at long range the attack is "1/5 normal skill chance
    /// (equivalent to a special success, but if rolled, the result is a normal success)."
    /// Settled decision (Issue #21): this is an override against the character's current
    /// rating -- with permanent modifiers folded in first, per Ch 5 (p.132) -- the same
    /// one-fifth division <c>Brp.Core.Resolution.ResolutionPolicy.SpecialDivisor</c> already uses
    /// for a special success, not a multiplier stacked on top of any other penalty for the same
    /// shot. See <see cref="RangeBandOutcome.ExclusiveOverride"/>.
    /// </summary>
    public int LongRangeChanceNumerator { get; }

    /// <summary>See <see cref="LongRangeChanceNumerator"/>.</summary>
    public int LongRangeChanceDenominator { get; }

    /// <summary>
    /// Ch 7: Spot Rules, "Extended Range" (p.171): "Small hand-propelled weapons such as the
    /// throwing knife and the throwing axe have no chance to hit beyond double base range."
    /// Applied only to weapons a caller identifies as hand-thrown -- see
    /// <see cref="RangeBandResolver.IsBeyondThrowingCutoff"/> -- not to an entire weapon class.
    /// </summary>
    public int ThrowingCutoffMultiplier { get; }

    /// <summary>
    /// Ch 6 (p.154), "Targeting Gear": "Using long-range goggles, a scope, laser sight, or
    /// other targeting system divides range modifiers by 1/2 if one combat round is taken to
    /// aim." <strong>House interpretation</strong> of admittedly ambiguous phrasing: read
    /// literally, "divides ... by 1/2" would double a penalty rather than shrink it, which
    /// contradicts the passage's evident purpose (aiming with a scope helps the shot). This
    /// ruleset instead halves the <em>severity</em> of the range penalty -- the shortfall from
    /// an unmodified roll -- so a Difficult (÷2) medium-range shot becomes a ×3/4 penalty and a
    /// long-range 1/5 override becomes ×3/5, when one round is spent aiming with targeting
    /// equipment. See <c>docs/decisions/0014-range-bands.md</c>.
    /// </summary>
    public int TargetingEquipmentDampeningNumerator { get; }

    /// <summary>See <see cref="TargetingEquipmentDampeningNumerator"/>.</summary>
    public int TargetingEquipmentDampeningDenominator { get; }
}
