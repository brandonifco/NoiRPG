using Brp.Core.Resolution;

namespace Brp.Rules.Combat;

/// <summary>
/// Resolves an attack against a defense (or its absence) by cross-referencing the attacker's
/// and defender's success grades against the data-driven attack/defense matrix
/// (<see cref="AttackDefenseMatrixRuleset"/>), per Ch 6: Combat, "Attack and Defense Matrix"
/// (p.147). This is Layer 4 piece C (#49); its output is the seam piece D (damage) consumes --
/// see <c>docs/decisions/0016-attack-defense-matrix.md</c>.
/// <para>
/// Deliberately not an if/switch chain over grades (AGENTS.md invariant 7): the lookup walks
/// <see cref="AttackDefenseMatrixRuleset.Cells"/> and
/// <see cref="AttackDefenseMatrixRuleset.UndefendedOutcomes"/> for a matching row; every value
/// in the returned <see cref="AttackDefenseOutcome"/> comes from ruleset data, not a hardcoded
/// branch on grade names.
/// </para>
/// </summary>
public static class AttackDefenseResolver
{
    /// <summary>
    /// Resolves one attack.
    /// </summary>
    /// <param name="attackerGrade">The attacker's rolled success grade.</param>
    /// <param name="defenseType">
    /// The defense the defender used -- <see cref="DefenseType.None"/> if the defender took no
    /// defensive action at all.
    /// </param>
    /// <param name="defenderGrade">
    /// The defender's rolled success grade, already reflecting any cumulative successive-parry
    /// or successive-dodge -30% penalty (Ch 6, "Parry"/"Dodge", p.144) the caller applied before
    /// rolling -- that penalty is a modifier on the defense roll, not a matrix concern, and this
    /// resolver consumes an already-computed grade (see the ADR's -30% seam decision). Required
    /// when <paramref name="defenseType"/> is <see cref="DefenseType.Parry"/> or
    /// <see cref="DefenseType.Dodge"/> and <paramref name="attackerGrade"/> is
    /// <see cref="SuccessLevel.Critical"/>, <see cref="SuccessLevel.Special"/>, or
    /// <see cref="SuccessLevel.Success"/>; ignored otherwise (an attacker Failure or Fumble
    /// needs no defender roll at all, per the matrix's "&#8212;" columns, p.147).
    /// </param>
    /// <param name="ruleset">The data-driven attack/defense matrix.</param>
    public static AttackDefenseOutcome Resolve(
        SuccessLevel attackerGrade,
        DefenseType defenseType,
        SuccessLevel? defenderGrade,
        AttackDefenseMatrixRuleset ruleset)
    {
        ArgumentNullException.ThrowIfNull(ruleset);

        // Ch 6, p.147: an attacker Failure or Fumble needs no defender roll regardless of the
        // defense type the defender might otherwise have used.
        if (attackerGrade is SuccessLevel.Failure or SuccessLevel.Fumble)
        {
            return FindCell(ruleset, attackerGrade, defenderGrade: null).Outcome;
        }

        if (defenseType == DefenseType.None)
        {
            if (!ruleset.UndefendedOutcomes.TryGetValue(attackerGrade, out var undefendedOutcome))
            {
                throw new ArgumentException(
                    $"The ruleset has no undefended outcome for attacker grade '{attackerGrade}'.",
                    nameof(attackerGrade));
            }

            return undefendedOutcome;
        }

        if (defenderGrade is null)
        {
            throw new ArgumentException(
                "A defender grade is required when the defender used a Parry or Dodge and the " +
                "attacker did not fail or fumble.",
                nameof(defenderGrade));
        }

        var cell = FindCell(ruleset, attackerGrade, defenderGrade);
        var outcome = cell.Outcome;

        // Ch 6, p.147, footnoted cells: weapon damage from a parry attempt applies only to
        // Parry, never Dodge -- a dodge has no weapon to damage.
        if (defenseType != DefenseType.Parry && outcome.ParryWeaponDamage is not null)
        {
            outcome = outcome with { ParryWeaponDamage = null };
        }

        return outcome;
    }

    private static AttackDefenseMatrixCell FindCell(
        AttackDefenseMatrixRuleset ruleset, SuccessLevel attackerGrade, SuccessLevel? defenderGrade)
    {
        foreach (var cell in ruleset.Cells)
        {
            if (cell.AttackerGrade == attackerGrade && cell.DefenderGrade == defenderGrade)
            {
                return cell;
            }
        }

        throw new ArgumentException(
            $"The ruleset has no matrix cell for attacker grade '{attackerGrade}' and " +
            $"defender grade '{defenderGrade?.ToString() ?? "none"}'.");
    }
}
