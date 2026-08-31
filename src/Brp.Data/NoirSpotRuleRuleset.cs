using System.Text.Json;
using Brp.Rules.Combat;

namespace Brp.Data;

/// <summary>
/// Loads NoiRPG's situational combat spot-rule percentage values from embedded JSON. Sourced:
/// Ch 7: Spot Rules -- "Firing Into Combat" (p.173) and "Darkness" (p.169, whose modifiers come
/// from the Ch 5 Situational Modifiers "Environment" row, p.133) -- see each field on
/// <see cref="SpotRuleRuleset"/> for its exact citation. Recorded in
/// <c>docs/decisions/0018-spot-rules.md</c>.
/// </summary>
public static class NoirSpotRuleRuleset
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>Loads a new, immutable spot-rule ruleset from the shipped data.</summary>
    public static SpotRuleRuleset Load()
    {
        var assembly = typeof(NoirSpotRuleRuleset).Assembly;
        using var stream = assembly.GetManifestResourceStream("Brp.Data.spot-rule-ruleset.json")
            ?? throw new InvalidOperationException("The spot-rule ruleset data resource is missing.");
        var data = JsonSerializer.Deserialize<SpotRuleRulesetData>(stream, SerializerOptions)
            ?? throw new InvalidOperationException("The spot-rule ruleset data is empty.");

        return new SpotRuleRuleset(
            firingIntoCombatModifier: data.FiringIntoCombatModifier,
            darknessSemiDarknessModifier: data.DarknessSemiDarknessModifier,
            darknessPitchBlackModifier: data.DarknessPitchBlackModifier,
            darknessDetectionHalvingNumerator: data.DarknessDetectionHalvingNumerator,
            darknessDetectionHalvingDenominator: data.DarknessDetectionHalvingDenominator);
    }

    private sealed class SpotRuleRulesetData
    {
        public required int FiringIntoCombatModifier { get; init; }

        public required int DarknessSemiDarknessModifier { get; init; }

        public required int DarknessPitchBlackModifier { get; init; }

        public required int DarknessDetectionHalvingNumerator { get; init; }

        public required int DarknessDetectionHalvingDenominator { get; init; }
    }
}
