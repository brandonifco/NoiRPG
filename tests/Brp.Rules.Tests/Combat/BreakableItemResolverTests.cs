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
    public void Repeated_hits_steadily_reduce_armor_until_damage_overcomes_it_page_225()
    {
        // Mirrors the book's own worked example shape: repeated hits at a fixed raw damage (6)
        // against a substance armor (3) -- the first two hits are fully absorbed (armor still
        // ahead of the raw damage), but each hit still wears the armor down by 1 until, on the
        // fourth hit, the steadily reducing armor value is finally overcome.
        var armor = 3;
        var hitPoints = 3; // Glass window, SIZ 3 -- item-hit-points-ruleset.json

        for (var hitsSoFar = 1; hitsSoFar <= 3; hitsSoFar++)
        {
            var stillAbsorbed = MakeDamageRoll(LandedGrade.Normal, damageDealt: 0);
            var step = BreakableItemResolver.ApplyDamage(hitPoints, armor, stillAbsorbed);
            armor = step.ResultingArmorValue;
            hitPoints = step.ResultingHitPoints;
        }

        Assert.Equal(0, armor); // 3 - 1 - 1 - 1
        Assert.Equal(3, hitPoints); // never dealt, since every rolled DamageDealt above was 0

        var overcomes = MakeDamageRoll(LandedGrade.Normal, damageDealt: 6); // now unmitigated by 0 armor
        var final = BreakableItemResolver.ApplyDamage(hitPoints, armor, overcomes);

        Assert.Equal(-3, final.ResultingHitPoints);
        Assert.Equal(BreakableItemCondition.Destroyed, final.Condition);
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
