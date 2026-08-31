using System.Reflection;
using Brp.Core.Resolution;
using Brp.Rules.Combat;

namespace Brp.Rules.Tests.Combat;

/// <summary>
/// Covers <see cref="AttackDefenseResolver"/> against Ch 6: Combat, "Attack and Defense Matrix"
/// (p.147) -- every matrix cell, the undefended case, the parry-only weapon-damage flag, the
/// fumble-table flags, and the absence of any shield concept. See
/// <c>docs/decisions/0016-attack-defense-matrix.md</c>.
/// </summary>
public class AttackDefenseResolverTests
{
    private static readonly AttackDefenseMatrixRuleset Ruleset = BuildRuleset();

    // ---- Every printed matrix cell, via the resolver, for both Parry and Dodge -----------------

    public static IEnumerable<object[]> AllDefendedCellsAndDefenseTypes()
    {
        var grades = new[] { SuccessLevel.Critical, SuccessLevel.Special, SuccessLevel.Success, SuccessLevel.Failure, SuccessLevel.Fumble };
        foreach (var attackerGrade in new[] { SuccessLevel.Critical, SuccessLevel.Special, SuccessLevel.Success })
        {
            foreach (var defenderGrade in grades)
            {
                foreach (var defenseType in new[] { DefenseType.Parry, DefenseType.Dodge })
                {
                    yield return new object[] { attackerGrade, defenderGrade, defenseType };
                }
            }
        }
    }

    [Theory]
    [MemberData(nameof(AllDefendedCellsAndDefenseTypes))]
    public void Resolve_matches_the_matrix_cell_for_every_attacker_defender_grade_pair(
        SuccessLevel attackerGrade, SuccessLevel defenderGrade, DefenseType defenseType)
    {
        var expectedCell = Ruleset.Cells.Single(
            c => c.AttackerGrade == attackerGrade && c.DefenderGrade == defenderGrade);

        var outcome = AttackDefenseResolver.Resolve(attackerGrade, defenseType, defenderGrade, Ruleset);

        Assert.Equal(expectedCell.Outcome.LandedGrade, outcome.LandedGrade);
        Assert.Equal(expectedCell.Outcome.ArmorTreatment, outcome.ArmorTreatment);
        Assert.Equal(expectedCell.Outcome.DefenderRollsOnFumbleTable, outcome.DefenderRollsOnFumbleTable);
        Assert.Equal(expectedCell.Outcome.AttackerRollsOnFumbleTable, outcome.AttackerRollsOnFumbleTable);
    }

    [Theory]
    [InlineData(SuccessLevel.Critical)]
    [InlineData(SuccessLevel.Special)]
    [InlineData(SuccessLevel.Success)]
    public void An_attacker_Failure_or_Fumble_needs_no_defender_grade_regardless_of_defense_type(
        SuccessLevel unusedAttackerGradeMarker)
    {
        // Silences an unused-parameter warning while keeping the theory's intent legible; the
        // real cases below are the Failure/Fumble rows themselves.
        _ = unusedAttackerGradeMarker;

        var failureOutcome = AttackDefenseResolver.Resolve(SuccessLevel.Failure, DefenseType.Parry, defenderGrade: null, Ruleset);
        Assert.Equal(LandedGrade.Miss, failureOutcome.LandedGrade);

        var fumbleOutcome = AttackDefenseResolver.Resolve(SuccessLevel.Fumble, DefenseType.None, defenderGrade: null, Ruleset);
        Assert.Equal(LandedGrade.Miss, fumbleOutcome.LandedGrade);
        Assert.True(fumbleOutcome.AttackerRollsOnFumbleTable);
    }

    // ---- The undefended (DefenseType.None) case: attacker's grade applies directly -------------

    [Fact]
    public void An_undefended_Critical_lands_as_Critical_with_armor_bypassed()
    {
        var outcome = AttackDefenseResolver.Resolve(SuccessLevel.Critical, DefenseType.None, defenderGrade: null, Ruleset);

        Assert.Equal(LandedGrade.Critical, outcome.LandedGrade);
        Assert.Equal(ArmorTreatment.Bypassed, outcome.ArmorTreatment);
    }

    [Fact]
    public void An_undefended_Special_lands_as_Special_with_armor_subtracted()
    {
        var outcome = AttackDefenseResolver.Resolve(SuccessLevel.Special, DefenseType.None, defenderGrade: null, Ruleset);

        Assert.Equal(LandedGrade.Special, outcome.LandedGrade);
        Assert.Equal(ArmorTreatment.Subtracted, outcome.ArmorTreatment);
    }

    [Fact]
    public void An_undefended_Success_lands_as_Normal_with_armor_subtracted()
    {
        var outcome = AttackDefenseResolver.Resolve(SuccessLevel.Success, DefenseType.None, defenderGrade: null, Ruleset);

        Assert.Equal(LandedGrade.Normal, outcome.LandedGrade);
        Assert.Equal(ArmorTreatment.Subtracted, outcome.ArmorTreatment);
    }

    [Fact]
    public void An_undefended_Failure_lands_as_a_Miss()
    {
        var outcome = AttackDefenseResolver.Resolve(SuccessLevel.Failure, DefenseType.None, defenderGrade: null, Ruleset);

        Assert.Equal(LandedGrade.Miss, outcome.LandedGrade);
    }

    [Fact]
    public void An_undefended_Fumble_lands_as_a_Miss_and_the_attacker_rolls_on_the_fumble_table()
    {
        var outcome = AttackDefenseResolver.Resolve(SuccessLevel.Fumble, DefenseType.None, defenderGrade: null, Ruleset);

        Assert.Equal(LandedGrade.Miss, outcome.LandedGrade);
        Assert.True(outcome.AttackerRollsOnFumbleTable);
    }

    // ---- Parry-weapon-damage: present on Parry, absent on Dodge, for the same cell -------------

    [Fact]
    public void The_same_cells_parry_weapon_damage_is_present_on_Parry_and_absent_on_Dodge()
    {
        var parried = AttackDefenseResolver.Resolve(SuccessLevel.Critical, DefenseType.Parry, SuccessLevel.Special, Ruleset);
        var dodged = AttackDefenseResolver.Resolve(SuccessLevel.Critical, DefenseType.Dodge, SuccessLevel.Special, Ruleset);

        Assert.NotNull(parried.ParryWeaponDamage);
        Assert.Equal(new ParryWeaponDamage(DamagedParty.Defender, 2), parried.ParryWeaponDamage);
        Assert.Null(dodged.ParryWeaponDamage);

        // Everything else about the outcome is identical between Parry and Dodge for this cell.
        Assert.Equal(parried.LandedGrade, dodged.LandedGrade);
        Assert.Equal(parried.ArmorTreatment, dodged.ArmorTreatment);
    }

    [Fact]
    public void A_Dodge_never_carries_parry_weapon_damage_across_every_cell_that_defines_it()
    {
        foreach (var cell in Ruleset.Cells.Where(c => c.Outcome.ParryWeaponDamage is not null))
        {
            var outcome = AttackDefenseResolver.Resolve(cell.AttackerGrade, DefenseType.Dodge, cell.DefenderGrade, Ruleset);
            Assert.Null(outcome.ParryWeaponDamage);
        }
    }

    // ---- Fumble-table flags -----------------------------------------------------------------

    [Fact]
    public void Defender_fumble_table_flag_is_set_only_on_the_defender_Fumble_column()
    {
        var outcome = AttackDefenseResolver.Resolve(SuccessLevel.Success, DefenseType.Dodge, SuccessLevel.Fumble, Ruleset);

        Assert.True(outcome.DefenderRollsOnFumbleTable);
    }

    [Fact]
    public void Attacker_fumble_table_flag_is_set_only_on_the_attacker_Fumble_row()
    {
        var outcome = AttackDefenseResolver.Resolve(SuccessLevel.Fumble, DefenseType.Parry, defenderGrade: null, Ruleset);

        Assert.True(outcome.AttackerRollsOnFumbleTable);
    }

    // ---- Scope: no shield concept anywhere ----------------------------------------------------

    [Fact]
    public void No_shield_concept_exists_anywhere_in_the_attack_defense_types()
    {
        var assembly = typeof(AttackDefenseResolver).Assembly;
        var combatTypes = assembly.GetTypes().Where(t => t.Namespace == "Brp.Rules.Combat");

        foreach (var type in combatTypes)
        {
            Assert.DoesNotContain("shield", type.Name, StringComparison.OrdinalIgnoreCase);

            var memberNames = type
                .GetMembers(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
                .Select(m => m.Name);

            foreach (var memberName in memberNames)
            {
                Assert.DoesNotContain("shield", memberName, StringComparison.OrdinalIgnoreCase);
            }

            if (type.IsEnum)
            {
                foreach (var enumName in Enum.GetNames(type))
                {
                    Assert.DoesNotContain("shield", enumName, StringComparison.OrdinalIgnoreCase);
                }
            }
        }
    }

    private static AttackDefenseMatrixRuleset BuildRuleset()
    {
        AttackDefenseOutcome Outcome(
            LandedGrade landedGrade,
            ArmorTreatment armorTreatment,
            ParryWeaponDamage? parryWeaponDamage = null,
            bool defenderFumble = false,
            bool attackerFumble = false) => new(
                landedGrade, armorTreatment, parryWeaponDamage, defenderFumble, attackerFumble, "test fixture");

        var cells = new List<AttackDefenseMatrixCell>
        {
            new(SuccessLevel.Critical, SuccessLevel.Critical, Outcome(LandedGrade.Miss, ArmorTreatment.NotApplicable)),
            new(SuccessLevel.Critical, SuccessLevel.Special, Outcome(LandedGrade.Normal, ArmorTreatment.Subtracted, new ParryWeaponDamage(DamagedParty.Defender, 2))),
            new(SuccessLevel.Critical, SuccessLevel.Success, Outcome(LandedGrade.Special, ArmorTreatment.Subtracted, new ParryWeaponDamage(DamagedParty.Defender, 4))),
            new(SuccessLevel.Critical, SuccessLevel.Failure, Outcome(LandedGrade.Critical, ArmorTreatment.Bypassed)),
            new(SuccessLevel.Critical, SuccessLevel.Fumble, Outcome(LandedGrade.Critical, ArmorTreatment.DoesNotApply, defenderFumble: true)),

            new(SuccessLevel.Special, SuccessLevel.Critical, Outcome(LandedGrade.Miss, ArmorTreatment.NotApplicable, new ParryWeaponDamage(DamagedParty.Attacker, 1))),
            new(SuccessLevel.Special, SuccessLevel.Special, Outcome(LandedGrade.Miss, ArmorTreatment.NotApplicable)),
            new(SuccessLevel.Special, SuccessLevel.Success, Outcome(LandedGrade.Normal, ArmorTreatment.Subtracted, new ParryWeaponDamage(DamagedParty.Defender, 2))),
            new(SuccessLevel.Special, SuccessLevel.Failure, Outcome(LandedGrade.Special, ArmorTreatment.Subtracted)),
            new(SuccessLevel.Special, SuccessLevel.Fumble, Outcome(LandedGrade.Special, ArmorTreatment.Subtracted, defenderFumble: true)),

            new(SuccessLevel.Success, SuccessLevel.Critical, Outcome(LandedGrade.Miss, ArmorTreatment.NotApplicable, new ParryWeaponDamage(DamagedParty.Attacker, 2))),
            new(SuccessLevel.Success, SuccessLevel.Special, Outcome(LandedGrade.Miss, ArmorTreatment.NotApplicable, new ParryWeaponDamage(DamagedParty.Attacker, 1))),
            new(SuccessLevel.Success, SuccessLevel.Success, Outcome(LandedGrade.Miss, ArmorTreatment.NotApplicable)),
            new(SuccessLevel.Success, SuccessLevel.Failure, Outcome(LandedGrade.Normal, ArmorTreatment.Subtracted)),
            new(SuccessLevel.Success, SuccessLevel.Fumble, Outcome(LandedGrade.Normal, ArmorTreatment.Subtracted, defenderFumble: true)),

            new(SuccessLevel.Failure, null, Outcome(LandedGrade.Miss, ArmorTreatment.NotApplicable)),
            new(SuccessLevel.Fumble, null, Outcome(LandedGrade.Miss, ArmorTreatment.NotApplicable, attackerFumble: true)),
        };

        var undefendedOutcomes = new Dictionary<SuccessLevel, AttackDefenseOutcome>
        {
            [SuccessLevel.Critical] = Outcome(LandedGrade.Critical, ArmorTreatment.Bypassed),
            [SuccessLevel.Special] = Outcome(LandedGrade.Special, ArmorTreatment.Subtracted),
            [SuccessLevel.Success] = Outcome(LandedGrade.Normal, ArmorTreatment.Subtracted),
        };

        return new AttackDefenseMatrixRuleset(cells, undefendedOutcomes);
    }
}
