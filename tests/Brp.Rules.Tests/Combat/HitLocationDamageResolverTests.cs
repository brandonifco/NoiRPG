using Brp.Core.Abilities;
using Brp.Data;
using Brp.Rules.Combat;

namespace Brp.Rules.Tests.Combat;

/// <summary>
/// Confirms <see cref="HitLocationDamageResolver"/> routes a blow's damage to both the struck
/// location and the character's total hit points, applying armor first and then the "a limb may
/// take only twice its hit points in damage" cap (Ch 6: Combat, "Damage and hit Locations (Option)",
/// pp.156-157), with the printed falling exception (Ch 7: Spot Rules, "Falling", p.172).
/// </summary>
public class HitLocationDamageResolverTests
{
    private static readonly HitLocationRuleset Ruleset = NoirHitLocationRuleset.Load();

    private static AbilitySet MakeTarget(int con, int siz)
    {
        var ruleset = NoirAbilityRuleset.Load();
        var values = ruleset.Characteristics.Keys.ToDictionary(id => id, _ => 12);
        values[new CharacteristicId("CON")] = con;
        values[new CharacteristicId("SIZ")] = siz;
        return new AbilitySet(ruleset, values);
    }

    private static HitLocationHitPoints MakeLocations(AbilitySet target) =>
        new(HitPointsByLocationCalculator.Compute(target.MaximumHitPoints, Ruleset));

    [Fact]
    public void Armor_is_subtracted_before_the_damage_is_applied()
    {
        var target = MakeTarget(con: 14, siz: 12); // max HP 13.
        var locations = MakeLocations(target);

        var result = HitLocationDamageResolver.ApplyDamage(
            target, locations, HitLocation.Chest, incomingDamage: 5, armorValue: 2, Ruleset);

        Assert.Equal(3, result.RawDamage);
        Assert.Equal(3, result.AppliedDamage);
        Assert.Equal(10, result.TotalRemainingHitPoints);
        Assert.Equal(10, target.CurrentHitPoints);
    }

    [Fact]
    public void Damage_is_routed_to_both_the_struck_location_and_the_total_pool()
    {
        var target = MakeTarget(con: 14, siz: 12); // max HP 13.
        var locations = MakeLocations(target);
        var armLocationHp = locations.MaximumAt(HitLocation.RightArm);

        var result = HitLocationDamageResolver.ApplyDamage(
            target, locations, HitLocation.RightArm, incomingDamage: 1, armorValue: 0, Ruleset);

        Assert.Equal(armLocationHp - 1, result.LocationRemainingHitPoints);
        Assert.Equal(armLocationHp - 1, locations.RemainingAt(HitLocation.RightArm));
        Assert.Equal(12, result.TotalRemainingHitPoints);
        Assert.Equal(0, locations.DamageTakenAt(HitLocation.LeftArm)); // untouched locations are unaffected.
    }

    [Fact]
    public void A_limb_hit_caps_the_damage_applied_to_the_total_pool_at_twice_its_hit_points()
    {
        // Ch 6, p.157's own worked example: a 2-point arm hit for 5 points takes only 4 off the total.
        var target = MakeTarget(con: 8, siz: 8); // max HP 8; arm location HP = ceil(8/4) = 2.
        var locations = MakeLocations(target);
        Assert.Equal(2, locations.MaximumAt(HitLocation.RightArm));

        var result = HitLocationDamageResolver.ApplyDamage(
            target, locations, HitLocation.RightArm, incomingDamage: 5, armorValue: 0, Ruleset);

        Assert.Equal(5, result.RawDamage);
        Assert.Equal(4, result.AppliedDamage); // capped at 2x2.
        Assert.True(result.CapApplied);
        Assert.Equal(4, target.CurrentHitPoints); // 8 - 4, not 8 - 5.
        Assert.Equal(HitLocationDamageBand.EqualOrExceedsDoubleLocationHitPoints, result.Band);
    }

    [Fact]
    public void A_limb_hit_for_triple_its_hit_points_is_still_capped_at_twice()
    {
        var target = MakeTarget(con: 8, siz: 8); // arm location HP = 2.
        var locations = MakeLocations(target);

        var result = HitLocationDamageResolver.ApplyDamage(
            target, locations, HitLocation.RightArm, incomingDamage: 8, armorValue: 0, Ruleset);

        Assert.Equal(8, result.RawDamage);
        Assert.Equal(4, result.AppliedDamage); // still capped at 2x2, even though raw exceeds 3x2=6.
        Assert.True(result.CapApplied);
        Assert.Equal(HitLocationDamageBand.EqualOrExceedsTripleLocationHitPoints, result.Band);
    }

    [Fact]
    public void The_limb_cap_does_not_apply_to_head_chest_or_abdomen()
    {
        var target = MakeTarget(con: 14, siz: 12); // chest location HP = ceil(13*4/10) = 6.
        var locations = MakeLocations(target);
        Assert.Equal(6, locations.MaximumAt(HitLocation.Chest));

        var result = HitLocationDamageResolver.ApplyDamage(
            target, locations, HitLocation.Chest, incomingDamage: 20, armorValue: 0, Ruleset);

        Assert.Equal(20, result.AppliedDamage);
        Assert.False(result.CapApplied);
        Assert.Equal(-7, target.CurrentHitPoints);
    }

    [Fact]
    public void Falling_damage_bypasses_the_limb_cap_per_the_printed_exception()
    {
        // Ch 7, p.172: "The entire damage done by the fall applies both to the rolled hit location
        // and to the falling character's total hit points. This is an exception to the rule that a
        // limb may take only twice its hit points in damage."
        var target = MakeTarget(con: 8, siz: 8); // leg location HP = ceil(8/3) = 3.
        var locations = MakeLocations(target);

        var result = HitLocationDamageResolver.ApplyDamage(
            target, locations, HitLocation.RightLeg, incomingDamage: 20, armorValue: 0, Ruleset,
            bypassLimbCap: true);

        Assert.Equal(20, result.AppliedDamage); // uncapped, unlike the non-falling case below.
        Assert.False(result.CapApplied);
        Assert.Equal(-12, target.CurrentHitPoints);
    }

    [Fact]
    public void The_same_leg_hit_is_capped_when_it_is_not_a_falling_blow()
    {
        var target = MakeTarget(con: 8, siz: 8); // leg location HP = 3.
        var locations = MakeLocations(target);

        var result = HitLocationDamageResolver.ApplyDamage(
            target, locations, HitLocation.RightLeg, incomingDamage: 20, armorValue: 0, Ruleset);

        Assert.Equal(6, result.AppliedDamage); // capped at 2x3.
        Assert.True(result.CapApplied);
        Assert.Equal(2, target.CurrentHitPoints);
    }

    [Fact]
    public void Negative_incoming_damage_or_armor_value_is_rejected()
    {
        var target = MakeTarget(con: 14, siz: 12);
        var locations = MakeLocations(target);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            HitLocationDamageResolver.ApplyDamage(target, locations, HitLocation.Head, -1, 0, Ruleset));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            HitLocationDamageResolver.ApplyDamage(target, locations, HitLocation.Head, 1, -1, Ruleset));
    }
}
