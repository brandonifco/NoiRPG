namespace Brp.Rules.Combat;

/// <summary>
/// The result of applying one landed hit's damage to an inanimate object --
/// <see cref="BreakableItemResolver.ApplyDamage"/>'s output.
/// </summary>
/// <param name="DamageDealt">The hit points actually removed (0 for a Miss).</param>
/// <param name="ResultingHitPoints">The object's hit points after the change; may be negative.</param>
/// <param name="ResultingArmorValue">
/// The object's armor value after the change, per Ch 8, p.224: "that many damage points reduce
/// its armor value" -- floored at zero, never negative.
/// </param>
/// <param name="Condition">The object's condition <see cref="ResultingHitPoints"/> puts it in.</param>
public sealed record BreakableItemDamageResult(
    int DamageDealt,
    int ResultingHitPoints,
    int ResultingArmorValue,
    BreakableItemCondition Condition);
