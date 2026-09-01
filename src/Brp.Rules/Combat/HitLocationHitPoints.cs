namespace Brp.Rules.Combat;

/// <summary>
/// A character's running hit-point damage at each <see cref="HitLocation"/>, tracked alongside (not
/// instead of) the single-pool total (Ch 6: Combat, "Damage and hit Locations (Option)", p.156:
/// "Keep track of each wound and each location separately, but also keep a running total of all hit
/// point damage your character has suffered"). The maximums come from
/// <see cref="HitPointsByLocationCalculator"/>; the damage recorded here comes from
/// <see cref="HitLocationDamageResolver"/>.
/// </summary>
public sealed class HitLocationHitPoints
{
    private readonly HitPointsByLocation _maximums;
    private readonly Dictionary<HitLocation, int> _damageTaken = new()
    {
        [HitLocation.RightLeg] = 0,
        [HitLocation.LeftLeg] = 0,
        [HitLocation.Abdomen] = 0,
        [HitLocation.Chest] = 0,
        [HitLocation.RightArm] = 0,
        [HitLocation.LeftArm] = 0,
        [HitLocation.Head] = 0,
    };

    /// <summary>Creates a tracker from a character's computed per-location maximums.</summary>
    public HitLocationHitPoints(HitPointsByLocation maximums)
    {
        ArgumentNullException.ThrowIfNull(maximums);
        _maximums = maximums;
    }

    /// <summary>The maximum hit points at a location -- never reduced by damage.</summary>
    public int MaximumAt(HitLocation location) => _maximums.At(location);

    /// <summary>The cumulative applied damage recorded at a location so far.</summary>
    public int DamageTakenAt(HitLocation location) => _damageTaken[location];

    /// <summary>
    /// The location's remaining hit points (<see cref="MaximumAt"/> minus <see cref="DamageTakenAt"/>).
    /// May be negative -- like the single-pool total (Ch 2, p.13), nothing here floors at zero.
    /// </summary>
    public int RemainingAt(HitLocation location) => MaximumAt(location) - DamageTakenAt(location);

    /// <summary>Adds to a location's cumulative recorded damage.</summary>
    internal void RecordDamage(HitLocation location, int amount) => _damageTaken[location] += amount;
}
