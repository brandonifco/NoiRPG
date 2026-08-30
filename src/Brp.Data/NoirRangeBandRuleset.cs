using System.Text.Json;
using Brp.Rules.Combat;

namespace Brp.Data;

/// <summary>
/// Loads NoiRPG's missile/firearm range-band parameters from embedded JSON. Sourced: Ch 6:
/// Combat, "Missile Weapons" (p.154) and Ch 7: Spot Rules, "Extended Range" (p.171) -- see
/// each field on <see cref="RangeBandRuleset"/> for its exact citation. Recorded in
/// <c>docs/decisions/0014-range-bands.md</c>.
/// </summary>
public static class NoirRangeBandRuleset
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>Loads a new, immutable range-band ruleset from the shipped data.</summary>
    public static RangeBandRuleset Load()
    {
        var assembly = typeof(NoirRangeBandRuleset).Assembly;
        using var stream = assembly.GetManifestResourceStream("Brp.Data.range-band-ruleset.json")
            ?? throw new InvalidOperationException("The range-band ruleset data resource is missing.");
        var data = JsonSerializer.Deserialize<RangeBandRulesetData>(stream, SerializerOptions)
            ?? throw new InvalidOperationException("The range-band ruleset data is empty.");

        return new RangeBandRuleset(
            pointBlankDexDivisor: data.PointBlankDexDivisor,
            mediumRangeMultiplier: data.MediumRangeMultiplier,
            longRangeChanceNumerator: data.LongRangeChanceNumerator,
            longRangeChanceDenominator: data.LongRangeChanceDenominator,
            throwingCutoffMultiplier: data.ThrowingCutoffMultiplier,
            targetingEquipmentDampeningNumerator: data.TargetingEquipmentDampeningNumerator,
            targetingEquipmentDampeningDenominator: data.TargetingEquipmentDampeningDenominator);
    }

    private sealed class RangeBandRulesetData
    {
        public required int PointBlankDexDivisor { get; init; }

        public required int MediumRangeMultiplier { get; init; }

        public required int LongRangeChanceNumerator { get; init; }

        public required int LongRangeChanceDenominator { get; init; }

        public required int ThrowingCutoffMultiplier { get; init; }

        public required int TargetingEquipmentDampeningNumerator { get; init; }

        public required int TargetingEquipmentDampeningDenominator { get; init; }
    }
}
