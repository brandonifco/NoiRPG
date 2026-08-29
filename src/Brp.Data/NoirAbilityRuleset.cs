using System.Text.Json;
using Brp.Core.Abilities;
using Brp.Core.Dice;

namespace Brp.Data;

/// <summary>
/// Loads NoiRPG's Layer 1 rules values from embedded JSON. The source is Ch 2: Characters,
/// "Characteristics", "Damage Modifier Table", and "Movement (MOV)" (pp.10-15).
/// </summary>
public static class NoirAbilityRuleset
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>Loads a new, immutable ruleset view from the shipped data.</summary>
    public static AbilityRuleset Load()
    {
        var assembly = typeof(NoirAbilityRuleset).Assembly;
        using var stream = assembly.GetManifestResourceStream("Brp.Data.ability-ruleset.json")
            ?? throw new InvalidOperationException("The ability ruleset data resource is missing.");
        var data = JsonSerializer.Deserialize<AbilityRulesetData>(stream, SerializerOptions)
            ?? throw new InvalidOperationException("The ability ruleset data is empty.");

        var characteristics = data.Characteristics.Select(c => new CharacteristicDefinition(
            new CharacteristicId(c.Id), c.DisplayName, c.Minimum, c.Maximum, c.RollName));
        var bands = data.DamageModifierBands.Select(b => new DamageModifierBand(
            b.Minimum,
            b.Maximum,
            b.Modifier is null ? null : DiceExpression.Parse(b.Modifier),
            b.Continuation is null ? null : new DamageModifierContinuation(
                b.Continuation.Step,
                b.Continuation.StartingDiceCount,
                b.Continuation.DiceSides,
                b.Continuation.DiceCountIncrease)));
        return new AbilityRuleset(
            characteristics,
            new DamageModifierTable(bands),
            data.StartingMovement,
            data.MinimumCharacteristicRollMultiplier,
            data.MaximumCharacteristicRollMultiplier,
            data.StandardCharacteristicRollMultiplier,
            data.HitPointDivisor,
            data.MajorWoundDivisor,
            data.ExperienceBonusDivisor);
    }

    private sealed class AbilityRulesetData
    {
        public required List<CharacteristicData> Characteristics { get; init; }

        public required List<DamageModifierBandData> DamageModifierBands { get; init; }

        public required int StartingMovement { get; init; }

        public required int MinimumCharacteristicRollMultiplier { get; init; }

        public required int MaximumCharacteristicRollMultiplier { get; init; }

        public required int StandardCharacteristicRollMultiplier { get; init; }

        public required int HitPointDivisor { get; init; }

        public required int MajorWoundDivisor { get; init; }

        public required int ExperienceBonusDivisor { get; init; }
    }

    private sealed class CharacteristicData
    {
        public required string Id { get; init; }

        public required string DisplayName { get; init; }

        public required int Minimum { get; init; }

        public int? Maximum { get; init; }

        public string? RollName { get; init; }
    }

    private sealed class DamageModifierBandData
    {
        public required int Minimum { get; init; }

        public int? Maximum { get; init; }

        public string? Modifier { get; init; }

        public DamageModifierContinuationData? Continuation { get; init; }
    }

    private sealed class DamageModifierContinuationData
    {
        public required int Step { get; init; }

        public required int StartingDiceCount { get; init; }

        public required int DiceSides { get; init; }

        public required int DiceCountIncrease { get; init; }
    }
}
