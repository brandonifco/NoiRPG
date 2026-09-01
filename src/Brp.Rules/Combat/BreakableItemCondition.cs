namespace Brp.Rules.Combat;

/// <summary>
/// An inanimate object's condition after a <see cref="BreakableItemResolver.ApplyDamage"/> call.
/// Ch 8: Equipment, "Damage to Inanimate Objects" (p.224): "If an object is smaller than
/// human-sized (such as a chair), it is totally destroyed if it is reduced to 0 hit points."
/// Every item hand-picked into <c>item-hit-points-ruleset.json</c> (doors, windows, locks) is
/// human-sized or smaller, so only this branch of the printed rule applies -- the "larger than
/// human-sized, human-sized hole punched through a segment" branch is out of scope for this set
/// (see <c>docs/decisions/0033-item-hit-points.md</c>).
/// </summary>
public enum BreakableItemCondition
{
    /// <summary>Above 0 hit points -- the object still functions (a door still bars the way, etc.).</summary>
    Intact,

    /// <summary>At or below 0 hit points -- the object is destroyed (p.224).</summary>
    Destroyed,
}
