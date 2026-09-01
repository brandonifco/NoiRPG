using Brp.Rules.Gear;

namespace Brp.Rules.Combat;

/// <summary>
/// Applies a landed hit's damage to an inanimate object's remaining hit points and armor value.
/// This is the item SIZ/hit-points breaking rule (#230), sourced to "General Qualities of
/// Objects", "Damage to Inanimate Objects", and "Armor Value of Substances" (all p.224), and
/// "SIZ of Common Objects" (pp.225-226) -- see <see cref="BreakableItemDefinition"/> and
/// <c>docs/decisions/0033-item-hit-points.md</c> for the per-item sourcing and the full
/// rules-interpretation record this summary follows.
/// <para>
/// <strong>Hit points</strong> follow "Damage to Inanimate Objects" (p.224) unchanged: "If the
/// damage exceeds the object's armor value, then the hit points are reduced by the remaining
/// damage" -- <see cref="DamageRoll.DamageDealt"/> already <em>is</em> that "remaining damage"
/// (raw damage minus armor value, floored at zero), computed by the reused, unmodified
/// <see cref="DamageResolver.RollDamage"/>. There is no bespoke damage-rolling path for items.
/// </para>
/// <para>
/// <strong>Armor degrades by exactly 1 per landed hit, not by the penetrating damage.</strong>
/// "Damage to Inanimate Objects" (p.224) says damage that gets through "reduce[s] its armor
/// value" by the same amount, but every item this ruleset ships (a door, a glass door, a glass
/// window, a padlock) draws its starting armor from "Armor Value of Substances" (p.224), and
/// that section's very next paragraph is more specific and directly contradicts a per-damage
/// degradation for exactly this kind of armor: "Natural armor values such as these above are not
/// lost and do not deteriorate through multiple attacks, unless through... a specific attempt to
/// reduce the armor value of an object" (p.224-225) -- and the book's only worked example of such
/// a deliberate attempt (repeatedly bashing bulletproof glass with a sledgehammer, p.225) reduces
/// the armor value "by 1 with each successful hit," not by the damage that got through that
/// particular swing. Deliberately breaking through a door, window, or lock -- this resolver's
/// entire purpose -- <em>is</em> that "specific attempt", so the more specific substance-armor
/// rule and its worked example govern over the general object rule's phrasing for every item this
/// ruleset ships. See <c>docs/decisions/0033-item-hit-points.md</c>'s "Rules interpretation:
/// armor degradation" block for the full quoted reconciliation.
/// </para>
/// <para>
/// A caller rolls the attacking weapon's damage with <c>DamageResolver.RollDamage(LandedGrade.Normal,
/// ArmorTreatment.Subtracted, weapon, damageBonus, currentArmorValue, damageRuleset, entropy)</c>
/// and passes the resulting <see cref="DamageRoll"/> here.
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
    /// armor. <see cref="DamageRoll.DamageDealt"/> reduces hit points (p.224's "reduced by the
    /// remaining damage"); a landed hit -- Normal, Special, or Critical, regardless of how much
    /// damage got through that swing -- reduces the armor value by exactly 1, per the substance-
    /// armor worked example (p.225: "reducing the armor value by 1 with each successful hit"). A
    /// Miss changes nothing.
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

        // Ch 8, p.224-225: substance armor "is not lost and does not deteriorate through multiple
        // attacks" except by "a specific attempt to reduce the armor value" -- breaking a door,
        // window, or lock is that attempt. The worked example reduces the armor value by 1 per
        // successful (landed) hit, independent of how much damage that swing actually dealt.
        var resultingArmorValue = Math.Max(0, currentArmorValue - 1);

        return new BreakableItemDamageResult(
            damage.DamageDealt, resultingHitPoints, resultingArmorValue, ClassifyCondition(resultingHitPoints));
    }

    private static BreakableItemCondition ClassifyCondition(int hitPoints) =>
        hitPoints <= 0 ? BreakableItemCondition.Destroyed : BreakableItemCondition.Intact;
}
