using System.Text.Json;
using Brp.Core.Dice;
using Brp.Rules.Combat;

namespace Brp.Data;

/// <summary>
/// Loads NoiRPG's injury/effect spot-rule values from embedded JSON. Sourced: Ch 7: Spot Rules --
/// "Falling" (p.171), "Poison"/"Poison Antidotes" (pp.175-176), and "Disease"/"Illness Severity
/// Table" (pp.169-170) -- see each field on <see cref="FallingRuleset"/>, <see cref="PoisonRuleset"/>,
/// and <see cref="DiseaseRuleset"/> for its exact citation. Recorded in
/// <c>docs/decisions/0019-injury-spot-rules.md</c>.
/// </summary>
public static class NoirInjuryRuleset
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>Loads a new, immutable injury ruleset from the shipped data.</summary>
    public static InjuryRuleset Load()
    {
        var assembly = typeof(NoirInjuryRuleset).Assembly;
        using var stream = assembly.GetManifestResourceStream("Brp.Data.injury-ruleset.json")
            ?? throw new InvalidOperationException("The injury ruleset data resource is missing.");
        var data = JsonSerializer.Deserialize<InjuryRulesetData>(stream, SerializerOptions)
            ?? throw new InvalidOperationException("The injury ruleset data is empty.");

        return new InjuryRuleset(BuildFalling(data.Falling), BuildPoison(data.Poison), BuildDisease(data.Disease));
    }

    private static FallingRuleset BuildFalling(FallingData data) => new(
        baseDamagePerIncrement: DiceExpression.Parse(data.BaseDamagePerIncrementFormula),
        metersPerDamageIncrement: data.MetersPerDamageIncrement,
        forceMultiplier: data.ForceMultiplier,
        smallSizeThreshold: data.SmallSizeThreshold,
        smallSizeReduction: DiceExpression.Parse(data.SmallSizeReductionFormula),
        largeSizeThreshold: data.LargeSizeThreshold,
        largeSizeBand: data.LargeSizeBand,
        largeSizeExtraDamage: DiceExpression.Parse(data.LargeSizeExtraDamageFormula),
        armorHalfProtectionMaxMeters: data.ArmorHalfProtectionMaxMeters,
        armorProtectionNumerator: data.ArmorProtectionNumerator,
        armorProtectionDenominator: data.ArmorProtectionDenominator);

    private static PoisonRuleset BuildPoison(PoisonData data) => new(
        notOvercomeNumerator: data.NotOvercomeNumerator,
        notOvercomeDenominator: data.NotOvercomeDenominator,
        onsetFastActingRounds: data.OnsetFastActingRounds,
        onsetSlowActingTurns: data.OnsetSlowActingTurns,
        antidoteWindowTurns: data.AntidoteWindowTurns);

    private static DiseaseRuleset BuildDisease(DiseaseData data)
    {
        var bands = data.IllnessSeverityTable.Select(row => new IllnessSeverityBand(
            row.MinimumFailures,
            row.MaximumFailures,
            Enum.Parse<IllnessDegree>(row.Degree),
            Enum.Parse<IllnessLossPeriod>(row.LossPeriod)));

        return new DiseaseRuleset(
            minorDiseaseHitPointLoss: DiceExpression.Parse(data.MinorDiseaseHitPointLossFormula),
            minorDiseaseFatigueLoss: DiceExpression.Parse(data.MinorDiseaseFatigueLossFormula),
            recoveryLadderStartingMultiplier: data.RecoveryLadderStartingMultiplier,
            recoveryLadderMultiplierIncrementPerDay: data.RecoveryLadderMultiplierIncrementPerDay,
            recoveryLadderFumbleMultiplierPenalty: data.RecoveryLadderFumbleMultiplierPenalty,
            recoveryLadderStrenuousConditionPenalty: data.RecoveryLadderStrenuousConditionPenalty,
            illnessSeverityTable: new IllnessSeverityTable(bands));
    }

    private sealed class InjuryRulesetData
    {
        public required FallingData Falling { get; init; }

        public required PoisonData Poison { get; init; }

        public required DiseaseData Disease { get; init; }
    }

    private sealed class FallingData
    {
        public required string BaseDamagePerIncrementFormula { get; init; }

        public required int MetersPerDamageIncrement { get; init; }

        public required int ForceMultiplier { get; init; }

        public required int SmallSizeThreshold { get; init; }

        public required string SmallSizeReductionFormula { get; init; }

        public required int LargeSizeThreshold { get; init; }

        public required int LargeSizeBand { get; init; }

        public required string LargeSizeExtraDamageFormula { get; init; }

        public required int ArmorHalfProtectionMaxMeters { get; init; }

        public required int ArmorProtectionNumerator { get; init; }

        public required int ArmorProtectionDenominator { get; init; }
    }

    private sealed class PoisonData
    {
        public required int NotOvercomeNumerator { get; init; }

        public required int NotOvercomeDenominator { get; init; }

        public required int OnsetFastActingRounds { get; init; }

        public required int OnsetSlowActingTurns { get; init; }

        public required int AntidoteWindowTurns { get; init; }
    }

    private sealed class DiseaseData
    {
        public required string MinorDiseaseHitPointLossFormula { get; init; }

        public required string MinorDiseaseFatigueLossFormula { get; init; }

        public required int RecoveryLadderStartingMultiplier { get; init; }

        public required int RecoveryLadderMultiplierIncrementPerDay { get; init; }

        public required int RecoveryLadderFumbleMultiplierPenalty { get; init; }

        public required int RecoveryLadderStrenuousConditionPenalty { get; init; }

        public required IReadOnlyList<IllnessSeverityRowData> IllnessSeverityTable { get; init; }
    }

    private sealed class IllnessSeverityRowData
    {
        public required int MinimumFailures { get; init; }

        public int? MaximumFailures { get; init; }

        public required string Degree { get; init; }

        public required string LossPeriod { get; init; }
    }
}
