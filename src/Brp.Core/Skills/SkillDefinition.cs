using Brp.Core.Abilities;
using Brp.Core.Primitives;

namespace Brp.Core.Skills;

/// <summary>
/// A skill's identity: id, canonical (framework) name, category, and printed base-chance
/// expression. Ch 3: Skills, "Base Chances" (p.31) is the source for the base-chance
/// concept; the canonical naming rule ("the framework's names win", `orc-scope-filter.md`,
/// "Skill naming") is why <see cref="Name"/> may differ from <see cref="BookEquivalent"/>.
/// </summary>
/// <param name="Id">The stable ruleset identifier, keyed to the framework's canonical name.</param>
/// <param name="Name">The display name shown to players -- the framework's name, not the book's.</param>
/// <param name="Category">One of Ch 3's six skill categories (p.31).</param>
/// <param name="BaseChance">The printed base-chance expression this skill resolves against.</param>
/// <param name="Parent">
/// For a specialty (see <see cref="Specialty"/>), the shared parent skill it was created
/// from, e.g. every <c>Knowledge (...)</c> specialty's parent is the <c>Knowledge</c>
/// definition. <c>null</c> for a skill that is not a specialty.
/// </param>
/// <param name="BookEquivalent">
/// The book's own name for this skill, e.g. <c>Streetwise</c>'s is
/// <c>Knowledge (Streetwise)</c> and <c>Shadow</c>'s is <c>Stealth</c>. Equal to
/// <see cref="Name"/> when the framework did not rename the skill. <c>Intimidate</c> has no
/// book equivalent (docs/decisions/0006-skill-bonus-system.md) and is recorded as such.
/// </param>
public sealed record SkillDefinition(
    SkillId Id,
    string Name,
    SkillCategory Category,
    BaseChanceExpression BaseChance,
    SkillDefinition? Parent,
    string BookEquivalent)
{
    /// <summary>Creates a non-specialty skill whose book name matches its framework name.</summary>
    public SkillDefinition(SkillId id, string name, SkillCategory category, BaseChanceExpression baseChance)
        : this(id, name, category, baseChance, Parent: null, BookEquivalent: name)
    {
    }

    /// <summary>
    /// A specialty is a distinct, independently resolvable skill instance that shares a
    /// parent definition (see <see cref="Specialty"/>) rather than a plain top-level skill.
    /// </summary>
    public bool IsSpecialty => Parent is not null;

    /// <summary>Evaluates this skill's base chance against a character's abilities.</summary>
    public Percent BaseChanceFor(AbilitySet abilities) => BaseChance.Evaluate(abilities);
}
