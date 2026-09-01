using Brp.Data;
using Brp.Rules.Combat;
using Brp.Rules.Gear;

namespace Brp.Rules.Tests.Combat;

/// <summary>
/// Ch 8: Equipment, "Damage to Inanimate Objects" (p.224): damage exceeding an object's armor
/// value reduces its hit points by the remainder; an object smaller than or about human-sized is
/// destroyed at 0 hit points. "Armor Value of Substances" (p.224-225): for every item this
/// ruleset ships, the armor value degrades by exactly 1 per landed hit (the worked bulletproof-
/// glass example), not by the penetrating damage that hit dealt -- see
/// <c>docs/decisions/0033-item-hit-points.md</c>'s "Rules interpretation: armor degradation"
/// block for the full reconciliation of these two passages.
/// </summary>
public class BreakableItemResolverTests
{
    [Fact]
    public void Damage_at_or_below_the_armor_value_deals_no_hit_point_loss_but_still_wears_the_armor_page_224()
    {
        var damage = MakeDamageRoll(LandedGrade.Normal, damageDealt: 0);

        var result = BreakableItemResolver.ApplyDamage(currentHitPoints: 6, currentArmorValue: 3, damage);

        Assert.Equal(0, result.DamageDealt);
        Assert.Equal(6, result.ResultingHitPoints);
        Assert.Equal(2, result.ResultingArmorValue); // still costs 1 armor -- "each successful hit" (p.225)
        Assert.Equal(BreakableItemCondition.Intact, result.Condition);
    }

    [Fact]
    public void Damage_exceeding_armor_reduces_hit_points_by_the_remaining_damage_page_224()
    {
        // "If the damage exceeds the object's armor value, then the hit points are reduced by
        // the remaining damage" (p.224). DamageDealt is already that remaining-damage figure.
        var damage = MakeDamageRoll(LandedGrade.Normal, damageDealt: 4);

        var result = BreakableItemResolver.ApplyDamage(currentHitPoints: 6, currentArmorValue: 3, damage);

        Assert.Equal(4, result.DamageDealt);
        Assert.Equal(2, result.ResultingHitPoints);
        Assert.Equal(BreakableItemCondition.Intact, result.Condition);
    }

    [Fact]
    public void Armor_degrades_by_exactly_one_per_landed_hit_regardless_of_penetrating_damage_page_225()
    {
        // Pinning test: the substance-armor worked example (p.225) reduces the armor value "by 1
        // with each successful hit," not by the damage that got through -- the general "Damage to
        // Inanimate Objects" phrasing (p.224, "that many damage points reduce its armor value")
        // does NOT govern here. A large penetrating hit still costs only 1 armor point.
        var bigHit = MakeDamageRoll(LandedGrade.Normal, damageDealt: 25);

        var result = BreakableItemResolver.ApplyDamage(currentHitPoints: 30, currentArmorValue: 9, bigHit);

        Assert.Equal(25, result.DamageDealt); // hit points: unaffected by the armor-degradation reading
        Assert.Equal(8, result.ResultingArmorValue); // armor: -1, not -25
    }

    [Fact]
    public void Armor_degrades_by_one_even_on_a_hit_that_deals_no_damage_page_225()
    {
        // The worked example degrades armor "with each successful hit" without qualifying it on
        // whether that swing's damage penetrated -- so a landed hit that rolled under the armor
        // value (DamageDealt 0) still wears the armor down by 1.
        var glancingHit = MakeDamageRoll(LandedGrade.Normal, damageDealt: 0);

        var result = BreakableItemResolver.ApplyDamage(currentHitPoints: 6, currentArmorValue: 3, glancingHit);

        Assert.Equal(0, result.DamageDealt);
        Assert.Equal(2, result.ResultingArmorValue);
    }

    [Fact]
    public void Armor_value_floors_at_zero_rather_than_going_negative_page_225()
    {
        var damage = MakeDamageRoll(LandedGrade.Normal, damageDealt: 1);

        var result = BreakableItemResolver.ApplyDamage(currentHitPoints: 6, currentArmorValue: 0, damage);

        Assert.Equal(0, result.ResultingArmorValue);
    }

    [Fact]
    public void Absorbed_hits_still_wear_the_armor_down_by_one_each_page_225()
    {
        // Mirrors the book's own worked example shape (p.225): a wood interior door (SIZ 6,
        // armor 3 -- item-hit-points-ruleset.json's doorWoodInterior) takes two swings of a
        // constant raw 2 damage. Raw damage never exceeds the armor value in effect for either
        // swing, so DamageDealt is honestly 0 both times (Max(0, raw - armor)) -- but each landed
        // hit still wears the armor down by 1, exactly as the sledgehammer-vs-bulletproof-glass
        // example describes.
        var hitPoints = 6;
        var armor = 3;

        var first = BreakableItemResolver.ApplyDamage(hitPoints, armor, MakeDamageRoll(LandedGrade.Normal, damageDealt: 0));
        Assert.Equal(0, first.DamageDealt); // raw 2 vs armor 3: fully absorbed
        Assert.Equal(6, first.ResultingHitPoints);
        Assert.Equal(2, first.ResultingArmorValue);
        hitPoints = first.ResultingHitPoints;
        armor = first.ResultingArmorValue;

        var second = BreakableItemResolver.ApplyDamage(hitPoints, armor, MakeDamageRoll(LandedGrade.Normal, damageDealt: 0));
        Assert.Equal(0, second.DamageDealt); // raw 2 vs armor 2: still fully absorbed
        Assert.Equal(6, second.ResultingHitPoints);
        Assert.Equal(1, second.ResultingArmorValue);
    }

    [Fact]
    public void A_hit_that_finally_overcomes_the_reduced_armor_deals_damage_and_can_destroy_the_object_page_225()
    {
        // Continuing the same door: two prior swings (Absorbed_hits_still_wear_the_armor_down_by_
        // one_each_page_225) have already worn its armor from 3 down to 1. A third, harder swing
        // (raw damage 8) now exceeds that reduced armor value -- DamageDealt = Max(0, 8 - 1) = 7,
        // matching the book's "when the damage roll overcomes the steadily reducing armor value,
        // the window bursts" (p.225). A near-dead door (2 HP left) is destroyed by it.
        var result = BreakableItemResolver.ApplyDamage(
            currentHitPoints: 2, currentArmorValue: 1, MakeDamageRoll(LandedGrade.Normal, damageDealt: 7));

        Assert.Equal(7, result.DamageDealt);
        Assert.Equal(-5, result.ResultingHitPoints);
        Assert.Equal(0, result.ResultingArmorValue);
        Assert.Equal(BreakableItemCondition.Destroyed, result.Condition);
    }

    [Fact]
    public void A_miss_changes_nothing()
    {
        var miss = MakeDamageRoll(LandedGrade.Miss, damageDealt: 0);

        var result = BreakableItemResolver.ApplyDamage(currentHitPoints: 6, currentArmorValue: 3, miss);

        Assert.Equal(0, result.DamageDealt);
        Assert.Equal(6, result.ResultingHitPoints);
        Assert.Equal(3, result.ResultingArmorValue);
        Assert.Equal(BreakableItemCondition.Intact, result.Condition);
    }

    [Fact]
    public void Rejects_a_negative_current_armor_value()
    {
        var damage = MakeDamageRoll(LandedGrade.Normal, damageDealt: 1);

        Assert.Throws<ArgumentOutOfRangeException>(
            () => BreakableItemResolver.ApplyDamage(currentHitPoints: 6, currentArmorValue: -1, damage));
    }

    [Fact]
    public void Reuses_DamageResolvers_weapon_damage_and_armor_subtraction_machinery_end_to_end()
    {
        // A heavy club (1D8, Ch 8, Modern Melee Weapons) bashes a glass window (SIZ 3, armor 1).
        var gear = NoirGearRuleset.Load();
        var weapon = gear.WeaponById(new WeaponId("clubHeavy"));
        var itemRegistry = NoirItemHitPointsRuleset.Load();
        var window = itemRegistry.ById(new BreakableItemId("windowGlass"));
        var damageRuleset = NoirDamageRuleset.Load();
        var entropy = new FixedEntropySource(6); // d8 roll of 6, no damage bonus supplied

        var damage = DamageResolver.RollDamage(
            LandedGrade.Normal, ArmorTreatment.Subtracted, weapon, damageBonus: null,
            armorValue: window.ArmorValue, damageRuleset, entropy);

        Assert.Equal(1, damage.ArmorApplied);
        Assert.Equal(5, damage.DamageDealt); // 6 raw - 1 armor

        var result = BreakableItemResolver.ApplyDamage(window.HitPoints, window.ArmorValue, damage);

        Assert.Equal(5, result.DamageDealt); // hit points: DamageResolver's armor-subtraction reused as-is
        Assert.Equal(-2, result.ResultingHitPoints);
        Assert.Equal(0, result.ResultingArmorValue); // armor: -1 (window's starting armor was already 1)
        Assert.Equal(BreakableItemCondition.Destroyed, result.Condition);
    }

    private static DamageRoll MakeDamageRoll(LandedGrade landedGrade, int damageDealt) => new(
        landedGrade, SpecialDamageTypeApplied: null, WeaponRolls: [], DamageBonusRolls: [],
        WeaponMaximum: null, ArmorApplied: 0, DamageDealt: damageDealt, SourceText: "test");
}
