using Brp.Rules.Gear;

namespace Brp.Rules.Combat;

/// <summary>
/// Applies a landed hit's damage to an inanimate object's remaining hit points and armor value,
/// per Ch 8: Equipment, "Damage to Inanimate Objects" (p.224): "assigning an armor value based on
/// its equivalent... If the damage exceeds the object's armor value, then the hit points are
/// reduced by the remaining damage and that many damage points reduce its armor value
/// (representing how much less it is able to withstand damage once damaged)." This is the item
/// SIZ/hit-points breaking rule (#230), sourced to "General Qualities of Objects" and "Damage to
/// Inanimate Objects" (p.224), "Armor Value of Substances" (p.224), and "SIZ of Common Objects"
/// (pp.225-226) -- see <see cref="BreakableItemDefinition"/> and
/// <c>docs/decisions/NNNN-item-hit-points.md</c> for the per-item sourcing.
/// <para>
/// <strong>Deliberately reuses <see cref="DamageResolver.RollDamage"/> unchanged</strong> for the
/// attack roll itself and its armor-subtraction arithmetic (<see cref="DamageRoll.DamageDealt"/>
/// is already "raw damage minus armor value, floored at zero" -- exactly p.224's "the hit points
/// are reduced by the remaining damage"): there is no bespoke damage-rolling path for items, only
/// this extra armor-degradation step the book adds for objects but withholds for characters (a
/// character's armor never wears down; an object's does). A caller rolls the attacking weapon's
/// damage with <c>DamageResolver.RollDamage(LandedGrade.Normal, ArmorTreatment.Subtracted,
/// weapon, damageBonus, currentArmorValue, damageRuleset, entropy)</c> and passes the resulting
/// <see cref="DamageRoll"/> here.
/// </para>
/// <para>
/// Only objects smaller than or about human-sized are handled -- every item hand-picked into
/// <c>item-hit-points-ruleset.json</c> (doors, windows, locks). The "larger than human-sized, a
/// human-sized hole punched through one segment" branch of p.224 does not apply to this set and
/// is out of scope; see <see cref="BreakableItemCondition"/>.
/// </para>
/// </summary>
public static class BreakableItemResolver
{
    /// <summary>
    /// Applies <paramref name="damage"/> -- already rolled against <paramref name="currentArmorValue"/>
    /// via <see cref="DamageResolver.RollDamage"/> -- to an object with
    /// <paramref name="currentHitPoints"/> hit points and <paramref name="currentArmorValue"/>
    /// armor. <see cref="DamageRoll.DamageDealt"/> reduces both hit points and (by the same
    /// amount) the armor value itself (p.224); a Miss changes nothing.
    /// </summary>
    /// <param name="currentHitPoints">The object's hit points before this hit.</param>
    /// <param name="currentArmorValue">
    /// The object's armor value before this hit -- must be the same value <paramref name="damage"/>
    /// was rolled against.
    /// </param>
    /// <param name="damage">The already-rolled damage for this hit.</param>
    public static BreakableItemDamageResult ApplyDamage(int currentHitPoints, int currentArmorValue, DamageRoll damage)
    {
        ArgumentNullException.ThrowIfNull(damage);
        ArgumentOutOfRangeException.ThrowIfNegative(currentArmorValue);

        if (damage.LandedGrade == LandedGrade.Miss)
        {
            return new BreakableItemDamageResult(
                0, currentHitPoints, currentArmorValue, ClassifyCondition(currentHitPoints));
        }

        var resultingHitPoints = currentHitPoints - damage.DamageDealt;
        var resultingArmorValue = Math.Max(0, currentArmorValue - damage.DamageDealt);
        return new BreakableItemDamageResult(
            damage.DamageDealt, resultingHitPoints, resultingArmorValue, ClassifyCondition(resultingHitPoints));
    }

    private static BreakableItemCondition ClassifyCondition(int hitPoints) =>
        hitPoints <= 0 ? BreakableItemCondition.Destroyed : BreakableItemCondition.Intact;
}
