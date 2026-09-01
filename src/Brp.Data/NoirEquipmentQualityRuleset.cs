using System.Text.Json;
using Brp.Rules.Gear;

namespace Brp.Data;

/// <summary>
/// Loads NoiRPG's equipment-quality percentages from embedded JSON. Sourced: Ch 8: Equipment,
/// "Equipment Quality Modifiers" (p.185). See <see cref="EquipmentQualityRuleset"/> for the
/// field-level citation.
/// </summary>
public static class NoirEquipmentQualityRuleset
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>Loads a new, immutable equipment-quality ruleset from the shipped data.</summary>
    public static EquipmentQualityRuleset Load()
    {
        var assembly = typeof(NoirEquipmentQualityRuleset).Assembly;
        using var stream = assembly.GetManifestResourceStream("Brp.Data.equipment-quality-ruleset.json")
            ?? throw new InvalidOperationException("The equipment-quality ruleset data resource is missing.");
        var data = JsonSerializer.Deserialize<EquipmentQualityData>(stream, SerializerOptions)
            ?? throw new InvalidOperationException("The equipment-quality ruleset data is empty.");

        return new EquipmentQualityRuleset(data.InferiorDelta, data.SuperiorDelta);
    }

    private sealed class EquipmentQualityData
    {
        public required int InferiorDelta { get; init; }

        public required int SuperiorDelta { get; init; }
    }
}
