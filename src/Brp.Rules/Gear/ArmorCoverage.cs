using Brp.Rules.Combat;

namespace Brp.Rules.Gear;

/// <summary>
/// Resolves <see cref="ArmorDefinition.HitLocations"/>'s printed category strings ("Head", "Chest",
/// "Abdomen", "Arms", "Legs" -- Ch 8: Equipment, Modern Armor table, "Fits Locations" column, p.207)
/// against the seven granular <see cref="HitLocation"/> values the D20 hit-location table rolls (Ch
/// 6, p.145), per "Armor by Hit Location (Option)" (Ch 8, p.209): "Each type of armor in the armor
/// tables lists the hit locations it covers... use the armor value from the armor charts."
/// "Arms"/"Legs" are two-sided categories in the printed table but the D20 table rolls a specific
/// side, so both sides of a limb category are covered identically -- the book does not distinguish
/// left- and right-side armor coverage.
/// </summary>
public static class ArmorCoverage
{
    /// <summary>Whether <paramref name="armor"/> covers the given granular hit location.</summary>
    public static bool Covers(this ArmorDefinition armor, HitLocation location)
    {
        ArgumentNullException.ThrowIfNull(armor);
        return armor.HitLocations.Any(printed => Matches(printed, location));
    }

    /// <summary>
    /// The armor value at a struck location: the highest value among all worn armor pieces that
    /// cover it (Ch 8, p.209: "Your character can vary the type of armor they are wearing on each
    /// hit location... using the heaviest if these differ"), by attack type. Zero if no worn armor
    /// covers the location.
    /// </summary>
    /// <param name="location">The struck location.</param>
    /// <param name="isFirearm">
    /// Whether the attack is a firearm, selecting <see cref="ArmorValue.Firearms"/> over
    /// <see cref="ArmorValue.MeleeAndLowVelocity"/> (Ch 8, p.207, note 1).
    /// </param>
    /// <param name="wornArmor">The armor pieces the target is wearing.</param>
    public static int ArmorValueAt(HitLocation location, bool isFirearm, IEnumerable<ArmorDefinition> wornArmor)
    {
        ArgumentNullException.ThrowIfNull(wornArmor);

        return wornArmor
            .Where(armor => armor.Covers(location))
            .Select(armor => isFirearm ? armor.ArmorValue.Firearms : armor.ArmorValue.MeleeAndLowVelocity)
            .DefaultIfEmpty(0)
            .Max();
    }

    private static bool Matches(string printedCategory, HitLocation location) => printedCategory switch
    {
        "Head" => location == HitLocation.Head,
        "Chest" => location == HitLocation.Chest,
        "Abdomen" => location == HitLocation.Abdomen,
        "Arms" => location is HitLocation.LeftArm or HitLocation.RightArm,
        "Legs" => location is HitLocation.LeftLeg or HitLocation.RightLeg,
        _ => throw new ArgumentException(
            $"Unrecognized armor hit-location category '{printedCategory}'.", nameof(printedCategory)),
    };
}
