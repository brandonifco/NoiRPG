using Brp.Core.Abilities;

namespace Brp.Rules.Combat;

/// <summary>
/// Routes a single blow's damage to a struck <see cref="HitLocation"/> and a character's total hit
/// points, per Ch 6: Combat, "Damage and hit Locations (Option)" (pp.156-157). A self-contained,
/// parallel path to <see cref="DamageResolver"/>'s single-pool Major Wounds handling -- the book
/// states the two optional systems "should not be used together" (p.156), a constraint
/// <c>docs/decisions/0021-major-wounds.md</c> already recorded against this issue. A caller chooses
/// one path or the other; this resolver does not touch <see cref="MajorWoundResolver"/> or
/// <see cref="DamageResolver"/>.
/// <para>
/// Does not roll the hit location or the weapon damage itself (<see cref="HitLocationResolver"/> and
/// <see cref="DamageResolver.RollDamage"/> respectively) -- this resolver's job starts once both the
/// struck location and the raw (pre-armor) damage number are known.
/// </para>
/// </summary>
public static class HitLocationDamageResolver
{
    private static readonly HashSet<HitLocation> Limbs =
    [
        HitLocation.RightArm, HitLocation.LeftArm, HitLocation.RightLeg, HitLocation.LeftLeg,
    ];

    /// <summary>
    /// Applies one blow's damage to <paramref name="location"/> and to <paramref name="target"/>'s
    /// total hit points.
    /// </summary>
    /// <param name="target">The character taking the damage.</param>
    /// <param name="locations">The character's per-location hit-point tracker.</param>
    /// <param name="location">The struck location.</param>
    /// <param name="incomingDamage">The damage rolled for the blow, before armor.</param>
    /// <param name="armorValue">
    /// The armor value that applies at <paramref name="location"/> (from
    /// <see cref="Gear.ArmorCoverage.ArmorValueAt"/>), subtracted from <paramref name="incomingDamage"/>
    /// before anything else (Ch 8: Equipment, "Armor by Hit Location (Option)", p.209: "To determine
    /// the armor value of each piece, use the armor value from the armor charts").
    /// </param>
    /// <param name="ruleset">Supplies the limb damage cap multiplier.</param>
    /// <param name="bypassLimbCap">
    /// <see langword="true"/> for the one printed exception to the limb cap: falling damage (Ch 7:
    /// Spot Rules, "Falling", p.172): "The entire damage done by the fall applies both to the rolled
    /// hit location and to the falling character's total hit points. This is an exception to the rule
    /// that a limb may take only twice its hit points in damage." Ignored for non-limb locations,
    /// which are never capped regardless.
    /// </param>
    public static HitLocationDamageResult ApplyDamage(
        AbilitySet target,
        HitLocationHitPoints locations,
        HitLocation location,
        int incomingDamage,
        int armorValue,
        HitLocationRuleset ruleset,
        bool bypassLimbCap = false)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(locations);
        ArgumentNullException.ThrowIfNull(ruleset);
        ArgumentOutOfRangeException.ThrowIfNegative(incomingDamage);
        ArgumentOutOfRangeException.ThrowIfNegative(armorValue);

        var rawDamage = Math.Max(0, incomingDamage - armorValue);
        var locationMaximum = locations.MaximumAt(location);

        var isCappedLimb = Limbs.Contains(location) && !bypassLimbCap;
        var cap = isCappedLimb ? locationMaximum * ruleset.LimbDamageCapMultiplier : int.MaxValue;
        var appliedDamage = Math.Min(rawDamage, cap);
        var capApplied = isCappedLimb && rawDamage > cap;

        locations.RecordDamage(location, appliedDamage);
        target.SetCurrentHitPoints(target.CurrentHitPoints - appliedDamage);

        var band = ClassifyBand(rawDamage, locationMaximum);

        return new HitLocationDamageResult(
            location, rawDamage, appliedDamage, capApplied,
            locations.RemainingAt(location), target.CurrentHitPoints, band);
    }

    private static HitLocationDamageBand ClassifyBand(int rawDamage, int locationMaximum)
    {
        if (rawDamage >= locationMaximum * 3)
        {
            return HitLocationDamageBand.EqualOrExceedsTripleLocationHitPoints;
        }

        if (rawDamage >= locationMaximum * 2)
        {
            return HitLocationDamageBand.EqualOrExceedsDoubleLocationHitPoints;
        }

        return rawDamage >= locationMaximum
            ? HitLocationDamageBand.EqualOrExceedsLocationHitPoints
            : HitLocationDamageBand.Unaffected;
    }
}
