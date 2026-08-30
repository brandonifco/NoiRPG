using Brp.Core.Dice;

namespace Brp.Rules.Gear;

/// <summary>
/// One range increment of a weapon whose damage falls off with distance -- currently only
/// shotguns in the hand-picked subset. Sourced: Ch 8: Equipment, Modern Missile Weapons table
/// (p.201), note 6: "Shotguns do damage by range; the first increment is the first damage
/// dice, the second is the second, etc."
/// </summary>
/// <param name="Range">
/// The upper bound of this increment, in the book's range units (Ch 1, p.20: "meters (yards)"
/// -- treated as interchangeable).
/// </param>
/// <param name="Damage">The damage dice rolled for a hit at or within <see cref="Range"/>.</param>
public sealed record RangeIncrementDamage(int Range, DiceExpression Damage);
