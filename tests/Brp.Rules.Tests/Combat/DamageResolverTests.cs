using Brp.Core.Abilities;
using Brp.Core.Dice;
using Brp.Core.Skills;
using Brp.Data;
using Brp.Rules.Characters;
using Brp.Rules.Combat;
using Brp.Rules.Gear;

namespace Brp.Rules.Tests.Combat;

/// <summary>
/// Layer 4 piece D (#52): <see cref="DamageResolver"/>. Ch 6: Combat, pp.146-156; Ch 7: Spot
/// Rules, "Knockout Attacks", p.174. See <c>docs/decisions/0017-damage.md</c> for the
/// correction this piece made against the initial "special = weaponMax + normalRoll + db"
/// transcription -- <see cref="Special_success_uses_the_same_dice_arithmetic_as_a_normal_hit_not_weaponMax_plus_a_fresh_roll"/>
/// pins the corrected behavior precisely so a reversion is a failing test, not a silent regression.
/// </summary>
public class DamageResolverTests
{
    private static readonly DamageRuleset Ruleset = NoirDamageRuleset.Load();

    private static WeaponDefinition MakeWeapon(string damage, bool applyDamageBonus) => new(
        Id: new WeaponId("test-weapon"),
        Name: "Test Weapon",
        SkillId: new SkillId("Melee Weapons"),
        WeaponClass: WeaponClass.Club,
        Damage: DiceExpression.Parse(damage),
        ApplyDamageBonus: applyDamageBonus,
        DamageByRange: [],
        Firearm: null,
        Source: "Test fixture, not from the book.");

    private static AbilitySet MakeTarget(int con, int siz)
    {
        var abilityRuleset = NoirAbilityRuleset.Load();
        var values = abilityRuleset.Characteristics.Keys.ToDictionary(id => id, _ => 10);
        values[new CharacteristicId("CON")] = con;
        values[new CharacteristicId("SIZ")] = siz;
        return new AbilitySet(abilityRuleset, values);
    }

    [Fact]
    public void Normal_hit_rolls_weapon_dice_plus_damage_bonus_minus_armor()
    {
        var weapon = MakeWeapon("1D6+2", applyDamageBonus: true);
        var damageBonus = DiceExpression.Parse("1D4");
        var entropy = new FixedEntropySource(3, 2); // weapon die 3 (+2 constant = 5), db die 2

        var roll = DamageResolver.RollDamage(
            LandedGrade.Normal, ArmorTreatment.Subtracted, weapon, damageBonus, armorValue: 2, entropy);

        Assert.Equal(5, roll.WeaponRoll!.RawTotal); // 3 + 2
        Assert.Equal(2, roll.DamageBonusRoll!.RawTotal);
        Assert.Equal(2, roll.ArmorApplied);
        Assert.Equal(5, roll.DamageDealt); // (5 + 2) - 2 = 5
    }

    [Fact]
    public void Special_success_uses_the_same_dice_arithmetic_as_a_normal_hit_not_weaponMax_plus_a_fresh_roll()
    {
        // Ch 6, p.147 footnote **: "For a greatsword, full damage is 2D8 on a normal success,
        // 2D8 bleeding damage on a special success" -- identical dice both times. Feeding the
        // Special path the exact same scripted entropy as the Normal test above must produce the
        // exact same damage; anyone who "fixes" this back to weaponMax + normalRoll + db makes
        // this test fail because it would consume an extra entropy draw for the weapon maximum
        // and double the weapon-dice contribution.
        var weapon = MakeWeapon("1D6+2", applyDamageBonus: true);
        var damageBonus = DiceExpression.Parse("1D4");
        var entropy = new FixedEntropySource(3, 2);

        var roll = DamageResolver.RollDamage(
            LandedGrade.Special, ArmorTreatment.Subtracted, weapon, damageBonus, armorValue: 2, entropy);

        Assert.Null(roll.WeaponMaximum);
        Assert.Equal(5, roll.WeaponRoll!.RawTotal);
        Assert.Equal(2, roll.ArmorApplied);
        Assert.Equal(5, roll.DamageDealt);
        Assert.Equal(2, entropy.DrawCount); // exactly one weapon die and one db die, no third draw
    }

    [Theory]
    [InlineData(ArmorTreatment.Bypassed)]
    [InlineData(ArmorTreatment.DoesNotApply)]
    public void Critical_success_uses_weapon_maximum_plus_damage_bonus_and_ignores_armor_under_either_collapsed_treatment(
        ArmorTreatment armorTreatment)
    {
        var weapon = MakeWeapon("1D6+2", applyDamageBonus: true); // max = 6 + 2 = 8
        var damageBonus = DiceExpression.Parse("1D4");
        var entropy = new FixedEntropySource(2); // only the db die is rolled

        var roll = DamageResolver.RollDamage(
            LandedGrade.Critical, armorTreatment, weapon, damageBonus, armorValue: 100, entropy);

        Assert.Null(roll.WeaponRoll);
        Assert.Equal(8, roll.WeaponMaximum);
        Assert.Equal(2, roll.DamageBonusRoll!.RawTotal);
        Assert.Equal(0, roll.ArmorApplied);
        Assert.Equal(10, roll.DamageDealt); // 8 + 2, armor (100) ignored entirely
        Assert.Equal(1, entropy.DrawCount);
    }

    [Fact]
    public void Damage_bonus_is_not_applied_when_the_weapon_does_not_use_it()
    {
        var firearm = MakeWeapon("1D8", applyDamageBonus: false);
        var damageBonus = DiceExpression.Parse("1D4");
        var entropy = new FixedEntropySource(5); // only the weapon die -- db must not be drawn

        var roll = DamageResolver.RollDamage(
            LandedGrade.Normal, ArmorTreatment.Subtracted, firearm, damageBonus, armorValue: 1, entropy);

        Assert.Null(roll.DamageBonusRoll);
        Assert.Equal(4, roll.DamageDealt); // 5 - 1 armor, no db
        Assert.Equal(1, entropy.DrawCount);
    }

    [Fact]
    public void Miss_deals_no_damage_and_consumes_no_entropy()
    {
        var weapon = MakeWeapon("1D6", applyDamageBonus: true);
        var entropy = new FixedEntropySource(); // throws if anything is drawn

        var roll = DamageResolver.RollDamage(
            LandedGrade.Miss, ArmorTreatment.NotApplicable, weapon, DiceExpression.Parse("1D4"), armorValue: 0, entropy);

        Assert.Equal(0, roll.DamageDealt);
        Assert.Equal(0, entropy.DrawCount);
    }

    [Fact]
    public void RollDamage_rejects_an_inconsistent_armor_treatment_for_a_landed_hit()
    {
        var weapon = MakeWeapon("1D6", applyDamageBonus: false);
        var entropy = new FixedEntropySource(3);

        Assert.Throws<ArgumentException>(() => DamageResolver.RollDamage(
            LandedGrade.Normal, ArmorTreatment.NotApplicable, weapon, null, armorValue: 0, entropy));
    }

    [Fact]
    public void ApplyDamage_reduces_hit_points_and_records_a_wound()
    {
        var target = MakeTarget(con: 12, siz: 12);
        var wounds = new WoundTrack();
        var weapon = MakeWeapon("1D6", applyDamageBonus: false);
        var entropy = new FixedEntropySource(4);
        var roll = DamageResolver.RollDamage(LandedGrade.Normal, ArmorTreatment.Subtracted, weapon, null, armorValue: 0, entropy);
        var before = target.CurrentHitPoints;

        var result = DamageResolver.ApplyDamage(target, wounds, roll, Ruleset, "Struck by a test weapon.");

        Assert.Equal(4, result.DamageDealt);
        Assert.Equal(before - 4, target.CurrentHitPoints);
        Assert.Equal(target.CurrentHitPoints, result.ResultingHitPoints);
        Assert.Single(wounds.Wounds);
        Assert.Equal("Struck by a test weapon.", wounds.Wounds[0].Description);
        Assert.Same(wounds.Wounds[0], result.Wound);
    }

    [Fact]
    public void ApplyDamage_on_a_miss_changes_nothing_and_records_no_wound()
    {
        var target = MakeTarget(con: 12, siz: 12);
        var wounds = new WoundTrack();
        var weapon = MakeWeapon("1D6", applyDamageBonus: false);
        var entropy = new FixedEntropySource();
        var roll = DamageResolver.RollDamage(LandedGrade.Miss, ArmorTreatment.NotApplicable, weapon, null, armorValue: 0, entropy);
        var before = target.CurrentHitPoints;

        var result = DamageResolver.ApplyDamage(target, wounds, roll, Ruleset, "Missed.");

        Assert.Equal(0, result.DamageDealt);
        Assert.Equal(before, target.CurrentHitPoints);
        Assert.Empty(wounds.Wounds);
        Assert.Null(result.Wound);
        Assert.Equal(HitPointCondition.Unaffected, result.Condition);
    }

    [Fact]
    public void Target_is_unconscious_at_or_below_the_ruleset_threshold()
    {
        // Ch 2, p.13: "Your character loses consciousness when their hit points are reduced to
        // 2 or less." Read from the loaded ruleset, not hardcoded, per AGENTS.md invariant 7.
        var target = MakeTarget(con: 21, siz: 21); // plenty of headroom above the threshold
        target.SetCurrentHitPoints(Ruleset.UnconsciousHitPointLevel + 1);
        var wounds = new WoundTrack();
        var weapon = MakeWeapon("1", applyDamageBonus: false); // flat 1 damage, no entropy needed
        var entropy = new FixedEntropySource();
        var roll = DamageResolver.RollDamage(LandedGrade.Normal, ArmorTreatment.Subtracted, weapon, null, armorValue: 0, entropy);

        var result = DamageResolver.ApplyDamage(target, wounds, roll, Ruleset, "Grazed.");

        Assert.Equal(Ruleset.UnconsciousHitPointLevel, result.ResultingHitPoints);
        Assert.Equal(HitPointCondition.Unconscious, result.Condition);
    }

    [Fact]
    public void Target_is_fatally_wounded_at_or_below_the_dead_threshold_and_negative_hit_points_are_tracked()
    {
        var target = MakeTarget(con: 21, siz: 21);
        target.SetCurrentHitPoints(Ruleset.DeadHitPointLevel + 3);
        var wounds = new WoundTrack();
        var weapon = MakeWeapon("10", applyDamageBonus: false); // a large flat hit
        var entropy = new FixedEntropySource();
        var roll = DamageResolver.RollDamage(LandedGrade.Normal, ArmorTreatment.Subtracted, weapon, null, armorValue: 0, entropy);

        var result = DamageResolver.ApplyDamage(target, wounds, roll, Ruleset, "Shot.");

        Assert.Equal(HitPointCondition.FatallyWounded, result.Condition);
        Assert.True(result.ResultingHitPoints < Ruleset.DeadHitPointLevel); // negative HP tracked, not floored
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    public void ResolvesToDeath_is_true_only_at_or_below_the_dead_threshold(int hitPointsAtEndOfFollowingRound)
    {
        Assert.Equal(
            hitPointsAtEndOfFollowingRound <= Ruleset.DeadHitPointLevel,
            DamageResolver.ResolvesToDeath(hitPointsAtEndOfFollowingRound, Ruleset));
    }

    [Fact]
    public void ResolvesToDeath_is_false_once_hit_points_are_restored_above_the_threshold()
    {
        // Models the seam to piece E: a First Aid intervention in the intervening round can
        // still change the outcome by the time the following round ends.
        Assert.False(DamageResolver.ResolvesToDeath(Ruleset.DeadHitPointLevel + 1, Ruleset));
    }

    [Fact]
    public void Knockout_attack_with_minor_wound_damage_deals_the_weapons_minimum_damage_and_does_not_knock_out()
    {
        // CON 3 (minimum) + SIZ 8 (minimum) -> 5.5 hit points, rounded up to 6; major wound
        // level is half of that, rounded up, i.e. 3.
        var target = MakeTarget(con: 3, siz: 8);
        Assert.Equal(6, target.MaximumHitPoints);
        Assert.Equal(3, target.MajorWoundLevel);

        var weapon = MakeWeapon("1D6", applyDamageBonus: false); // minimum possible damage: 1
        var outcome = new AttackDefenseOutcome(
            LandedGrade.Normal, ArmorTreatment.Subtracted, ParryWeaponDamage: null,
            DefenderRollsOnFumbleTable: false, AttackerRollsOnFumbleTable: false, SourceText: "test");
        var entropy = new FixedEntropySource(2); // rolled damage 2 < major wound level 3

        var knockout = DamageResolver.ResolveKnockoutAttack(outcome, weapon, null, armorValue: 0, target, Ruleset, entropy);

        Assert.False(knockout.KnockedOut);
        Assert.Equal(1, knockout.DamageDealt); // the weapon's minimum, not the rolled damage
        Assert.Null(knockout.DurationRounds);
        Assert.Equal(1, entropy.DrawCount); // no duration roll consumed
    }

    [Fact]
    public void Knockout_attack_with_major_wound_damage_deals_one_point_and_knocks_out_for_1D10_plus_10_rounds()
    {
        var target = MakeTarget(con: 3, siz: 8); // major wound level 3, as above
        var weapon = MakeWeapon("1D6", applyDamageBonus: false);
        var outcome = new AttackDefenseOutcome(
            LandedGrade.Normal, ArmorTreatment.Subtracted, ParryWeaponDamage: null,
            DefenderRollsOnFumbleTable: false, AttackerRollsOnFumbleTable: false, SourceText: "test");
        var entropy = new FixedEntropySource(5, 7); // rolled damage 5 >= 3, then duration D10 face 7

        var knockout = DamageResolver.ResolveKnockoutAttack(outcome, weapon, null, armorValue: 0, target, Ruleset, entropy);

        Assert.True(knockout.KnockedOut);
        Assert.Equal(1, knockout.DamageDealt);
        Assert.Equal(17, knockout.DurationRounds); // 7 + 10
    }

    [Fact]
    public void Knockout_attack_that_misses_deals_no_damage_and_does_not_knock_out()
    {
        var target = MakeTarget(con: 12, siz: 12);
        var weapon = MakeWeapon("1D6", applyDamageBonus: false);
        var outcome = new AttackDefenseOutcome(
            LandedGrade.Miss, ArmorTreatment.NotApplicable, ParryWeaponDamage: null,
            DefenderRollsOnFumbleTable: false, AttackerRollsOnFumbleTable: false, SourceText: "test");
        var entropy = new FixedEntropySource();

        var knockout = DamageResolver.ResolveKnockoutAttack(outcome, weapon, null, armorValue: 0, target, Ruleset, entropy);

        Assert.False(knockout.KnockedOut);
        Assert.Equal(0, knockout.DamageDealt);
    }

    [Fact]
    public void Critical_knockout_attack_ignores_armor_for_the_underlying_roll_per_the_special_or_critical_effects_still_applying()
    {
        // Ch 7, p.174: "The effects of special or critical successes (such as extra damage or
        // bypassing armor) apply in all cases" to a knockout attempt.
        var target = MakeTarget(con: 3, siz: 8); // major wound level 3
        var weapon = MakeWeapon("1D6", applyDamageBonus: false); // critical max = 6
        var outcome = new AttackDefenseOutcome(
            LandedGrade.Critical, ArmorTreatment.Bypassed, ParryWeaponDamage: null,
            DefenderRollsOnFumbleTable: false, AttackerRollsOnFumbleTable: false, SourceText: "test");
        var entropy = new FixedEntropySource(9); // duration roll only -- critical needs no weapon-dice draw

        var knockout = DamageResolver.ResolveKnockoutAttack(outcome, weapon, null, armorValue: 1000, target, Ruleset, entropy);

        Assert.Equal(6, knockout.UnderlyingRoll.DamageDealt); // armor (1000) ignored, matches a plain critical
        Assert.True(knockout.KnockedOut); // 6 >= major wound level 3
        Assert.Equal(1, knockout.DamageDealt);
        Assert.Equal(19, knockout.DurationRounds); // 9 + 10
    }

    [Fact]
    public void ApplyKnockoutAttack_applies_the_resolved_damage_and_records_a_wound()
    {
        var target = MakeTarget(con: 3, siz: 8);
        var wounds = new WoundTrack();
        var weapon = MakeWeapon("1D6", applyDamageBonus: false);
        var outcome = new AttackDefenseOutcome(
            LandedGrade.Normal, ArmorTreatment.Subtracted, ParryWeaponDamage: null,
            DefenderRollsOnFumbleTable: false, AttackerRollsOnFumbleTable: false, SourceText: "test");
        var entropy = new FixedEntropySource(5, 7);
        var knockout = DamageResolver.ResolveKnockoutAttack(outcome, weapon, null, armorValue: 0, target, Ruleset, entropy);
        var before = target.CurrentHitPoints;

        var result = DamageResolver.ApplyKnockoutAttack(target, wounds, knockout, Ruleset, "Knockout blow.");

        Assert.Equal(1, result.DamageDealt);
        Assert.Equal(before - 1, target.CurrentHitPoints);
        Assert.Single(wounds.Wounds);
    }
}
