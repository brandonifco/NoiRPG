using Brp.Rules.Combat;

namespace Brp.Rules.Gear;

/// <summary>
/// Resolves <see cref="ArmorDefinition.HitLocations"/>'s printed category strings -- "Head",
/// "Chest", "Abdomen", "Arms", "Legs", "All", and "All but head" -- against the seven granular
/// <see cref="HitLocation"/> values the D20 hit-location table rolls (Ch 6, p.145). The category
/// vocabulary is the union of every "Fits Locations" cell printed across the Primitive, Ancient and
/// Medieval, Modern, and Advanced Armor tables (Ch 8: Equipment, pp.207-208) -- the in-scope modern
/// subset (ADR 0013) uses "Chest" and "All" (e.g. "Clothing, Heavy"); "All but head" is printed only
/// on out-of-scope historical armors (Lamellar, Plate, Ring, Scale), but is handled here so the
/// vocabulary itself does not throw if a future entry uses it.
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
    /// The armor value at a struck location, by attack type, summed across every worn piece that
    /// covers it. Ch 8: Equipment, "Layering Armor" (p.209): soft armor worn with other armor
    /// "add[s] their usual armor value," and overlapping anything else "total[s] the armor value" (at
    /// the cost of tripling the lesser piece's ENC -- ENC/burden/skill-modifier layering is not
    /// modeled here, only the armor-value total). "Armor by Hit Location (Option)" (p.209) separately
    /// says to use "the heaviest" piece for <em>burden</em> and <em>skill modifier</em> when pieces
    /// differ -- that heaviest-wins rule does not apply to armor value, which always totals per
    /// Layering Armor. Zero if no worn armor covers the location.
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
            .Sum(armor => isFirearm ? armor.ArmorValue.Firearms : armor.ArmorValue.MeleeAndLowVelocity);
    }

    private static bool Matches(string printedCategory, HitLocation location) => printedCategory switch
    {
        "Head" => location == HitLocation.Head,
        "Chest" => location == HitLocation.Chest,
        "Abdomen" => location == HitLocation.Abdomen,
        "Arms" => location is HitLocation.LeftArm or HitLocation.RightArm,
        "Legs" => location is HitLocation.LeftLeg or HitLocation.RightLeg,
        "All" => true,
        "All but head" => location != HitLocation.Head,
        _ => throw new ArgumentException(
            $"Unrecognized armor hit-location category '{printedCategory}'.", nameof(printedCategory)),
    };
}
