using System.Text.Json;
using Brp.Core.Abilities;
using Brp.Core.Skills;

namespace Brp.Data;

/// <summary>
/// Loads NoiRPG's skill-category-bonus policy from embedded JSON. The source is Ch 2: Characters,
/// "Skill Category Bonuses (Option)" -- the "Skill Category Modifiers" and "Skill Bonus Table"
/// (pp.18-19); the neutral pivot, the per-point/per-2-point divisors, and the
/// category-to-characteristic map are all data. Recorded in
/// <c>docs/decisions/0006-skill-bonus-system.md</c> (the decision) and
/// <c>docs/decisions/0022-skill-category-bonus-application.md</c> (its engine application),
/// and book-verified against <c>tools/skill_bonus.py</c>.
/// </summary>
public static class NoirSkillCategoryBonusRuleset
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>Loads a new, immutable skill-category-bonus policy from the shipped data.</summary>
    public static SkillCategoryBonusRuleset Load()
    {
        var assembly = typeof(NoirSkillCategoryBonusRuleset).Assembly;
        using var stream = assembly.GetManifestResourceStream("Brp.Data.skill-category-bonus-ruleset.json")
            ?? throw new InvalidOperationException("The skill category bonus ruleset data resource is missing.");
        var data = JsonSerializer.Deserialize<SkillCategoryBonusData>(stream, SerializerOptions)
            ?? throw new InvalidOperationException("The skill category bonus ruleset data is empty.");

        var categories = data.Categories.Select(c => new SkillCategoryModifierSpec(
            Enum.Parse<SkillCategory>(c.Category, ignoreCase: true),
            new CharacteristicId(c.Primary),
            c.Secondary.Select(id => new CharacteristicId(id)).ToList(),
            c.Negative.Select(id => new CharacteristicId(id)).ToList()));

        return new SkillCategoryBonusRuleset(
            data.NeutralCharacteristicValue,
            data.PrimaryPointsPerModifier,
            data.SecondaryPointsPerModifier,
            categories);
    }

    private sealed class SkillCategoryBonusData
    {
        public required int NeutralCharacteristicValue { get; init; }

        public required int PrimaryPointsPerModifier { get; init; }

        public required int SecondaryPointsPerModifier { get; init; }

        public required List<CategoryData> Categories { get; init; }
    }

    private sealed class CategoryData
    {
        public required string Category { get; init; }

        public required string Primary { get; init; }

        public required List<string> Secondary { get; init; }

        public required List<string> Negative { get; init; }
    }
}
