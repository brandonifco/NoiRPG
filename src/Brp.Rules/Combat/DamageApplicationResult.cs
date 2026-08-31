using Brp.Rules.Characters;

namespace Brp.Rules.Combat;

/// <summary>
/// The result of applying a rolled damage amount to a target's hit points --
/// the output of <see cref="DamageResolver"/>'s <c>ApplyDamage</c> overloads.
/// </summary>
/// <param name="DamageDealt">The hit points actually removed (0 for a Miss).</param>
/// <param name="ResultingHitPoints">The target's hit points after the change; may be negative.</param>
/// <param name="Condition">The condition <see cref="ResultingHitPoints"/> puts the target in.</param>
/// <param name="Wound">
/// The wound recorded for this blow, for piece E to heal later. <see langword="null"/> for a
/// Miss, which lands no blow to record.
/// </param>
public sealed record DamageApplicationResult(
    int DamageDealt,
    int ResultingHitPoints,
    HitPointCondition Condition,
    Wound? Wound);
