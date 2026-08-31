using Brp.Core.Resolution;
using Brp.Rules.Combat;

namespace Brp.Data.Tests;

/// <summary>
/// Confirms the shipped attack/defense matrix data loads and reproduces every cell of Ch 6:
/// Combat, "Attack and Defense Matrix" (p.147), cell by cell rather than sampled -- per
/// AGENTS.md's "table-backed rule" convention. See <c>docs/decisions/0016-attack-defense-matrix.md</c>.
/// </summary>
public class NoirAttackDefenseMatrixRulesetTests
{
    private static readonly AttackDefenseMatrixRuleset Ruleset = NoirAttackDefenseMatrixRuleset.Load();

    public static IEnumerable<object?[]> PrintedCells()
    {
        // (attackerGrade, defenderGrade, landedGrade, armorTreatment, parryDamage, defenderFumble, attackerFumble)
        yield return new object?[] { SuccessLevel.Critical, SuccessLevel.Critical, LandedGrade.Miss, ArmorTreatment.NotApplicable, null, false, false };
        yield return new object?[] { SuccessLevel.Critical, SuccessLevel.Special, LandedGrade.Normal, ArmorTreatment.Subtracted, new ParryWeaponDamage(DamagedParty.Defender, 2), false, false };
        yield return new object?[] { SuccessLevel.Critical, SuccessLevel.Success, LandedGrade.Special, ArmorTreatment.Subtracted, new ParryWeaponDamage(DamagedParty.Defender, 4), false, false };
        yield return new object?[] { SuccessLevel.Critical, SuccessLevel.Failure, LandedGrade.Critical, ArmorTreatment.Bypassed, null, false, false };
        yield return new object?[] { SuccessLevel.Critical, SuccessLevel.Fumble, LandedGrade.Critical, ArmorTreatment.DoesNotApply, null, true, false };

        yield return new object?[] { SuccessLevel.Special, SuccessLevel.Critical, LandedGrade.Miss, ArmorTreatment.NotApplicable, new ParryWeaponDamage(DamagedParty.Attacker, 1), false, false };
        yield return new object?[] { SuccessLevel.Special, SuccessLevel.Special, LandedGrade.Miss, ArmorTreatment.NotApplicable, null, false, false };
        yield return new object?[] { SuccessLevel.Special, SuccessLevel.Success, LandedGrade.Normal, ArmorTreatment.Subtracted, new ParryWeaponDamage(DamagedParty.Defender, 2), false, false };
        yield return new object?[] { SuccessLevel.Special, SuccessLevel.Failure, LandedGrade.Special, ArmorTreatment.Subtracted, null, false, false };
        yield return new object?[] { SuccessLevel.Special, SuccessLevel.Fumble, LandedGrade.Special, ArmorTreatment.Subtracted, null, true, false };

        yield return new object?[] { SuccessLevel.Success, SuccessLevel.Critical, LandedGrade.Miss, ArmorTreatment.NotApplicable, new ParryWeaponDamage(DamagedParty.Attacker, 2), false, false };
        yield return new object?[] { SuccessLevel.Success, SuccessLevel.Special, LandedGrade.Miss, ArmorTreatment.NotApplicable, new ParryWeaponDamage(DamagedParty.Attacker, 1), false, false };
        yield return new object?[] { SuccessLevel.Success, SuccessLevel.Success, LandedGrade.Miss, ArmorTreatment.NotApplicable, null, false, false };
        yield return new object?[] { SuccessLevel.Success, SuccessLevel.Failure, LandedGrade.Normal, ArmorTreatment.Subtracted, null, false, false };
        yield return new object?[] { SuccessLevel.Success, SuccessLevel.Fumble, LandedGrade.Normal, ArmorTreatment.Subtracted, null, true, false };

        yield return new object?[] { SuccessLevel.Failure, null, LandedGrade.Miss, ArmorTreatment.NotApplicable, null, false, false };
        yield return new object?[] { SuccessLevel.Fumble, null, LandedGrade.Miss, ArmorTreatment.NotApplicable, null, false, true };
    }

    [Theory]
    [MemberData(nameof(PrintedCells))]
    public void Every_printed_matrix_cell_matches_the_book(
        SuccessLevel attackerGrade,
        SuccessLevel? defenderGrade,
        LandedGrade expectedLandedGrade,
        ArmorTreatment expectedArmorTreatment,
        ParryWeaponDamage? expectedParryWeaponDamage,
        bool expectedDefenderFumble,
        bool expectedAttackerFumble)
    {
        var cell = Ruleset.Cells.Single(c => c.AttackerGrade == attackerGrade && c.DefenderGrade == defenderGrade);

        Assert.Equal(expectedLandedGrade, cell.Outcome.LandedGrade);
        Assert.Equal(expectedArmorTreatment, cell.Outcome.ArmorTreatment);
        Assert.Equal(expectedParryWeaponDamage, cell.Outcome.ParryWeaponDamage);
        Assert.Equal(expectedDefenderFumble, cell.Outcome.DefenderRollsOnFumbleTable);
        Assert.Equal(expectedAttackerFumble, cell.Outcome.AttackerRollsOnFumbleTable);
    }

    [Fact]
    public void The_shipped_ruleset_has_exactly_the_seventeen_printed_cells()
    {
        Assert.Equal(17, Ruleset.Cells.Count);
    }

    [Fact]
    public void The_shipped_ruleset_has_an_undefended_outcome_for_each_grade_that_needs_one()
    {
        Assert.Equal(
            new[] { SuccessLevel.Critical, SuccessLevel.Special, SuccessLevel.Success },
            Ruleset.UndefendedOutcomes.Keys.OrderByDescending(g => g));
    }
}
