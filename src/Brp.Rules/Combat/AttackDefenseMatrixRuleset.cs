using Brp.Core.Resolution;

namespace Brp.Rules.Combat;

/// <summary>
/// The data-defined attack/defense matrix <see cref="AttackDefenseResolver"/> reads (AGENTS.md
/// invariant 7: rules values are data, not constants). Every cell is transcribed row-for-row
/// from Ch 6: Combat, "Attack and Defense Matrix" (p.147). Loaded from
/// <c>attack-defense-matrix-ruleset.json</c> by <c>Brp.Data.NoirAttackDefenseMatrixRuleset.Load()</c>.
/// See <c>docs/decisions/0016-attack-defense-matrix.md</c>.
/// </summary>
public sealed class AttackDefenseMatrixRuleset
{
    /// <summary>Creates an attack/defense matrix ruleset from data-defined cells.</summary>
    public AttackDefenseMatrixRuleset(
        IReadOnlyList<AttackDefenseMatrixCell> cells,
        IReadOnlyDictionary<SuccessLevel, AttackDefenseOutcome> undefendedOutcomes)
    {
        ArgumentNullException.ThrowIfNull(cells);
        ArgumentNullException.ThrowIfNull(undefendedOutcomes);

        if (cells.Count == 0)
        {
            throw new ArgumentException("The attack/defense matrix must have at least one cell.", nameof(cells));
        }

        Cells = cells;
        UndefendedOutcomes = undefendedOutcomes;
    }

    /// <summary>
    /// Every printed (attacker grade, defender grade) cell of the matrix (p.147) -- 17 cells:
    /// 5 defender columns each for attacker grades Critical/Special/Success, plus one row each
    /// (with a null defender grade) for attacker grades Failure and Fumble.
    /// </summary>
    public IReadOnlyList<AttackDefenseMatrixCell> Cells { get; }

    /// <summary>
    /// The direct-application outcome for each attacker grade when the defender takes no
    /// defensive action at all (<see cref="DefenseType.None"/>) -- not itself a printed matrix
    /// column; see <c>docs/decisions/0016-attack-defense-matrix.md</c> for the derivation. Keyed
    /// only by the attacker grades that need a defender roll at all (Critical/Special/Success) --
    /// Failure and Fumble need no defender grade regardless of defense type and are covered by
    /// <see cref="Cells"/> directly.
    /// </summary>
    public IReadOnlyDictionary<SuccessLevel, AttackDefenseOutcome> UndefendedOutcomes { get; }
}
