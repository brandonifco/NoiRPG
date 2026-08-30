namespace Brp.Core.Skills;

/// <summary>
/// The six skill categories a skill belongs to. Sourced: Ch 3: Skills, "Skill Categories"
/// (p.31) and the "Skill List by Category" table (p.32), which assigns every printed skill
/// to exactly one of these. Category assignments for the canonical 18 are already recorded
/// in <c>docs/decisions/0006-skill-bonus-system.md</c>; this enum is the type that field
/// anticipates ("<c>SkillDefinition</c> must carry a category").
/// </summary>
public enum SkillCategory
{
    /// <summary>Weapon proficiency and combat maneuvers.</summary>
    Combat,

    /// <summary>Conversation, reading, and interpersonal exchange.</summary>
    Communication,

    /// <summary>Tasks requiring precise hand-eye coordination.</summary>
    Manipulation,

    /// <summary>Specific knowledge and individual judgment.</summary>
    Mental,

    /// <summary>Gathering and interpreting information from the environment.</summary>
    Perception,

    /// <summary>Feats of strength, agility, and athletics.</summary>
    Physical,
}
