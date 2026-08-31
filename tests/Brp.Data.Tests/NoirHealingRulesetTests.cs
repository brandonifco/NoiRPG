using Brp.Core.Contests;
using Brp.Rules.Combat;

namespace Brp.Data.Tests;

/// <summary>
/// Confirms the shipped healing data loads and reproduces the printed values: the First Aid healing
/// amounts and bonuses (Ch 3: Skills, "First Aid", p.39), the natural healing rate (Ch 6, "Healing
/// Naturally", p.157), the Medicine doubled rate and characteristic restoration (Ch 3, "Medicine",
/// p.46; p.157), and every row of the "Conditions of Medical Care" table (p.157) -- row by row rather
/// than sampled, per AGENTS.md's "table-backed rule" convention. See
/// <c>docs/decisions/0023-healing-and-recovery.md</c>.
/// </summary>
public class NoirHealingRulesetTests
{
    private static readonly HealingRuleset Ruleset = NoirHealingRuleset.Load();

    [Fact]
    public void First_aid_healing_amounts_and_bonuses_match_the_book_page_39()
    {
        Assert.Equal(30, Ruleset.FirstAid.PrintedBaseChancePercent);
        Assert.Equal("1D3", Ruleset.FirstAid.SuccessHealing.Notation);
        Assert.Equal("2D3", Ruleset.FirstAid.SpecialHealing.Notation);
        // "Critical: As above but healing 3+1D3 hit points" -- the constant leads, then the die.
        Assert.Equal("3+1D3", Ruleset.FirstAid.CriticalHealing.Notation);
        Assert.Equal(1, Ruleset.FirstAid.FumbleSelfDamage);

        // "may add 1/2 of their Medicine skill rating and 1/5 of their Science (Pharmacy) skill rating."
        Assert.Equal(1, Ruleset.FirstAid.MedicineBonusNumerator);
        Assert.Equal(2, Ruleset.FirstAid.MedicineBonusDenominator);
        Assert.Equal(1, Ruleset.FirstAid.SciencePharmacyBonusNumerator);
        Assert.Equal(5, Ruleset.FirstAid.SciencePharmacyBonusDenominator);

        // "medical equipment ... may add up to a +20% bonus to skill ratings."
        Assert.Equal(20, Ruleset.FirstAid.EquipmentBonusMaxPercent);
    }

    [Fact]
    public void Natural_healing_is_a_flat_1d3_per_game_week_page_157()
    {
        // "Your character will normally heal 1D3 hit points per game week" -- flat, not CON-tied.
        Assert.Equal("1D3", Ruleset.NaturalHealing.WeeklyRate.Notation);
    }

    [Fact]
    public void Medicine_doubles_the_rate_and_restores_characteristics_pages_46_157()
    {
        Assert.Equal(5, Ruleset.Medicine.PrintedBaseChancePercent);
        // "The patient's healing rate doubles from 1D3 to 2D3 hit points per week."
        Assert.Equal("2D3", Ruleset.Medicine.DoubledWeeklyRate.Notation);
        // "recovers 1D3-1 ... characteristic points per week" (Success), "1D3" (Special), "1D3+1" (Critical).
        Assert.Equal("1D3-1", Ruleset.Medicine.CharacteristicRestorationSuccess.Notation);
        Assert.Equal("1D3", Ruleset.Medicine.CharacteristicRestorationSpecial.Notation);
        Assert.Equal("1D3+1", Ruleset.Medicine.CharacteristicRestorationCritical.Notation);
    }

    // (tier, requiresRoll, difficulty, naturalHealing, fumbleAdditionalDamage-or-null, allowsAdditional)
    // -- one case per printed row of the Conditions of Medical Care table, Ch 6 p.157.
    public static IEnumerable<object?[]> PrintedRows()
    {
        yield return new object?[] { MedicalCareTier.Poor, true, HealingRollDifficulty.Difficult, "1D3", "1D3", false };
        yield return new object?[] { MedicalCareTier.Decent, false, HealingRollDifficulty.None, "1D3", null, false };
        yield return new object?[] { MedicalCareTier.Excellent, false, HealingRollDifficulty.None, "1D3", null, true };
    }

    [Theory]
    [MemberData(nameof(PrintedRows))]
    public void Every_conditions_of_medical_care_row_matches_the_book_page_157(
        MedicalCareTier tier,
        bool requiresRoll,
        HealingRollDifficulty difficulty,
        string naturalHealing,
        string? fumbleAdditionalDamage,
        bool allowsAdditional)
    {
        var row = Ruleset.ConditionsOfMedicalCare.ForTier(tier);

        Assert.Equal(tier, row.Tier);
        Assert.Equal(requiresRoll, row.RequiresCaregiverRoll);
        Assert.Equal(difficulty, row.CaregiverRollDifficulty);
        Assert.Equal(naturalHealing, row.NaturalHealing.Notation);
        Assert.False(string.IsNullOrWhiteSpace(row.Conditions));

        if (fumbleAdditionalDamage is null)
        {
            Assert.Null(row.FumbleAdditionalDamage);
        }
        else
        {
            Assert.NotNull(row.FumbleAdditionalDamage);
            Assert.Equal(fumbleAdditionalDamage, row.FumbleAdditionalDamage!.Notation);
        }

        Assert.Equal(allowsAdditional, row.AllowsAdditionalHealing);
    }

    [Fact]
    public void The_shipped_conditions_table_has_exactly_the_three_printed_rows()
    {
        Assert.Equal(3, Ruleset.ConditionsOfMedicalCare.Rows.Count);
    }
}
