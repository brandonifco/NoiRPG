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
/// The hit locations this armor covers ("Fits Locations" column, p.207), as printed. No
/// <c>HitLocation</c> type exists yet in the engine, so these are plain strings; formalizing
/// hit locations is a combat-layer concern this issue was not asked to design.
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
