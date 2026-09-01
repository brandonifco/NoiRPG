namespace Brp.Rules.Gear;

/// <summary>
/// An armor type's identity and protection stats. Sourced: Ch 8: Equipment, Modern Armor table
/// (p.207) and each entry's own description (p.206). Hand-picked to the modern noir subset per
/// `orc-scope-filter.md`, Ch 8: "a dozen firearms and three armor types."
/// </summary>
/// <param name="Id">The stable ruleset identifier.</param>
/// <param name="Name">The display name, as printed in the book.</param>
/// <param name="ArmorValue">Armor points, by attack type (Ch 8, "Armor by Hit Locations" is on the keep-list).</param>
/// <param name="SkillPenalty">The skill penalty imposed while worn.</param>
/// <param name="HitLocations">
/// The hit locations this armor covers ("Fits Locations" column, p.207), as printed --
/// "Head", "Chest", "Abdomen", "Arms", or "Legs". Kept as the book's own category strings
/// rather than the granular <see cref="Combat.HitLocation"/> enum the D20 hit-location table
/// rolls (Ch 6, p.145) because the printed armor table's categories are coarser than that
/// table's seven locations ("Arms" covers both <see cref="Combat.HitLocation.LeftArm"/> and
/// <see cref="Combat.HitLocation.RightArm"/>, and likewise for "Legs"). See
/// <see cref="ArmorCoverage"/> (#112) for the mapping between the two.
/// </param>
/// <param name="Note">An optional printed footnote, e.g. Riot Gear's "Includes helmet".</param>
/// <param name="Source">The book table this entry was transcribed from.</param>
public sealed record ArmorDefinition(
    ArmorId Id,
    string Name,
    ArmorValue ArmorValue,
    ArmorSkillPenalty SkillPenalty,
    IReadOnlyList<string> HitLocations,
    string? Note,
    string Source);
