using System.Text.Json;
using Brp.Core.Dice;
using Brp.Rules.Combat;

namespace Brp.Data;

/// <summary>
/// Loads NoiRPG's damage thresholds from embedded JSON. Sourced: Ch 2: Characters, "Hit Points"
/// (p.13); Ch 6: Combat, "Damage &amp; Healing" (pp.154-156); Ch 7: Spot Rules, "Knockout
/// Attacks" (p.174). Recorded in <c>docs/decisions/0017-damage.md</c>.
/// </summary>
public static class NoirDamageRuleset
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>Loads a new, immutable damage ruleset from the shipped data.</summary>
    public static DamageRuleset Load()
    {
        var assembly = typeof(NoirDamageRuleset).Assembly;
        using var stream = assembly.GetManifestResourceStream("Brp.Data.damage-ruleset.json")
            ?? throw new InvalidOperationException("The damage ruleset data resource is missing.");
        var data = JsonSerializer.Deserialize<DamageRulesetData>(stream, SerializerOptions)
            ?? throw new InvalidOperationException("The damage ruleset data is empty.");

        var knockoutDuration = DiceExpression.Parse(data.KnockoutRule.KnockoutDurationRoundsFormula);

        return new DamageRuleset(
            unconsciousHitPointLevel: data.DamageThresholds.UnconsciousHitPointLevel,
            deadHitPointLevel: data.DamageThresholds.DeadHitPointLevel,
            knockoutDuration: knockoutDuration);
    }

    private sealed class DamageRulesetData
    {
        public required DamageThresholdsData DamageThresholds { get; init; }

        public required KnockoutRuleData KnockoutRule { get; init; }
    }

    private sealed class DamageThresholdsData
    {
        public required int UnconsciousHitPointLevel { get; init; }

        public required int DeadHitPointLevel { get; init; }
    }

    private sealed class KnockoutRuleData
    {
        public required string KnockoutDurationRoundsFormula { get; init; }
    }
}
