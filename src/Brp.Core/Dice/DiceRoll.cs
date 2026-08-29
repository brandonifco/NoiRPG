namespace Brp.Core.Dice;

/// <summary>
/// One evaluated term of a dice expression.
/// </summary>
/// <param name="Notation">The term as written, for display and provenance.</param>
/// <param name="Value">The term's signed contribution to the total.</param>
/// <param name="Faces">
/// Individual die faces, in roll order. Empty for constant terms. Exposed because the
/// UI shows dice and the replay log has to be able to reconstruct exactly what happened,
/// neither of which a bare total supports.
/// </param>
public sealed record DiceTermResult(string Notation, int Value, IReadOnlyList<int> Faces);

/// <summary>
/// The result of evaluating a dice expression.
/// </summary>
/// <param name="Total">
/// The sum of all terms, floored at zero. The source states that die rolls are never
/// modified below zero, so a large negative damage modifier yields no damage rather
/// than healing the target.
/// </param>
/// <param name="RawTotal">
/// The sum before the zero floor is applied. Kept so a test or a log can tell the
/// difference between "rolled exactly zero" and "rolled negative and was clamped".
/// </param>
/// <param name="Terms">Each term's contribution, in written order.</param>
public sealed record DiceRoll(int Total, int RawTotal, IReadOnlyList<DiceTermResult> Terms)
{
    /// <summary>True when the zero floor changed the result.</summary>
    public bool WasFloored => RawTotal < 0;
}
