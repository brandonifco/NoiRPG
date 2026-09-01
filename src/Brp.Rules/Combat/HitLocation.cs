namespace Brp.Rules.Combat;

/// <summary>
/// The seven hit locations of a humanoid, per Ch 6: Combat, "Melee Hit Location Table (Option)"
/// (p.145: "Hit Locations", D20 roll table). Formalizes the type
/// <see cref="Gear.ArmorDefinition.HitLocations"/>'s doc comment noted did not yet exist (#112).
/// Nonhuman hit-location tables (Ch 11: Creatures) are out of scope -- the noir setting is
/// human-only (<c>orc-scope-filter.md</c>).
/// </summary>
public enum HitLocation
{
    /// <summary>Right leg, hip to bottom of foot (D20 1-4).</summary>
    RightLeg,

    /// <summary>Left leg, hip to bottom of foot (D20 5-8).</summary>
    LeftLeg,

    /// <summary>Hip joint to bottom of rib cage (D20 9-11 -- see <see cref="HitLocationTable"/> for the printed-misprint note).</summary>
    Abdomen,

    /// <summary>Rib cage up to neck and shoulders (D20 12).</summary>
    Chest,

    /// <summary>Entire right arm (D20 13-15).</summary>
    RightArm,

    /// <summary>Entire left arm (D20 16-18).</summary>
    LeftArm,

    /// <summary>Neck and head (D20 19-20).</summary>
    Head,
}
