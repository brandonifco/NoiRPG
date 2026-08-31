using Brp.Core.Abilities;

namespace Brp.Core.Skills;

/// <summary>
/// One row of the book's "Skill Category Modifiers" table (Ch 2: Characters, "Skill Category
/// Bonuses (Option)", p.18): the characteristics a single <see cref="SkillCategory"/> draws its
/// category bonus from. A category has exactly one primary characteristic, zero or more
/// secondary characteristics, and zero or more negative characteristics.
/// </summary>
/// <param name="Category">The skill category this row configures.</param>
/// <param name="Primary">
/// The primary characteristic: +/-1% per point away from the neutral value (see
/// <see cref="SkillCategoryBonusRuleset.NeutralCharacteristicValue"/>).
/// </param>
/// <param name="Secondary">
/// The secondary characteristics: +/-1% per 2 points away from neutral, magnitude rounded down.
/// The printed table gives a category one or two secondaries; the shape allows any count.
/// </param>
/// <param name="Negative">
/// The negative characteristics: an inverted primary, subtracted rather than added (+1% per
/// point <em>under</em> neutral, -1% per point over). Only Physical has one (SIZ) in the book.
/// </param>
public sealed record SkillCategoryModifierSpec(
    SkillCategory Category,
    CharacteristicId Primary,
    IReadOnlyList<CharacteristicId> Secondary,
    IReadOnlyList<CharacteristicId> Negative);
