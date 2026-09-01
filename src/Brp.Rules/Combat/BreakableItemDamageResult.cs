namespace Brp.Rules.Combat;

/// <summary>
/// The result of applying one landed hit's damage to an inanimate object --
/// <see cref="BreakableItemResolver.ApplyDamage"/>'s output.
/// </summary>
/// <param name="DamageDealt">The hit points actually removed (0 for a Miss).</param>
/// <param name="ResultingHitPoints">The object's hit points after the change; may be negative.</param>
/// <param name="ResultingArmorValue">
/// The object's armor value after the change: reduced by exactly 1 for any landed (non-Miss) hit,
/// per the substance-armor worked example (Ch 8, p.225: "reducing the armor value by 1 with each
/// successful hit") -- not by <see cref="DamageDealt"/>; floored at zero, never negative. See
/// <see cref="BreakableItemResolver"/>'s remarks for the full reconciliation against p.224's more
/// general (but less specific, for these substance-armored items) phrasing.
/// </param>
/// <param name="Condition">The object's condition <see cref="ResultingHitPoints"/> puts it in.</param>
public sealed record BreakableItemDamageResult(
    int DamageDealt,
    int ResultingHitPoints,
    int ResultingArmorValue,
    BreakableItemCondition Condition);
