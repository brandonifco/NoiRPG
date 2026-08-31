using Brp.Core.Abilities;
using Brp.Core.Contests;
using Brp.Core.Primitives;
using Brp.Core.Resolution;
using Brp.Data;
using Brp.Rules.Characters;
using Brp.Rules.Combat;

namespace Brp.Rules.Tests.Combat;

/// <summary>
/// Ch 6: Combat healing and recovery (#109): First Aid's per-wound healing (Ch 3, p.39), natural
/// healing (p.157), the Medicine skill's doubled rate and characteristic restoration (Ch 3, p.46;
/// p.157), and the Conditions of Medical Care Table (p.157). Rolls are pinned through
/// <see cref="FixedEntropySource"/> so each grade and amount is exact. See
/// <c>docs/decisions/0023-healing-and-recovery.md</c>.
/// </summary>
public class HealingResolverTests
{
    private static readonly HealingRuleset Healing = NoirHealingRuleset.Load();
    private static readonly DamageRuleset Damage = NoirDamageRuleset.Load();
    private static readonly MajorWoundRuleset MajorWounds = NoirMajorWoundRuleset.Load();

    private static AbilitySet MakeTarget(int con = 12, int siz = 12)
    {
        var ruleset = NoirAbilityRuleset.Load();
        var values = ruleset.Characteristics.Keys.ToDictionary(id => id, _ => 12);
        values[new CharacteristicId("CON")] = con;
        values[new CharacteristicId("SIZ")] = siz;
        return new AbilitySet(ruleset, values);
    }

    private static Wound Wound(AbilitySet target, WoundTrack wounds, int damage) =>
        DamageResolver.ApplyDamage(target, wounds, damage, Damage, "test wound").Wound!;

    // --- First Aid -------------------------------------------------------------------------------

    [Fact]
    public void First_aid_success_heals_1d3_to_a_single_wound_page_39()
    {
        var target = MakeTarget();      // 12 max HP
        var wounds = new WoundTrack();
        var wound = Wound(target, wounds, 5);   // HP 7, wound damage 5
        Assert.Equal(7, target.CurrentHitPoints);

        // First Aid 47%: roll 30 is an ordinary Success; then 1D3 heals 2.
        var outcome = HealingResolver.ResolveFirstAid(
            Percent.Of(47), default, wound, target, wounds, Healing, new FixedEntropySource(30, 2));

        Assert.Equal(SuccessLevel.Success, outcome.Roll.Level);
        Assert.Equal(2, outcome.HealedHitPoints);
        Assert.Equal(9, outcome.ResultingHitPoints);
        Assert.False(outcome.WoundFullyHealed);
        Assert.False(outcome.BlocksFurtherAttempts);

        // The healed points are removed from the wound (5 -> 3), not a new wound.
        var remaining = Assert.Single(wounds.Wounds);
        Assert.Equal(3, remaining.DamageAmount);
    }

    [Fact]
    public void First_aid_healing_is_capped_at_the_wound_damage_and_removes_a_fully_healed_wound_page_39()
    {
        var target = MakeTarget();
        var wounds = new WoundTrack();
        var wound = Wound(target, wounds, 1);   // HP 11, wound damage 1

        // 1D3 rolls 3, but the wound only inflicted 1, so only 1 is healed ("up to the amount ... inflicted").
        var outcome = HealingResolver.ResolveFirstAid(
            Percent.Of(47), default, wound, target, wounds, Healing, new FixedEntropySource(30, 3));

        Assert.Equal(1, outcome.HealedHitPoints);
        Assert.Equal(12, outcome.ResultingHitPoints);
        Assert.True(outcome.WoundFullyHealed);
        Assert.Empty(wounds.Wounds);
    }

    [Fact]
    public void First_aid_special_heals_2d3_page_39()
    {
        var target = MakeTarget();
        var wounds = new WoundTrack();
        var wound = Wound(target, wounds, 6);   // HP 6, wound damage 6

        // First Aid 47%: roll 5 is a Special (critical 3, special 10); then 2D3 heals 2+3 = 5.
        var outcome = HealingResolver.ResolveFirstAid(
            Percent.Of(47), default, wound, target, wounds, Healing, new FixedEntropySource(5, 2, 3));

        Assert.Equal(SuccessLevel.Special, outcome.Roll.Level);
        Assert.Equal(5, outcome.HealedHitPoints);
        Assert.Equal(11, outcome.ResultingHitPoints);
    }

    [Fact]
    public void First_aid_critical_heals_3_plus_1d3_page_39()
    {
        var target = MakeTarget();
        var wounds = new WoundTrack();
        var wound = Wound(target, wounds, 8);   // HP 4, wound damage 8

        // First Aid 47%: roll 2 is a Critical (<= 3); then 3+1D3 heals 3 + 3 = 6.
        var outcome = HealingResolver.ResolveFirstAid(
            Percent.Of(47), default, wound, target, wounds, Healing, new FixedEntropySource(2, 3));

        Assert.Equal(SuccessLevel.Critical, outcome.Roll.Level);
        Assert.Equal(6, outcome.HealedHitPoints);
        Assert.Equal(10, outcome.ResultingHitPoints);
    }

    [Fact]
    public void First_aid_fumble_deals_1_hit_point_and_heals_nothing_page_39()
    {
        var target = MakeTarget();
        var wounds = new WoundTrack();
        var wound = Wound(target, wounds, 5);   // HP 7, wound damage 5

        // A roll of 00 (100) always fumbles. No healing dice are drawn.
        var outcome = HealingResolver.ResolveFirstAid(
            Percent.Of(47), default, wound, target, wounds, Healing, new FixedEntropySource(100));

        Assert.Equal(SuccessLevel.Fumble, outcome.Roll.Level);
        Assert.Equal(0, outcome.HealedHitPoints);
        Assert.Equal(1, outcome.SelfDamage);
        Assert.Equal(6, outcome.ResultingHitPoints);
        Assert.True(outcome.BlocksFurtherAttempts);

        // The wound is untouched by a fumble.
        Assert.Equal(5, Assert.Single(wounds.Wounds).DamageAmount);
    }

    [Fact]
    public void First_aid_failure_heals_nothing_and_blocks_further_attempts_page_39()
    {
        var target = MakeTarget();
        var wounds = new WoundTrack();
        var wound = Wound(target, wounds, 5);   // HP 7

        // First Aid 47%: roll 60 fails.
        var outcome = HealingResolver.ResolveFirstAid(
            Percent.Of(47), default, wound, target, wounds, Healing, new FixedEntropySource(60));

        Assert.Equal(SuccessLevel.Failure, outcome.Roll.Level);
        Assert.Equal(0, outcome.HealedHitPoints);
        Assert.Equal(0, outcome.SelfDamage);
        Assert.Equal(7, outcome.ResultingHitPoints);
        Assert.True(outcome.BlocksFurtherAttempts);
        Assert.Equal(5, Assert.Single(wounds.Wounds).DamageAmount);
    }

    [Fact]
    public void First_aid_support_bonuses_add_half_medicine_fifth_pharmacy_and_capped_equipment_page_39()
    {
        var target = MakeTarget();
        var wounds = new WoundTrack();
        var wound = Wound(target, wounds, 5);

        // Rating 30 + 1/2 of Medicine 40 (=20) + 1/5 of Science(Pharmacy) 40 (=8) + equipment 25 capped
        // at 20 = 78. A roll of 70 fails at 30 but succeeds at 78.
        var support = new FirstAidSupport(MedicineRating: 40, SciencePharmacyRating: 40, EquipmentBonusPercent: 25);
        var outcome = HealingResolver.ResolveFirstAid(
            Percent.Of(30), support, wound, target, wounds, Healing, new FixedEntropySource(70, 1));

        Assert.Equal(78, outcome.Roll.EffectiveChance.Value);
        Assert.True(outcome.Roll.Succeeded);
    }

    [Fact]
    public void First_aid_in_hazardous_conditions_is_difficult_page_39()
    {
        var target = MakeTarget();
        var wounds = new WoundTrack();
        var wound = Wound(target, wounds, 5);

        // Rating 60 halved by the Difficult grade = 30; a roll of 40 succeeds at 60 but fails at 30.
        var support = new FirstAidSupport(HazardousConditions: true);
        var outcome = HealingResolver.ResolveFirstAid(
            Percent.Of(60), support, wound, target, wounds, Healing, new FixedEntropySource(40));

        Assert.Equal(30, outcome.Roll.EffectiveChance.Value);
        Assert.False(outcome.Roll.Succeeded);
    }

    [Fact]
    public void First_aid_restores_a_fatally_wounded_character_via_the_reused_111_rescue_window_page_39()
    {
        // A character at 0 hit points (fatal) is First-Aided to 1+, then the #111 rescue decides survival.
        var target = MakeTarget();
        var wounds = new WoundTrack();
        var wound = Wound(target, wounds, 12);  // HP 0 -- fatally wounded
        Assert.Equal(0, target.CurrentHitPoints);

        // First Aid 47%: roll 30 Success; 1D3 heals 3 -> HP 3 (>= 1).
        var outcome = HealingResolver.ResolveFirstAid(
            Percent.Of(47), default, wound, target, wounds, Healing, new FixedEntropySource(30, 3));
        Assert.Equal(3, outcome.ResultingHitPoints);

        // In the wound round (0) or the round after (1): the reused MajorWoundResolver.SurvivesFatalWound
        // path returns survival because hit points are now above the dead level.
        Assert.True(HealingResolver.ResolvesFatalWoundRescue(target, roundsSinceFatalWound: 0, MajorWounds, Damage));
        Assert.True(HealingResolver.ResolvesFatalWoundRescue(target, roundsSinceFatalWound: 1, MajorWounds, Damage));

        // Outside the window, the same restored hit points do not rescue.
        Assert.False(HealingResolver.ResolvesFatalWoundRescue(target, roundsSinceFatalWound: 2, MajorWounds, Damage));
    }

    [Fact]
    public void The_rescue_fails_when_first_aid_did_not_bring_hit_points_above_the_dead_level_page_156()
    {
        var target = MakeTarget();
        target.SetCurrentHitPoints(0);   // still fatally wounded, no aid applied

        Assert.False(HealingResolver.ResolvesFatalWoundRescue(target, roundsSinceFatalWound: 0, MajorWounds, Damage));
    }

    // --- Natural healing -------------------------------------------------------------------------

    [Fact]
    public void Natural_healing_reports_a_flat_1d3_weekly_rate_page_157()
    {
        // The resolver REPORTS the rolled rate; accrual over weeks is a caller seam.
        var rate = HealingResolver.RollNaturalHealingRate(Healing, new FixedEntropySource(2));

        Assert.Equal(2, rate.HitPointsPerWeek);
        Assert.False(rate.Doubled);
    }

    [Fact]
    public void Applied_healing_spreads_across_wounds_as_evenly_as_possible_page_157()
    {
        var target = MakeTarget();
        var wounds = new WoundTrack();
        Wound(target, wounds, 4);   // wound A
        Wound(target, wounds, 3);   // wound B -- HP now 12 - 7 = 5
        Assert.Equal(5, target.CurrentHitPoints);

        // Apply 4 hit points: distributed round-robin A,B,A,B -> A heals 2, B heals 2.
        var application = HealingResolver.ApplyWeeklyHealing(target, wounds, 4);

        Assert.Equal(4, application.TotalHealed);
        Assert.Equal(9, application.ResultingHitPoints);
        Assert.All(application.PerWound, w => Assert.Equal(2, w.HealedHitPoints));
        // Remaining outstanding wound damage: 2 + 1 = 3 across two still-open wounds.
        Assert.Equal(2, wounds.Wounds.Count);
        Assert.Equal(3, wounds.Wounds.Sum(w => w.DamageAmount));
    }

    [Fact]
    public void Applied_healing_is_capped_at_outstanding_wound_damage_page_157()
    {
        var target = MakeTarget();
        var wounds = new WoundTrack();
        Wound(target, wounds, 3);   // HP 9, only 3 outstanding

        // Ask for 10 hit points; only 3 can land (the wound), and the wound is fully healed.
        var application = HealingResolver.ApplyWeeklyHealing(target, wounds, 10);

        Assert.Equal(3, application.TotalHealed);
        Assert.Equal(12, application.ResultingHitPoints);
        Assert.Empty(wounds.Wounds);
    }

    // --- Medicine --------------------------------------------------------------------------------

    [Fact]
    public void Medicine_doubles_the_weekly_rate_to_2d3_page_157()
    {
        var rate = HealingResolver.RollMedicineHealingRate(Healing, new FixedEntropySource(3, 3));

        Assert.Equal(6, rate.HitPointsPerWeek);
        Assert.True(rate.Doubled);
    }

    [Theory]
    [InlineData(SuccessLevel.Success, new[] { 3 }, 2)]   // 1D3-1: 3 - 1 = 2
    [InlineData(SuccessLevel.Special, new[] { 2 }, 2)]   // 1D3: 2
    [InlineData(SuccessLevel.Critical, new[] { 2 }, 3)]  // 1D3+1: 2 + 1 = 3
    public void Medicine_characteristic_restoration_rate_matches_the_grade_page_46(
        SuccessLevel grade, int[] dice, int expected)
    {
        var rate = HealingResolver.RollCharacteristicRestorationRate(grade, Healing, new FixedEntropySource(dice));

        Assert.Equal(grade, rate.Grade);
        Assert.Equal(expected, rate.PointsPerWeek);
    }

    [Fact]
    public void Medicine_characteristic_restoration_rate_requires_a_successful_roll()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            HealingResolver.RollCharacteristicRestorationRate(SuccessLevel.Failure, Healing, new FixedEntropySource(1)));
    }

    [Fact]
    public void Applying_characteristic_restoration_recomputes_derived_values_via_ability_set_pages_13_46()
    {
        var target = MakeTarget();   // CON 12, SIZ 12 -> 12 max HP
        target.Set(new CharacteristicId("CON"), 8);   // drained: max HP now (8+12)/2 = 10, current HP clamped to 10
        Assert.Equal(10, target.MaximumHitPoints);
        Assert.Equal(10, target.CurrentHitPoints);

        // Restore 3 CON through AbilitySet.Set: CON 11, and max HP recomputes live to (11+12)/2 = 12 (rounded up).
        var restored = HealingResolver.ApplyCharacteristicRestoration(target, new CharacteristicId("CON"), 3);

        Assert.Equal(11, restored);
        Assert.Equal(11, target.ValueOf(new CharacteristicId("CON")));
        Assert.Equal(12, target.MaximumHitPoints);
        // Ch 2, p.13: restoring the characteristic does NOT resurrect the already-clamped current hit points.
        Assert.Equal(10, target.CurrentHitPoints);
    }

    [Fact]
    public void Characteristic_restoration_does_not_exceed_the_ruleset_maximum()
    {
        var target = MakeTarget();
        // CON's ruleset maximum is 21; restoring far beyond it caps at 21.
        var restored = HealingResolver.ApplyCharacteristicRestoration(target, new CharacteristicId("CON"), 50);

        Assert.Equal(21, restored);
    }

    // --- Conditions of Medical Care --------------------------------------------------------------

    [Fact]
    public void Decent_conditions_heal_the_natural_rate_with_no_roll_page_157()
    {
        var target = MakeTarget();
        var wounds = new WoundTrack();
        Wound(target, wounds, 5);   // HP 7

        // No gating roll: only the 1D3 natural-healing die is drawn (3).
        var outcome = HealingResolver.ResolveConditionsOfCare(
            MedicalCareTier.Decent, Percent.Of(50), Healing.FirstAid.PrintedBaseChance, target, wounds, Healing,
            new FixedEntropySource(3));

        Assert.Null(outcome.CaregiverRoll);
        Assert.Equal(3, outcome.HealingRate);
        Assert.Equal(10, outcome.Application!.ResultingHitPoints);
        Assert.False(outcome.AllowsAdditionalHealing);
    }

    [Fact]
    public void Excellent_conditions_heal_naturally_and_allow_further_healing_page_157()
    {
        var target = MakeTarget();
        var wounds = new WoundTrack();
        Wound(target, wounds, 5);

        var outcome = HealingResolver.ResolveConditionsOfCare(
            MedicalCareTier.Excellent, Percent.Of(50), Healing.FirstAid.PrintedBaseChance, target, wounds, Healing,
            new FixedEntropySource(2));

        Assert.Null(outcome.CaregiverRoll);
        Assert.Equal(2, outcome.HealingRate);
        Assert.True(outcome.AllowsAdditionalHealing);
    }

    [Fact]
    public void Poor_conditions_require_a_difficult_roll_which_heals_normally_on_success_page_157()
    {
        var target = MakeTarget();
        var wounds = new WoundTrack();
        Wound(target, wounds, 5);   // HP 7

        // Caregiver First Aid 80 halved by Difficult = 40; roll 20 succeeds, then 1D3 heals 2.
        var outcome = HealingResolver.ResolveConditionsOfCare(
            MedicalCareTier.Poor, Percent.Of(80), Healing.FirstAid.PrintedBaseChance, target, wounds, Healing,
            new FixedEntropySource(20, 2));

        Assert.NotNull(outcome.CaregiverRoll);
        Assert.Equal(40, outcome.CaregiverRoll!.EffectiveChance.Value);
        Assert.Equal(SuccessLevel.Success, outcome.CaregiverRoll.Level);
        Assert.Equal(2, outcome.HealingRate);
        Assert.Equal(9, outcome.Application!.ResultingHitPoints);
        Assert.Equal(0, outcome.AdditionalDamage);
    }

    [Fact]
    public void Poor_conditions_yield_no_healing_on_a_failed_roll_page_157()
    {
        var target = MakeTarget();
        var wounds = new WoundTrack();
        Wound(target, wounds, 5);   // HP 7

        // 80 halved = 40; roll 50 fails. No natural-healing die is drawn.
        var outcome = HealingResolver.ResolveConditionsOfCare(
            MedicalCareTier.Poor, Percent.Of(80), Healing.FirstAid.PrintedBaseChance, target, wounds, Healing,
            new FixedEntropySource(50));

        Assert.Equal(SuccessLevel.Failure, outcome.CaregiverRoll!.Level);
        Assert.Equal(0, outcome.HealingRate);
        Assert.Null(outcome.Application);
        Assert.Equal(7, target.CurrentHitPoints);
    }

    [Fact]
    public void Poor_conditions_fumble_inflicts_1d3_additional_damage_page_157()
    {
        var target = MakeTarget();
        var wounds = new WoundTrack();
        Wound(target, wounds, 5);   // HP 7

        // Roll 00 (100) fumbles; then 1D3 additional damage rolls 2.
        var outcome = HealingResolver.ResolveConditionsOfCare(
            MedicalCareTier.Poor, Percent.Of(80), Healing.FirstAid.PrintedBaseChance, target, wounds, Healing,
            new FixedEntropySource(100, 2));

        Assert.Equal(SuccessLevel.Fumble, outcome.CaregiverRoll!.Level);
        Assert.Equal(0, outcome.HealingRate);
        Assert.Equal(2, outcome.AdditionalDamage);
        Assert.Equal(5, target.CurrentHitPoints);   // 7 - 2
    }
}
