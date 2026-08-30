using Brp.Rules.Gear;

namespace Brp.Rules.Combat;

/// <summary>
/// The data-defined thresholds, multipliers, and weapon classes the range-band mechanic reads
/// (AGENTS.md invariant 7: rules values are data, not constants). Every value is sourced or
/// marked as a house interpretation on its own member below. Loaded from
/// <c>range-band-ruleset.json</c> by <c>Brp.Data.NoirRangeBandRuleset.Load()</c>.
/// </summary>
public sealed class RangeBandRuleset
{
    /// <summary>Creates a range-band ruleset from data-defined values.</summary>
    public RangeBandRuleset(
        int pointBlankDexDivisor,
        int mediumRangeMultiplier,
        int longRangeMultiplier,
        int longRangeChanceNumerator,
        int longRangeChanceDenominator,
        int throwingCutoffMultiplier,
        IReadOnlyList<WeaponClass> throwingWeaponClasses,
        int targetingEquipmentDampeningNumerator,
        int targetingEquipmentDampeningDenominator)
    {
        ArgumentNullException.ThrowIfNull(throwingWeaponClasses);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(pointBlankDexDivisor, 0);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(mediumRangeMultiplier, 0);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(longRangeMultiplier, mediumRangeMultiplier);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(longRangeChanceNumerator, 0);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(longRangeChanceDenominator, 0);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(throwingCutoffMultiplier, 0);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(targetingEquipmentDampeningNumerator, 0);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(targetingEquipmentDampeningDenominator, 0);

        PointBlankDexDivisor = pointBlankDexDivisor;
        MediumRangeMultiplier = mediumRangeMultiplier;
        LongRangeMultiplier = longRangeMultiplier;
        LongRangeChanceNumerator = longRangeChanceNumerator;
        LongRangeChanceDenominator = longRangeChanceDenominator;
        ThrowingCutoffMultiplier = throwingCutoffMultiplier;
        ThrowingWeaponClasses = throwingWeaponClasses;
        TargetingEquipmentDampeningNumerator = targetingEquipmentDampeningNumerator;
        TargetingEquipmentDampeningDenominator = targetingEquipmentDampeningDenominator;
    }

    /// <summary>
    /// Ch 6: Combat, "Missile Weapons" (p.153): "Point Blank: If the target is within the
    /// attacker's DEX/3 meters (round up)... the skill is Easy." Corroborated word-for-word by
    /// Ch 7: Spot Rules, "Point-blank Range" (p.175) and "Extended Range" (p.170).
    /// </summary>
    public int PointBlankDexDivisor { get; }

    /// <summary>
    /// Ch 6 (p.153): "Medium Range: If the target is at double the weapon's standard listed
    /// range, the attack is Difficult." Ch 7, "Extended Range" (p.170) corroborates.
    /// </summary>
    public int MediumRangeMultiplier { get; }

    /// <summary>
    /// Ch 6 (p.153): "Long Range: If the target is at quadruple the weapon's standard listed
    /// range..." Ch 7 (p.170) corroborates: "at long range (four times basic range)".
    /// </summary>
    public int LongRangeMultiplier { get; }

    /// <summary>
    /// Ch 6 (p.153) / Ch 7 (p.170): at long range the attack is "1/5 normal skill chance
    /// (equivalent to a special success, but if rolled, the result is a normal success)."
    /// Settled decision (Issue #21): this is an override against the character's current,
    /// otherwise-unmodified rating -- the same one-fifth division
    /// <c>Brp.Core.Resolution.ResolutionPolicy.SpecialDivisor</c> already uses for a special
    /// success -- not a multiplier stacked on top of any other penalty for the same shot.
    /// </summary>
    public int LongRangeChanceNumerator { get; }

    /// <summary>See <see cref="LongRangeChanceNumerator"/>.</summary>
    public int LongRangeChanceDenominator { get; }

    /// <summary>
    /// Ch 7: Spot Rules, "Extended Range" (p.170): "Small hand-propelled weapons such as the
    /// throwing knife and the throwing axe have no chance to hit beyond double base range."
    /// </summary>
    public int ThrowingCutoffMultiplier { get; }

    /// <summary>
    /// The weapon classes <see cref="ThrowingCutoffMultiplier"/> applies to. Ch 8: Equipment,
    /// "Weapon Classes" (p.196) files the throwing knife and throwing axe under the "Missile"
    /// class alongside the blowgun, bola, boomerang, dagger, dart, hand axe, javelin, shuriken,
    /// and sling -- a weapon-class rule, not a distance tier, per the settled decision on #21.
    /// </summary>
    public IReadOnlyList<WeaponClass> ThrowingWeaponClasses { get; }

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
