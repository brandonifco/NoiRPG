namespace Brp.Rules.Gear;

/// <summary>
/// An armor type's protection value. Sourced: Ch 8: Equipment, Modern Armor table (p.207),
/// whose "AV" column sometimes prints a single number and sometimes a slash-separated pair
/// (note 1: "First value is vs. melee or low-velocity missile weapons; second value is vs.
/// firearms."). Both fields are always populated -- when the book prints one number, both
/// fields carry it, so callers never need to branch on which form the table used.
/// </summary>
/// <param name="MeleeAndLowVelocity">Armor points against melee and low-velocity missile weapons.</param>
/// <param name="Firearms">Armor points against firearms.</param>
public sealed record ArmorValue(int MeleeAndLowVelocity, int Firearms)
{
    /// <summary>Creates an armor value where the book printed one number for both columns.</summary>
    public static ArmorValue Flat(int value) => new(value, value);
}
