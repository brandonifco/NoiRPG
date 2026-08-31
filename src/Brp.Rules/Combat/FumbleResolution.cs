namespace Brp.Rules.Combat;

/// <summary>
/// The full result of resolving a fumble on one of the four Ch 6 tables (pp.148-149): the table
/// used and every consequence that accrued. Usually a single <see cref="FumbleStep"/>, but a
/// "blow it" (99) or "blow it badly" (00) result rolls further times on the same table and each
/// resulting step is accumulated here in roll order -- cumulatively, so a reroll that lands on
/// 99/00 again adds still more. See <c>docs/decisions/0020-fumble-tables.md</c>.
/// </summary>
/// <param name="Table">Which of the four tables was rolled on.</param>
/// <param name="Steps">
/// Every roll made, in order: the initial roll first, then any reroll markers and the rolls they
/// triggered. A caller applies each step's effect; the <see cref="FumbleEffectKind.Reroll"/> steps
/// are the audit trail of why more rolls followed and carry no effect of their own.
/// </param>
public sealed record FumbleResolution(FumbleTable Table, IReadOnlyList<FumbleStep> Steps);

/// <summary>
/// One D100 roll on a fumble table and the row it landed on, plus -- for the rows that branch on a
/// caller fact -- which branch applies. No dice inside the effect (rounds lost, meters, weapon hit
/// points) are rolled here, and no damage is applied: every effect is a structured outcome the
/// caller applies (the #50/#96 caller seam).
/// </summary>
/// <param name="Roll">The D100 result that selected this row (1-100, with 00 read as 100).</param>
/// <param name="Row">The matched ruleset row, carrying the effect and any structured quantities.</param>
/// <param name="Branch">
/// For a row with a <see cref="FumbleConsequenceRow.Fallback"/> (the hit-ally rows and the one
/// missile weapon-hit-point row), the resolved branch selection -- which of the primary effect and
/// the printed fallback applies, and the fallback row itself. <see langword="null"/> for every other
/// row.
/// </param>
public sealed record FumbleStep(int Roll, FumbleConsequenceRow Row, FumbleBranchSelection? Branch = null);

/// <summary>
/// The resolution of a fumble row's "primary effect, or use result NN-NN if ..." branch (Ch 6,
/// pp.148-149). Both branches are always named so a caller can see the whole rule; which one applies
/// depends on the row's <see cref="FumbleFallbackCondition"/>.
/// </summary>
/// <param name="Condition">The caller fact that selects between the two branches.</param>
/// <param name="PrimaryApplies">
/// Whether the row's primary effect (hit the ally; damage the weapon's hit points) applies rather
/// than the fallback. For <see cref="FumbleFallbackCondition.NoAllyNearby"/> this is the
/// <see cref="Core.Contests.IFumbleAdjudicator.IsAllyInRange"/> answer. For
/// <see cref="FumbleFallbackCondition.WeaponHasNoHitPoints"/> it is <see langword="null"/> -- this
/// layer does not model weapon hit points, so the caller decides; both branches are reported.
/// </param>
/// <param name="FallbackRow">
/// The row the fallback names ("use result NN-NN"), resolved on the same table. This is the effect
/// that applies when the primary does not.
/// </param>
public sealed record FumbleBranchSelection(
    FumbleFallbackCondition Condition,
    bool? PrimaryApplies,
    FumbleConsequenceRow FallbackRow);
