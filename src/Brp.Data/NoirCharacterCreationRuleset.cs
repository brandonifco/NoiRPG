using System.Text.Json;
using Brp.Core.Abilities;
using Brp.Rules.Creation;

namespace Brp.Data;

/// <summary>
/// Loads NoiRPG's Layer 3 character-creation parameters from embedded JSON. The source is
/// Ch 2: Characters, "Point-Based Character Creation (option)" (pp.9-10) and "Step Seven:
/// Profession and Skills" (p.8) -- see <see cref="CharacterCreationRuleset"/>'s own members
/// for the exact citation of each value.
/// </summary>
public static class NoirCharacterCreationRuleset
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>Loads a new, immutable creation ruleset view from the shipped data.</summary>
    public static CharacterCreationRuleset Load()
    {
        var assembly = typeof(NoirCharacterCreationRuleset).Assembly;
        using var stream = assembly.GetManifestResourceStream("Brp.Data.character-creation-ruleset.json")
            ?? throw new InvalidOperationException("The character creation ruleset data resource is missing.");
        var data = JsonSerializer.Deserialize<CreationRulesetData>(stream, SerializerOptions)
            ?? throw new InvalidOperationException("The character creation ruleset data is empty.");

        var costs = data.CharacteristicCosts.ToDictionary(
            pair => new CharacteristicId(pair.Key), pair => pair.Value);

        return new CharacterCreationRuleset(
            data.CharacteristicPointPool,
            data.StartingCharacteristicValue,
            data.CharacteristicCreationMaximum,
            costs,
            data.FreeShiftPoints,
            data.ProfessionalSkillPoints,
            data.PersonalSkillPointsIntMultiplier,
            data.IncreasedPersonalSkillPointsIntMultiplier,
            data.StartingSkillCapPercent);
    }

    private sealed class CreationRulesetData
    {
        public required int CharacteristicPointPool { get; init; }

        public required int StartingCharacteristicValue { get; init; }

        public required int CharacteristicCreationMaximum { get; init; }

        public required Dictionary<string, int> CharacteristicCosts { get; init; }

        public required int FreeShiftPoints { get; init; }

        public required int ProfessionalSkillPoints { get; init; }

        public required int PersonalSkillPointsIntMultiplier { get; init; }

        public required int IncreasedPersonalSkillPointsIntMultiplier { get; init; }

        public required int StartingSkillCapPercent { get; init; }
    }
}
