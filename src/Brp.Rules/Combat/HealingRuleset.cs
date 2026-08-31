using Brp.Core.Dice;
using Brp.Core.Primitives;

namespace Brp.Rules.Combat;

/// <summary>
/// The data-defined values of Ch 6: Combat healing and recovery (AGENTS.md invariant 7: rules values
/// are data, not constants): the First Aid skill's per-wound healing (Ch 3, p.39), natural healing
/// (p.157), the Medicine skill's doubled rate and characteristic restoration (Ch 3, p.46; p.157), and
/// the Conditions of Medical Care Table (p.157). Loaded from <c>healing-ruleset.json</c> by
/// <c>Brp.Data.NoirHealingRuleset.Load()</c>. See <c>docs/decisions/0023-healing-and-recovery.md</c>.
/// </summary>
public sealed class HealingRuleset
{
    /// <summary>Creates a healing ruleset from data-defined values.</summary>
    public HealingRuleset(
        FirstAidRuleset firstAid,
        NaturalHealingRuleset naturalHealing,
        MedicineRuleset medicine,
        ConditionsOfMedicalCareTable conditionsOfMedicalCare)
    {
        ArgumentNullException.ThrowIfNull(firstAid);
        ArgumentNullException.ThrowIfNull(naturalHealing);
        ArgumentNullException.ThrowIfNull(medicine);
        ArgumentNullException.ThrowIfNull(conditionsOfMedicalCare);

        FirstAid = firstAid;
        NaturalHealing = naturalHealing;
        Medicine = medicine;
        ConditionsOfMedicalCare = conditionsOfMedicalCare;
    }

    /// <summary>Ch 3, "First Aid" (p.39): the per-wound healing skill's values.</summary>
    public FirstAidRuleset FirstAid { get; }

    /// <summary>Ch 6, "Healing Naturally" (p.157): the flat weekly natural-healing rate.</summary>
    public NaturalHealingRuleset NaturalHealing { get; }

    /// <summary>Ch 3, "Medicine" (p.46) and Ch 6 (p.157): the Medicine skill's recovery values.</summary>
    public MedicineRuleset Medicine { get; }

    /// <summary>Ch 6, "Conditions of Medical Care" (p.157): the three-tier healing-rate modifier table.</summary>
    public ConditionsOfMedicalCareTable ConditionsOfMedicalCare { get; }
}

/// <summary>
/// The data-defined values of the Ch 3: Skills, "First Aid" (p.39) skill's healing effect. First Aid
/// restores hit points to a single wound, capped at the hit points that wound inflicted, once per
/// wound (System Notes, p.39).
/// </summary>
public sealed class FirstAidRuleset
{
    /// <summary>Creates a First Aid ruleset from data-defined values.</summary>
    public FirstAidRuleset(
        int printedBaseChancePercent,
        DiceExpression successHealing,
        DiceExpression specialHealing,
        DiceExpression criticalHealing,
        int fumbleSelfDamage,
        int medicineBonusNumerator,
        int medicineBonusDenominator,
        int sciencePharmacyBonusNumerator,
        int sciencePharmacyBonusDenominator,
        int equipmentBonusMaxPercent)
    {
        ArgumentNullException.ThrowIfNull(successHealing);
        ArgumentNullException.ThrowIfNull(specialHealing);
        ArgumentNullException.ThrowIfNull(criticalHealing);
        ArgumentOutOfRangeException.ThrowIfNegative(printedBaseChancePercent);
        ArgumentOutOfRangeException.ThrowIfNegative(fumbleSelfDamage);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(medicineBonusDenominator);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sciencePharmacyBonusDenominator);
        ArgumentOutOfRangeException.ThrowIfNegative(medicineBonusNumerator);
        ArgumentOutOfRangeException.ThrowIfNegative(sciencePharmacyBonusNumerator);
        ArgumentOutOfRangeException.ThrowIfNegative(equipmentBonusMaxPercent);

        PrintedBaseChancePercent = printedBaseChancePercent;
        SuccessHealing = successHealing;
        SpecialHealing = specialHealing;
        CriticalHealing = criticalHealing;
        FumbleSelfDamage = fumbleSelfDamage;
        MedicineBonusNumerator = medicineBonusNumerator;
        MedicineBonusDenominator = medicineBonusDenominator;
        SciencePharmacyBonusNumerator = sciencePharmacyBonusNumerator;
        SciencePharmacyBonusDenominator = sciencePharmacyBonusDenominator;
        EquipmentBonusMaxPercent = equipmentBonusMaxPercent;
    }

    /// <summary>Ch 3, "First Aid" (p.39): "Base Chance: 30%." The printed base chance the 5% floor keys on.</summary>
    public int PrintedBaseChancePercent { get; }

    /// <summary>Ch 3, "First Aid" (p.39): "Success: ... Heal 1D3 hit points to a single wound or injury."</summary>
    public DiceExpression SuccessHealing { get; }

    /// <summary>Ch 3, "First Aid" (p.39): "Special: As above but healing 2D3 hit points."</summary>
    public DiceExpression SpecialHealing { get; }

    /// <summary>Ch 3, "First Aid" (p.39): "Critical: As above but healing 3+1D3 hit points."</summary>
    public DiceExpression CriticalHealing { get; }

    /// <summary>Ch 3, "First Aid" (p.39): "Fumble: The patient takes 1 general hit point of damage."</summary>
    public int FumbleSelfDamage { get; }

    /// <summary>Ch 3, "First Aid" (p.39): "may add 1/2 of their Medicine skill rating." Bonus numerator.</summary>
    public int MedicineBonusNumerator { get; }

    /// <summary>Ch 3, "First Aid" (p.39): "1/2 of their Medicine skill rating." Bonus denominator.</summary>
    public int MedicineBonusDenominator { get; }

    /// <summary>Ch 3, "First Aid" (p.39): "1/5 of their Science (Pharmacy) skill rating." Bonus numerator.</summary>
    public int SciencePharmacyBonusNumerator { get; }

    /// <summary>Ch 3, "First Aid" (p.39): "1/5 of their Science (Pharmacy) skill rating." Bonus denominator.</summary>
    public int SciencePharmacyBonusDenominator { get; }

    /// <summary>Ch 3, "First Aid" (p.39): medical equipment "may add up to a +20% bonus to skill ratings."</summary>
    public int EquipmentBonusMaxPercent { get; }

    /// <summary>The First Aid printed base chance as a <see cref="Percent"/>.</summary>
    public Percent PrintedBaseChance => Percent.Of(PrintedBaseChancePercent);
}

/// <summary>
/// The data-defined values of Ch 6: Combat, "Healing Naturally" (p.157): "Your character will
/// normally heal 1D3 hit points per game week. This is the normal healing rate." A flat rate, not
/// tied to CON.
/// </summary>
public sealed class NaturalHealingRuleset
{
    /// <summary>Creates a natural-healing ruleset from data-defined values.</summary>
    public NaturalHealingRuleset(DiceExpression weeklyRate)
    {
        ArgumentNullException.ThrowIfNull(weeklyRate);
        WeeklyRate = weeklyRate;
    }

    /// <summary>Ch 6, "Healing Naturally" (p.157): the flat 1D3 hit points healed per game week.</summary>
    public DiceExpression WeeklyRate { get; }
}

/// <summary>
/// The data-defined values of the Ch 3: Skills, "Medicine" (p.46) skill and its Ch 6 (p.157) role:
/// doubling the natural healing rate, and stabilizing a poisoned/diseased character to restore hit
/// points or characteristic points per week.
/// </summary>
public sealed class MedicineRuleset
{
    /// <summary>Creates a Medicine ruleset from data-defined values.</summary>
    public MedicineRuleset(
        int printedBaseChancePercent,
        DiceExpression doubledWeeklyRate,
        DiceExpression characteristicRestorationSuccess,
        DiceExpression characteristicRestorationSpecial,
        DiceExpression characteristicRestorationCritical)
    {
        ArgumentNullException.ThrowIfNull(doubledWeeklyRate);
        ArgumentNullException.ThrowIfNull(characteristicRestorationSuccess);
        ArgumentNullException.ThrowIfNull(characteristicRestorationSpecial);
        ArgumentNullException.ThrowIfNull(characteristicRestorationCritical);
        ArgumentOutOfRangeException.ThrowIfNegative(printedBaseChancePercent);

        PrintedBaseChancePercent = printedBaseChancePercent;
        DoubledWeeklyRate = doubledWeeklyRate;
        CharacteristicRestorationSuccess = characteristicRestorationSuccess;
        CharacteristicRestorationSpecial = characteristicRestorationSpecial;
        CharacteristicRestorationCritical = characteristicRestorationCritical;
    }

    /// <summary>Ch 3, "Medicine" (p.46): "Base Chance: 05%." The printed base chance the 5% floor keys on.</summary>
    public int PrintedBaseChancePercent { get; }

    /// <summary>Ch 3, "Medicine" (p.46) / Ch 6 (p.157): "The patient's healing rate doubles from 1D3 to 2D3 hit points per week."</summary>
    public DiceExpression DoubledWeeklyRate { get; }

    /// <summary>Ch 3, "Medicine" (p.46): "recovers 1D3-1 hit points or characteristic points per week." Success grade.</summary>
    public DiceExpression CharacteristicRestorationSuccess { get; }

    /// <summary>Ch 3, "Medicine" (p.46): "1D3 characteristic points are recovered." Special grade.</summary>
    public DiceExpression CharacteristicRestorationSpecial { get; }

    /// <summary>Ch 3, "Medicine" (p.46): "1D3+1 characteristic points are recovered." Critical grade.</summary>
    public DiceExpression CharacteristicRestorationCritical { get; }

    /// <summary>The Medicine printed base chance as a <see cref="Percent"/>.</summary>
    public Percent PrintedBaseChance => Percent.Of(PrintedBaseChancePercent);
}
