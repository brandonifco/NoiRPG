using Brp.Core.Primitives;

namespace Brp.Core.Contests;

/// <summary>
/// The full record of one resolved resistance roll, per Ch 5: System, "Resistance Rolls" and
/// "The Resistance Table" (BRP ORC Content Document, p.129).
/// <para>
/// Deliberately binary, not graded on the five-level <c>Resolution.SuccessLevel</c> scale: Ch 5,
/// "Critical Results, Special Successes, and Fumbles on Resistance Rolls" (p.130) states
/// "Usually resistance rolls have yes/no results -- success or failure -- but your gamemaster
/// <em>may choose</em> to characterize results more granularly" -- finer grading is an explicit
/// gamemaster option, not the default result shape, so this type does not carry a
/// <c>SuccessLevel</c>.
/// </para>
/// </summary>
/// <param name="Roll">
/// The percentile result the outcome was decided from, in <c>[1, 100]</c> -- a printed roll of
/// <c>00</c> is represented as <c>100</c>.
/// </param>
/// <param name="Active">The active characteristic (or other measurable quantity) rated.</param>
/// <param name="Passive">The passive characteristic (or other measurable quantity) resisted.</param>
/// <param name="Chance">
/// The linear chance the resistance formula computes: <c>50% + 5% x (Active - Passive)</c>
/// (Ch 5, "Resistance Rolls", p.129). Only meaningful as the roll target inside the printed
/// table's bounds -- see <see cref="IsAutomaticSuccess"/> / <see cref="IsAutomaticFailure"/> for
/// what governs the result outside them. Floored at zero per <see cref="Percent"/>.
/// </param>
/// <param name="IsAutomaticSuccess">
/// True when the characteristic difference puts the roll in the table's automatic-success zone
/// (Ch 5, "The Resistance Table" caption, p.130: "over 95% in the range of automatic success").
/// </param>
/// <param name="IsAutomaticFailure">
/// True when the characteristic difference puts the roll in the table's automatic-failure zone
/// (same caption: "Changes below 05% are in the range of automatic failure").
/// </param>
/// <param name="Succeeded">The resolved yes/no result.</param>
public sealed record ResistanceOutcome(
    int Roll,
    int Active,
    int Passive,
    Percent Chance,
    bool IsAutomaticSuccess,
    bool IsAutomaticFailure,
    bool Succeeded);
