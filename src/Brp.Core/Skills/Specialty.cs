namespace Brp.Core.Skills;

/// <summary>
/// Creates specialty skills: distinct, independently resolvable <see cref="SkillDefinition"/>
/// instances that share a parent definition. Ch 3: Skills, "Knowledge Specialties" (p.43):
/// "Write them like this on your character sheet: Knowledge (Group/Templars) or Knowledge
/// (Templars)." Two specialties of the same parent -- e.g. <c>Knowledge (Law)</c> and
/// <c>Knowledge (Streetwise)</c> -- are two independent skills with their own base chances
/// and their own experience checks (the framework's "two specialties are two checks" rule);
/// only <see cref="SkillDefinition.Parent"/> and <see cref="SkillDefinition.Category"/> are
/// shared.
/// </summary>
public static class Specialty
{
    /// <summary>Creates a specialty of <paramref name="parent"/>.</summary>
    public static SkillDefinition Create(
        SkillDefinition parent, SkillId id, string name, BaseChanceExpression baseChance, string? bookEquivalent = null)
    {
        ArgumentNullException.ThrowIfNull(parent);
        ArgumentNullException.ThrowIfNull(baseChance);
        return new SkillDefinition(id, name, parent.Category, baseChance, parent, bookEquivalent ?? name);
    }
}
