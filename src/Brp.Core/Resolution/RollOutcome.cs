using Brp.Core.Primitives;

namespace Brp.Core.Resolution;

/// <summary>
/// The full record of one resolved action roll -- not just the grade, but everything that
/// produced it: the raw roll, the chance it was judged against, and the exact thresholds
/// computed from that chance. Deliberately never collapsed to a bare <c>bool</c> or a bare
/// <see cref="SuccessLevel"/>: the game shows the player the real probability behind a
/// result, which is only possible if the result can explain itself after the fact.
/// </summary>
/// <param name="Roll">
/// The percentile result the grade was decided from, in <c>[1, 100]</c> -- a printed roll of
/// <c>00</c> is represented as <c>100</c>, per <see cref="Randomness.IEntropySource.NextD100"/>.
/// </param>
/// <param name="BaseChance">
/// The skill's unmodified base chance. Carried for provenance and used only to evaluate the
/// 5%-floor rule (Ch 5: System, "Skill Rolls", p.128); every other threshold below is derived
/// from <see cref="EffectiveChance"/> alone.
/// </param>
/// <param name="EffectiveChance">
/// The modified chance of success the roll was actually resolved against. Where that
/// modified chance came from (situational modifiers, difficulty, etc.) is out of scope here.
/// </param>
/// <param name="CriticalThreshold">
/// A roll at or below this value is a critical success: 1/20th of
/// <see cref="EffectiveChance"/>, rounded up (Ch 5, "Critical Success", p.128).
/// </param>
/// <param name="SpecialThreshold">
/// A roll at or below this value is a special success: 1/5th of <see cref="EffectiveChance"/>,
/// rounded up (Ch 5, "Special Success", p.128). This range contains
/// <see cref="CriticalThreshold"/>'s range; the two never stack -- a roll in both bands
/// resolves as <see cref="SuccessLevel.Critical"/> only.
/// </param>
/// <param name="FumbleThreshold">
/// A roll at or above this value is a fumble. Always <c>100</c> at most, so a roll of
/// <c>100</c> (printed <c>00</c>) is unconditionally a fumble (Ch 5, "Fumble", p.127: "A roll
/// of 00 is always a fumble, no matter what the skill rating is").
/// </param>
/// <param name="Level">The resulting grade.</param>
public sealed record RollOutcome(
    int Roll,
    Percent BaseChance,
    Percent EffectiveChance,
    Percent CriticalThreshold,
    Percent SpecialThreshold,
    Percent FumbleThreshold,
    SuccessLevel Level)
{
    /// <summary>True for <see cref="SuccessLevel.Success"/> or better.</summary>
    public bool Succeeded => Level >= SuccessLevel.Success;
}
