using Brp.Data;
using Brp.Rules.Combat;
using Brp.Rules.Gear;

namespace Brp.Rules.Tests.Combat;

/// <summary>
/// Ch 8: Equipment, "Damage to Inanimate Objects" (p.224): damage exceeding an object's armor
/// value reduces its hit points by the remainder and its armor value by the same amount; an
/// object smaller than or about human-sized is destroyed at 0 hit points. See
/// <c>docs/decisions/NNNN-item-hit-points.md</c>.
/// </summary>
public class BreakableItemResolverTests
{
    [Fact]
    public void Damage_at_or_below_the_armor_value_deals_no_hit_point_or_armor_loss_page_224()
    {
        var damage = MakeDamageRoll(damageDealt: 0);

        var result = BreakableItemResolver.ApplyDamage(currentHitPoints: 6, currentArmorValue: 3, damage);

        Assert.Equal(0, result.DamageDealt);
        Assert.Equal(6, result.ResultingHitPoints);
        Assert.Equal(3, result.ResultingArmorValue);
        Assert.Equal(BreakableItemCondition.Intact, result.Condition);
    }

    [Fact]
    public void Damage_exceeding_armor_reduces_hit_points_and_degrades_armor_by_the_same_amount_page_224()
    {
        // "If the damage exceeds the object's armor value, then the hit points are reduced by
        // the remaining damage and that many damage points reduce its armor value."
        var damage = MakeDamageRoll(damageDealt: 4);

        var result = BreakableItemResolver.ApplyDamage(currentHitPoints: 6, currentArmorValue: 3, damage);

        Assert.Equal(4, result.DamageDealt);
        Assert.Equal(2, result.ResultingHitPoints);
        Assert.Equal(0, result.ResultingArmorValue);
        Assert.Equal(BreakableItemCondition.Intact, result.Condition);
    }

    [Fact]
    public void Armor_value_floors_at_zero_rather_than_going_negative_page_224()
    {
        var damage = MakeDamageRoll(damageDealt: 4);

        // Armor is already at 1, well below the damage that got through.
        var result = BreakableItemResolver.ApplyDamage(currentHitPoints: 6, currentArmorValue: 1, damage);

        Assert.Equal(0, result.ResultingArmorValue);
    }

    [Fact]
    public void An_object_smaller_than_human_sized_is_destroyed_at_zero_hit_points_page_224()
    {
        var first = BreakableItemResolver.ApplyDamage(6, 3, MakeDamageRoll(damageDealt: 4));
        Assert.Equal(BreakableItemCondition.Intact, first.Condition);

        // Its armor is now 0 (degraded by the first hit), so the second hit's full raw damage
        // gets through unmitigated.
        var second = BreakableItemResolver.ApplyDamage(
            first.ResultingHitPoints, first.ResultingArmorValue, MakeDamageRoll(damageDealt: 3));

        Assert.True(second.ResultingHitPoints <= 0);
        Assert.Equal(BreakableItemCondition.Destroyed, second.Condition);
    }

    [Fact]
    public void A_miss_changes_nothing()
    {
        var miss = new DamageRoll(
            LandedGrade.Miss, SpecialDamageTypeApplied: null, WeaponRolls: [], DamageBonusRolls: [],
            WeaponMaximum: null, ArmorApplied: 0, DamageDealt: 0, SourceText: "test miss");

        var result = BreakableItemResolver.ApplyDamage(currentHitPoints: 6, currentArmorValue: 3, miss);

        Assert.Equal(0, result.DamageDealt);
        Assert.Equal(6, result.ResultingHitPoints);
        Assert.Equal(3, result.ResultingArmorValue);
        Assert.Equal(BreakableItemCondition.Intact, result.Condition);
    }

    [Fact]
    public void Rejects_a_negative_current_armor_value()
    {
        var damage = MakeDamageRoll(damageDealt: 1);

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

        Assert.Equal(5, result.DamageDealt);
        Assert.Equal(-2, result.ResultingHitPoints);
        Assert.Equal(0, result.ResultingArmorValue);
        Assert.Equal(BreakableItemCondition.Destroyed, result.Condition);
    }

    private static DamageRoll MakeDamageRoll(int damageDealt) => new(
        LandedGrade.Normal, SpecialDamageTypeApplied: null, WeaponRolls: [], DamageBonusRolls: [],
        WeaponMaximum: null, ArmorApplied: 0, DamageDealt: damageDealt, SourceText: "test");
}
