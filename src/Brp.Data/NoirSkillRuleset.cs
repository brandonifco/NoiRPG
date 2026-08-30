using System.Text.Json;
using Brp.Core.Abilities;
using Brp.Core.Primitives;
using Brp.Core.Skills;

namespace Brp.Data;

/// <summary>
/// Loads NoiRPG's Layer 2 skill list from embedded JSON. The source is Ch 3: Skills, the
/// "Alphabetical Skill List" (pp.33-34) and each skill's own description for its base
/// chance and category; the canonical naming rule and inward mapping are from
/// `orc-scope-filter.md`, "Skill naming: the framework's names win".
/// </summary>
public static class NoirSkillRuleset
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>Loads a new, immutable skill registry from the shipped data.</summary>
    public static SkillRegistry Load()
    {
        var assembly = typeof(NoirSkillRuleset).Assembly;
        using var stream = assembly.GetManifestResourceStream("Brp.Data.skill-ruleset.json")
            ?? throw new InvalidOperationException("The skill ruleset data resource is missing.");
        var data = JsonSerializer.Deserialize<SkillRulesetData>(stream, SerializerOptions)
            ?? throw new InvalidOperationException("The skill ruleset data is empty.");

        var resolvable = new List<SkillDefinition>();
        foreach (var entry in data.Skills)
        {
            AddSkill(entry, parent: null, resolvable);
        }

        return new SkillRegistry(resolvable);
    }

    /// <summary>
    /// Builds one entry and its specialties, if any. A parent entry with specialties is
    /// itself excluded from the resolvable set -- only leaves (specialties, or entries with
    /// no specialties) are independently resolvable skills. This mirrors the book's own
    /// distinction between a skill category like "Knowledge (various)" and an actual,
    /// rollable skill like "Knowledge (Law)".
    /// </summary>
    private static void AddSkill(SkillEntryData entry, SkillDefinition? parent, List<SkillDefinition> resolvable)
    {
        var category = Enum.Parse<SkillCategory>(entry.Category ?? parent?.Category.ToString()
            ?? throw new InvalidOperationException($"Skill '{entry.Id}' has no category."), ignoreCase: true);
        var baseChance = ToExpression(entry.BaseChance);
        var bookEquivalent = entry.BookEquivalent ?? entry.Name;
        var definition = parent is null
            ? new SkillDefinition(new SkillId(entry.Id), entry.Name, category, baseChance, Parent: null, BookEquivalent: bookEquivalent)
            : Specialty.Create(parent, new SkillId(entry.Id), entry.Name, baseChance, bookEquivalent);

        if (entry.Specialties is { Count: > 0 })
        {
            foreach (var specialty in entry.Specialties)
            {
                AddSkill(specialty, definition, resolvable);
            }
        }
        else
        {
            resolvable.Add(definition);
        }
    }

    private static BaseChanceExpression ToExpression(BaseChanceData data) => data.Type.ToLowerInvariant() switch
    {
        "constant" => new ConstantBaseChance(Percent.Of(data.Value
            ?? throw new InvalidOperationException("A constant base chance requires a value."))),
        "formula" => new CharacteristicFormulaBaseChance((data.Terms
                ?? throw new InvalidOperationException("A formula base chance requires terms."))
            .Select(term => new CharacteristicTerm(new CharacteristicId(term.Characteristic), term.Multiplier))
            .ToList()),
        "era" => new EraConditionalBaseChance(
            ToExpression(data.Modern ?? throw new InvalidOperationException("An era-conditional base chance requires a modern value.")),
            ToExpression(data.Historical ?? throw new InvalidOperationException("An era-conditional base chance requires a historical value."))),
        "weaponderived" => new WeaponDerivedBaseChance(),
        _ => throw new InvalidOperationException($"Unknown base chance type '{data.Type}'."),
    };

    private sealed class SkillRulesetData
    {
        public required List<SkillEntryData> Skills { get; init; }
    }

    private sealed class SkillEntryData
    {
        public required string Id { get; init; }

        public required string Name { get; init; }

        public string? Category { get; init; }

        public required BaseChanceData BaseChance { get; init; }

        public string? BookEquivalent { get; init; }

        public List<SkillEntryData>? Specialties { get; init; }
    }

    private sealed class BaseChanceData
    {
        public required string Type { get; init; }

        public int? Value { get; init; }

        public List<CharacteristicTermData>? Terms { get; init; }

        public BaseChanceData? Modern { get; init; }

        public BaseChanceData? Historical { get; init; }
    }

    private sealed class CharacteristicTermData
    {
        public required string Characteristic { get; init; }

        public required int Multiplier { get; init; }
    }
}
