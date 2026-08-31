using Brp.Core.Resolution;

namespace Brp.Rules.Combat;

/// <summary>
/// One (attacker grade, defender grade) cell of the printed attack/defense matrix (Ch 6:
/// Combat, "Attack and Defense Matrix", p.147). <see cref="DefenderGrade"/> is <c>null</c> for
/// the Failure and Fumble attacker rows, which the book marks "&#8212;" (no defender roll
/// required) rather than naming a defender grade.
/// </summary>
public sealed record AttackDefenseMatrixCell(
    SuccessLevel AttackerGrade,
    SuccessLevel? DefenderGrade,
    AttackDefenseOutcome Outcome);
