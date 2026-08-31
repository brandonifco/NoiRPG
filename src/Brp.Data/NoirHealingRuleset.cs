using System.Text.Json;
using Brp.Core.Contests;
using Brp.Core.Dice;
using Brp.Rules.Combat;

namespace Brp.Data;

/// <summary>
/// Loads NoiRPG's healing and recovery values from embedded JSON. Sourced: Ch 3: Skills -- "First Aid"
/// (p.39) and "Medicine" (p.46) -- and Ch 6: Combat -- "Healing Naturally" and "Conditions of Medical
/// Care" (p.157). See each field on <see cref="HealingRuleset"/> for its exact citation. Recorded in
/// <c>docs/decisions/0023-healing-and-recovery.md</c>.
/// </summary>
public static class NoirHealingRuleset
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>Loads a new, immutable healing ruleset from the shipped data.</summary>
    public static HealingRuleset Load()
    {
        var assembly = typeof(NoirHealingRuleset).Assembly;
        using var stream = assembly.GetManifestResourceStream("Brp.Data.healing-ruleset.json")
            ?? throw new InvalidOperationException("The healing ruleset data resource is missing.");
        var data = JsonSerializer.Deserialize<HealingRulesetData>(stream, SerializerOptions)
            ?? throw new InvalidOperationException("The healing ruleset data is empty.");

        return new HealingRuleset(
            BuildFirstAid(data.FirstAid),
            BuildNaturalHealing(data.NaturalHealing),
            BuildMedicine(data.Medicine),
            BuildConditionsOfMedicalCare(data.ConditionsOfMedicalCare));
    }

    private static FirstAidRuleset BuildFirstAid(FirstAidData data) => new(
        printedBaseChancePercent: data.PrintedBaseChancePercent,
        successHealing: DiceExpression.Parse(data.SuccessHealingFormula),
        specialHealing: DiceExpression.Parse(data.SpecialHealingFormula),
        criticalHealing: DiceExpression.Parse(data.CriticalHealingFormula),
        fumbleSelfDamage: data.FumbleSelfDamage,
        medicineBonusNumerator: data.MedicineBonusNumerator,
        medicineBonusDenominator: data.MedicineBonusDenominator,
        sciencePharmacyBonusNumerator: data.SciencePharmacyBonusNumerator,
        sciencePharmacyBonusDenominator: data.SciencePharmacyBonusDenominator,
        equipmentBonusMaxPercent: data.EquipmentBonusMaxPercent);

    private static NaturalHealingRuleset BuildNaturalHealing(NaturalHealingData data) =>
        new(weeklyRate: DiceExpression.Parse(data.WeeklyRateFormula));

    private static MedicineRuleset BuildMedicine(MedicineData data) => new(
        printedBaseChancePercent: data.PrintedBaseChancePercent,
        doubledWeeklyRate: DiceExpression.Parse(data.DoubledWeeklyRateFormula),
        characteristicRestorationSuccess: DiceExpression.Parse(data.CharacteristicRestorationSuccessFormula),
        characteristicRestorationSpecial: DiceExpression.Parse(data.CharacteristicRestorationSpecialFormula),
        characteristicRestorationCritical: DiceExpression.Parse(data.CharacteristicRestorationCriticalFormula));

    private static ConditionsOfMedicalCareTable BuildConditionsOfMedicalCare(
        IReadOnlyList<MedicalCareRowData> rows)
    {
        var built = rows.Select(row => new MedicalCareRow(
            Enum.Parse<MedicalCareTier>(row.Tier),
            row.Conditions,
            row.RequiresCaregiverRoll,
            Enum.Parse<HealingRollDifficulty>(row.CaregiverRollDifficulty),
            DiceExpression.Parse(row.NaturalHealingFormula),
            row.FumbleAdditionalDamageFormula is null ? null : DiceExpression.Parse(row.FumbleAdditionalDamageFormula),
            row.AllowsAdditionalHealing));

        return new ConditionsOfMedicalCareTable(built);
    }

    private sealed class HealingRulesetData
    {
        public required FirstAidData FirstAid { get; init; }

        public required NaturalHealingData NaturalHealing { get; init; }

        public required MedicineData Medicine { get; init; }

        public required IReadOnlyList<MedicalCareRowData> ConditionsOfMedicalCare { get; init; }
    }

    private sealed class FirstAidData
    {
        public required int PrintedBaseChancePercent { get; init; }

        public required string SuccessHealingFormula { get; init; }

        public required string SpecialHealingFormula { get; init; }

        public required string CriticalHealingFormula { get; init; }

        public required int FumbleSelfDamage { get; init; }

        public required int MedicineBonusNumerator { get; init; }

        public required int MedicineBonusDenominator { get; init; }

        public required int SciencePharmacyBonusNumerator { get; init; }

        public required int SciencePharmacyBonusDenominator { get; init; }

        public required int EquipmentBonusMaxPercent { get; init; }
    }

    private sealed class NaturalHealingData
    {
        public required string WeeklyRateFormula { get; init; }
    }

    private sealed class MedicineData
    {
        public required int PrintedBaseChancePercent { get; init; }

        public required string DoubledWeeklyRateFormula { get; init; }

        public required string CharacteristicRestorationSuccessFormula { get; init; }

        public required string CharacteristicRestorationSpecialFormula { get; init; }

        public required string CharacteristicRestorationCriticalFormula { get; init; }
    }

    private sealed class MedicalCareRowData
    {
        public required string Tier { get; init; }

        public required string Conditions { get; init; }

        public required bool RequiresCaregiverRoll { get; init; }

        public required string CaregiverRollDifficulty { get; init; }

        public required string NaturalHealingFormula { get; init; }

        public string? FumbleAdditionalDamageFormula { get; init; }

        public required bool AllowsAdditionalHealing { get; init; }
    }
}
