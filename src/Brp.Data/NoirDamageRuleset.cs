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
        var crushingNoModifierBonus = DiceExpression.Parse(
            data.DamageFormulas.SpecialSuccessDamage.SpecialDamageByType.Crushing.CrushingNoModifierBonusFormula);

        return new DamageRuleset(
            unconsciousHitPointLevel: data.DamageThresholds.UnconsciousHitPointLevel,
            deadHitPointLevel: data.DamageThresholds.DeadHitPointLevel,
            knockoutDuration: knockoutDuration,
            crushingNoModifierBonus: crushingNoModifierBonus);
    }

    private sealed class DamageRulesetData
    {
        public required DamageThresholdsData DamageThresholds { get; init; }

        public required KnockoutRuleData KnockoutRule { get; init; }

        public required DamageFormulasData DamageFormulas { get; init; }
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

    private sealed class DamageFormulasData
    {
        public required SpecialSuccessDamageData SpecialSuccessDamage { get; init; }
    }

    private sealed class SpecialSuccessDamageData
    {
        public required SpecialDamageByTypeData SpecialDamageByType { get; init; }
    }

    private sealed class SpecialDamageByTypeData
    {
        public required CrushingData Crushing { get; init; }
    }

    private sealed class CrushingData
    {
        public required string CrushingNoModifierBonusFormula { get; init; }
    }
}
