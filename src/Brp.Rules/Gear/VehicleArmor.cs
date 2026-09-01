namespace Brp.Rules.Gear;

/// <summary>
/// A vehicle's armor rating. Sourced: Ch 8: Equipment, "Vehicles", the "Armor:" term definition
/// (p.217): "The vehicle's general armor value and protection it provides to crew or passengers.
/// Usually, attacks on passengers are through a window or open section of the cabin. If these two
/// numbers are different, they are expressed as two values separated by a slash." This is a
/// distinct pair from <see cref="ArmorValue"/> (the Modern Armor table's melee-vs-firearms split,
/// p.207) -- the two tables print a slash-separated pair with different meanings, and reusing
/// <see cref="ArmorValue"/>'s <c>Melee</c>/<c>Firearms</c> fields here would mislabel the vehicle
/// table's numbers.
/// </summary>
/// <param name="GeneralArmor">
/// The vehicle's own armor value -- what its hull/body/structure absorbs before hit points are
/// spent (see the Vehicle Damage rule, "Chases", p.220).
/// </param>
/// <param name="OccupantProtection">
/// The protection the vehicle affords to crew or passengers riding inside it, typically lower
/// than <see cref="GeneralArmor"/> because occupants are exposed through windows or an open
/// cabin section.
/// </param>
public sealed record VehicleArmor(int GeneralArmor, int OccupantProtection)
{
    /// <summary>Creates a vehicle armor pair where the book printed one number for both.</summary>
    public static VehicleArmor Flat(int value) => new(value, value);
}
