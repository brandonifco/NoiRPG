using Brp.Core.Abilities;
using Brp.Core.Contests;
using Brp.Core.Modifiers;
using Brp.Core.Primitives;
using Brp.Core.Randomness;
using Brp.Core.Resolution;
using Brp.Rules.Characters;

namespace Brp.Rules.Combat;

/// <summary>
/// Resolves Ch 6: Combat healing and recovery (#109): First Aid's per-wound healing (Ch 3, p.39),
/// natural healing (p.157), the Medicine skill's doubled rate and characteristic restoration (Ch 3,
/// p.46; p.157), and the Conditions of Medical Care Table (p.157). The injure->heal counterpart to
/// <see cref="DamageResolver"/> / <see cref="MajorWoundResolver"/>: healing restores hit points
/// through the same <see cref="AbilitySet.SetCurrentHitPoints"/> path damage removes them by, removes
/// the recovered points from the recorded <see cref="Wound"/>s, and restores drained characteristics
/// through <see cref="AbilitySet.Set"/> so derived values recompute live (Ch 2, p.13; ADR 0008).
/// First Aid and Medicine rolls resolve through the skill kernel (<see cref="ModifierPipeline"/> +
/// <see cref="SkillResolver"/>), with the printed +1/2 Medicine / +1/5 Science(Pharmacy) / +20%
/// equipment bonuses and any hazardous-conditions Difficult grade supplied as modifiers. See
/// <c>docs/decisions/0023-healing-and-recovery.md</c>.
/// <para>
/// <strong>Rates are reported; wall-clock accrual is a caller seam.</strong> Natural healing (1D3/game
/// week) and Medicine's doubled (2D3/week) and characteristic-restoration (1D3-1/week) rates accrue
/// over campaign time this layer does not model, so <see cref="RollNaturalHealingRate"/>,
/// <see cref="RollMedicineHealingRate"/>, and <see cref="RollCharacteristicRestorationRate"/> report a
/// rolled rate that a clock-aware caller applies via <see cref="ApplyWeeklyHealing"/> /
/// <see cref="ApplyCharacteristicRestoration"/> -- the same "resolver reports the rate, caller applies
/// it over time" seam as disease/poison (#96). First Aid, by contrast, is immediate and applies its
/// own healing.
/// </para>
/// <para>
/// <strong>The fatal-wound rescue is reused, not duplicated.</strong> First Aid's "restore a character
/// at 0 or negative hit points to life if their total is brought to 1+" (Ch 3, p.39) is exactly the
/// #111 fatal-wound rescue window (Ch 6, p.156): First Aid raises hit points, and
/// <see cref="ResolvesFatalWoundRescue"/> forwards to <see cref="MajorWoundResolver.SurvivesFatalWound"/>
/// (which itself reuses <see cref="DamageResolver.ResolvesToDeath"/>) to decide survival -- no second
/// copy of the death threshold or the rescue window.
/// </para>
/// </summary>
public static class HealingResolver
{
    /// <summary>
    /// Resolves and applies a First Aid attempt on a single wound, per Ch 3: Skills, "First Aid"
    /// (p.39). The roll resolves through the skill kernel with the support bonuses (+1/2 Medicine,
    /// +1/5 Science(Pharmacy), +20% equipment) figured into the rating and any hazardous-conditions
    /// Difficult grade halving it. On a healing grade the rolled amount (Success 1D3, Special 2D3,
    /// Critical 3+1D3) is capped at the wound's <see cref="Wound.DamageAmount"/> ("potentially healing
    /// it up to the amount of hit points the injury inflicted", System Notes, p.39), the hit points are
    /// restored through <see cref="AbilitySet.SetCurrentHitPoints"/>, and the healed points are removed
    /// from the wound (fully healed -> removed; partially -> reduced). A Fumble deals
    /// <see cref="FirstAidRuleset.FumbleSelfDamage"/> (1 hit point) and heals nothing; a Failure heals
    /// nothing. Both a Failure and a Fumble set <see cref="FirstAidOutcome.BlocksFurtherAttempts"/> --
    /// "no further First Aid attempts may be made" (p.39).
    /// <para>
    /// Consumes entropy in a fixed order: the First Aid d100 roll; then, only on a healing grade, the
    /// grade's healing dice. A Fumble and a Failure consume only the d100.
    /// </para>
    /// </summary>
    /// <param name="firstAidRating">The caregiver's current First Aid rating.</param>
    /// <param name="support">The stacking support bonuses and hazardous-conditions flag.</param>
    /// <param name="wound">The single wound being treated (its <see cref="Wound.DamageAmount"/> caps the heal).</param>
    /// <param name="patient">The wounded character (hit points restored through the existing HP path).</param>
    /// <param name="wounds">The patient's wound track (the treated wound is reduced or removed).</param>
    /// <param name="ruleset">The healing values.</param>
    /// <param name="entropy">The entropy source, per AGENTS.md invariant 5.</param>
    public static FirstAidOutcome ResolveFirstAid(
        Percent firstAidRating,
        FirstAidSupport support,
        Wound wound,
        AbilitySet patient,
        WoundTrack wounds,
        HealingRuleset ruleset,
        IEntropySource entropy)
    {
        ArgumentNullException.ThrowIfNull(wound);
        ArgumentNullException.ThrowIfNull(patient);
        ArgumentNullException.ThrowIfNull(wounds);
        ArgumentNullException.ThrowIfNull(ruleset);
        ArgumentNullException.ThrowIfNull(entropy);

        var roll = ResolveFirstAidRoll(firstAidRating, support, ruleset, entropy);

        if (roll.Level == SuccessLevel.Fumble)
        {
            // "Fumble: The patient takes 1 general hit point of damage" (p.39): removed through the same
            // HP path damage uses, recording no new wound (the condition is unchanged).
            var damage = ruleset.FirstAid.FumbleSelfDamage;
            patient.SetCurrentHitPoints(patient.CurrentHitPoints - damage);
            return new FirstAidOutcome(
                roll, HealedHitPoints: 0, SelfDamage: damage, WoundFullyHealed: false,
                BlocksFurtherAttempts: true, patient.CurrentHitPoints);
        }

        if (!roll.Succeeded)
        {
            // "Failure: No effect, and no further First Aid attempts may be made" (p.39).
            return new FirstAidOutcome(
                roll, HealedHitPoints: 0, SelfDamage: 0, WoundFullyHealed: false,
                BlocksFurtherAttempts: true, patient.CurrentHitPoints);
        }

        var healingDice = roll.Level switch
        {
            SuccessLevel.Critical => ruleset.FirstAid.CriticalHealing,
            SuccessLevel.Special => ruleset.FirstAid.SpecialHealing,
            _ => ruleset.FirstAid.SuccessHealing,
        };

        // Capped at the hit points this wound inflicted (System Notes, p.39). One attempt per wound, so
        // the cap is the whole wound damage rather than a running per-wound tally kept across calls --
        // "once per wound" is the caller's not-again seam (see the outcome remarks).
        var rolled = healingDice.Roll(entropy).Total;
        var healed = Math.Min(rolled, wound.DamageAmount);

        patient.SetCurrentHitPoints(patient.CurrentHitPoints + healed);
        var fullyHealed = ReduceWound(wounds, wound, healed);

        return new FirstAidOutcome(
            roll, healed, SelfDamage: 0, fullyHealed, BlocksFurtherAttempts: false, patient.CurrentHitPoints);
    }

    /// <summary>
    /// Whether a fatally wounded character (0 or negative hit points) survives after First Aid (or any
    /// other aid) has restored hit points, per Ch 3: Skills, "First Aid" (p.39: "A character at 0 or
    /// negative hit points in this or the previous round, can be restored to life if their hit point
    /// total is brought to 1+") and Ch 6, "Fatal Wounds" (p.156). This does not re-implement the
    /// rescue: it forwards to <see cref="MajorWoundResolver.SurvivesFatalWound"/> (built in #111),
    /// which reuses <see cref="DamageResolver.ResolvesToDeath"/> for the death threshold and adds the
    /// rescue window. First Aid's only job is to raise <paramref name="patient"/>'s hit points (via
    /// <see cref="ResolveFirstAid"/>) before this is checked.
    /// </summary>
    /// <param name="patient">The character, whose current hit points already reflect the aid.</param>
    /// <param name="roundsSinceFatalWound">0 is the wound round, 1 the round immediately after.</param>
    /// <param name="majorWoundRuleset">Supplies the rescue-window length (#111).</param>
    /// <param name="damageRuleset">Supplies the dead hit-point level (Ch 2, p.13; Ch 6, p.156).</param>
    public static bool ResolvesFatalWoundRescue(
        AbilitySet patient,
        int roundsSinceFatalWound,
        MajorWoundRuleset majorWoundRuleset,
        DamageRuleset damageRuleset)
    {
        ArgumentNullException.ThrowIfNull(patient);
        return MajorWoundResolver.SurvivesFatalWound(
            patient.CurrentHitPoints, roundsSinceFatalWound, majorWoundRuleset, damageRuleset);
    }

    /// <summary>
    /// Rolls and reports the flat natural healing rate for one game week, per Ch 6, "Healing Naturally"
    /// (p.157): "Your character will normally heal 1D3 hit points per game week." Reports the rate only
    /// -- the accrual over weeks, and applying the recovered points to wounds, is a caller seam
    /// (<see cref="ApplyWeeklyHealing"/>); a fresh roll is made each week, so the rate can vary. The
    /// rate is flat and not tied to CON.
    /// </summary>
    public static WeeklyHealingRate RollNaturalHealingRate(HealingRuleset ruleset, IEntropySource entropy)
    {
        ArgumentNullException.ThrowIfNull(ruleset);
        ArgumentNullException.ThrowIfNull(entropy);
        return new WeeklyHealingRate(ruleset.NaturalHealing.WeeklyRate.Roll(entropy).Total, Doubled: false);
    }

    /// <summary>
    /// Rolls and reports the Medicine-doubled healing rate for one game week, per Ch 3: Skills,
    /// "Medicine" (p.46) / Ch 6 (p.157): "The patient's healing rate doubles from 1D3 to 2D3 hit points
    /// per week." Reports the rate only -- accrual is a caller seam (<see cref="ApplyWeeklyHealing"/>),
    /// the same as natural healing. A successful Medicine roll is the caller's precondition for choosing
    /// this effect (Medicine offers several success effects, player choice); this method rolls the rate
    /// that choice yields.
    /// </summary>
    public static WeeklyHealingRate RollMedicineHealingRate(HealingRuleset ruleset, IEntropySource entropy)
    {
        ArgumentNullException.ThrowIfNull(ruleset);
        ArgumentNullException.ThrowIfNull(entropy);
        return new WeeklyHealingRate(ruleset.Medicine.DoubledWeeklyRate.Roll(entropy).Total, Doubled: true);
    }

    /// <summary>
    /// Applies <paramref name="hitPoints"/> of accrued healing to <paramref name="patient"/>, removing
    /// the recovered points from existing wounds "spreading the healing between multiple wounds as
    /// evenly as possible" (Ch 6, "Healing Naturally", p.157), and restoring the character's hit points
    /// through <see cref="AbilitySet.SetCurrentHitPoints"/>. The healing that lands is capped at the
    /// total outstanding wound damage (a character with no wounds recovers nothing to apply). Points
    /// are distributed one at a time, round-robin, across the wounds that still have damage, so the
    /// spread is as even as the wound sizes allow. Fully healed wounds are removed; partially healed
    /// wounds are reduced.
    /// </summary>
    /// <param name="patient">The healing character (hit points restored through the existing HP path).</param>
    /// <param name="wounds">The patient's wound track (recovered points removed from wounds).</param>
    /// <param name="hitPoints">The accrued hit points to apply (e.g. a reported weekly rate).</param>
    public static HealingApplication ApplyWeeklyHealing(AbilitySet patient, WoundTrack wounds, int hitPoints)
    {
        ArgumentNullException.ThrowIfNull(patient);
        ArgumentNullException.ThrowIfNull(wounds);
        ArgumentOutOfRangeException.ThrowIfNegative(hitPoints);

        // Work against a mutable copy of the current wound damages, distributing 1 HP at a time across
        // the wounds that still have capacity so the spread is as even as possible (p.157).
        var remaining = wounds.Wounds.Select(w => new WoundHealingState(w, w.DamageAmount)).ToList();
        var toApply = Math.Min(hitPoints, remaining.Sum(w => w.RemainingDamage));
        var applied = 0;

        while (applied < toApply)
        {
            var progressedThisPass = false;
            foreach (var state in remaining)
            {
                if (applied >= toApply)
                {
                    break;
                }

                if (state.RemainingDamage <= 0)
                {
                    continue;
                }

                state.RemainingDamage--;
                applied++;
                progressedThisPass = true;
            }

            if (!progressedThisPass)
            {
                break;
            }
        }

        // Commit: restore the character's hit points, then reconcile the wound track.
        patient.SetCurrentHitPoints(patient.CurrentHitPoints + applied);

        var perWound = new List<WoundHealing>();
        foreach (var state in remaining)
        {
            var healedOnThisWound = state.Original.DamageAmount - state.RemainingDamage;
            if (healedOnThisWound == 0)
            {
                continue;
            }

            var fullyHealed = state.RemainingDamage == 0;
            wounds.Remove(state.Original);
            if (!fullyHealed)
            {
                wounds.Add(new Wound(state.Original.Description, state.RemainingDamage));
            }

            perWound.Add(new WoundHealing(state.Original, healedOnThisWound, fullyHealed));
        }

        return new HealingApplication(applied, perWound, patient.CurrentHitPoints);
    }

    /// <summary>
    /// Rolls and reports the Medicine-gated characteristic restoration rate for one week, per Ch 3:
    /// Skills, "Medicine" (p.46): a stabilized poisoned/diseased character "recovers 1D3-1 hit points or
    /// characteristic points per week" (Success), "1D3 characteristic points" (Special), or "1D3+1
    /// characteristic points" (Critical). Reports the rolled rate only -- applying it to a characteristic
    /// (and accruing it week to week) is a caller seam (<see cref="ApplyCharacteristicRestoration"/>),
    /// since the recovery accrues over campaign time this layer does not model. Major-wound
    /// characteristic loss recovers only "through training or various means" (p.156, vague) -- this
    /// method does not model that; it reports the Medicine-gated rate the book prints.
    /// </summary>
    /// <param name="grade">The grade of the successful Medicine roll (drives which formula is rolled).</param>
    /// <param name="ruleset">The healing values.</param>
    /// <param name="entropy">The entropy source, per AGENTS.md invariant 5.</param>
    public static CharacteristicRestorationRate RollCharacteristicRestorationRate(
        SuccessLevel grade, HealingRuleset ruleset, IEntropySource entropy)
    {
        ArgumentNullException.ThrowIfNull(ruleset);
        ArgumentNullException.ThrowIfNull(entropy);
        if (grade < SuccessLevel.Success)
        {
            throw new ArgumentOutOfRangeException(
                nameof(grade), grade, "Characteristic restoration requires a successful Medicine roll.");
        }

        var dice = grade switch
        {
            SuccessLevel.Critical => ruleset.Medicine.CharacteristicRestorationCritical,
            SuccessLevel.Special => ruleset.Medicine.CharacteristicRestorationSpecial,
            _ => ruleset.Medicine.CharacteristicRestorationSuccess,
        };

        return new CharacteristicRestorationRate(grade, dice.Roll(entropy).Total);
    }

    /// <summary>
    /// Applies <paramref name="points"/> of accrued characteristic restoration to
    /// <paramref name="characteristic"/> through <see cref="AbilitySet.Set"/> so the Layer 1 derived
    /// characteristics (hit points, major-wound level, skill category modifiers) recompute live (Ch 2,
    /// p.13; ADR 0008) -- the same recompute path the #96 injury drain lowers a characteristic by, run
    /// in reverse. The new value is capped at the characteristic's ruleset maximum where one is defined
    /// (a characteristic cannot be restored above its allowed ceiling); restoring only up to the
    /// pre-drain value is the caller's concern (this layer does not remember the original). Returns the
    /// resulting characteristic value. Note (Ch 2, p.13): restoring a characteristic does not resurrect
    /// a current-hit-point total already clamped by an earlier reduction -- <see cref="AbilitySet"/>
    /// enforces that, so a restored CON raises maximum hit points without healing current ones.
    /// </summary>
    public static int ApplyCharacteristicRestoration(AbilitySet patient, CharacteristicId characteristic, int points)
    {
        ArgumentNullException.ThrowIfNull(patient);
        ArgumentOutOfRangeException.ThrowIfNegative(points);

        if (!patient.Ruleset.Characteristics.TryGetValue(characteristic, out var definition))
        {
            throw new KeyNotFoundException($"Unknown characteristic '{characteristic}'.");
        }

        var raised = patient.ValueOf(characteristic) + points;
        var capped = definition.Maximum is int maximum ? Math.Min(raised, maximum) : raised;
        patient.Set(characteristic, capped);
        return capped;
    }

    /// <summary>
    /// Resolves the healing a Conditions of Medical Care tier grants for one week, per Ch 6:
    /// "Conditions of Medical Care" (p.157), and applies it to <paramref name="patient"/>. The three
    /// printed tiers:
    /// <list type="bullet">
    /// <item><description>
    /// <strong>Poor</strong> -- the caregiver must succeed a Difficult First Aid or Medicine roll for
    /// any healing. Success heals the row's natural rate (1D3); Failure heals nothing; a Fumble instead
    /// deals the row's additional damage (1D3). The roll resolves through the skill kernel with the
    /// Difficult grade applied.
    /// </description></item>
    /// <item><description><strong>Decent</strong> -- heals the row's natural rate (1D3) with no roll.</description></item>
    /// <item><description>
    /// <strong>Excellent</strong> -- heals the row's natural rate (1D3) with no roll, and reports that a
    /// further successful First Aid or Medicine use allows possible additional healing (that additional
    /// use is a separate <see cref="ResolveFirstAid"/> / Medicine call -- the book prints no amount, so
    /// this does not invent one).
    /// </description></item>
    /// </list>
    /// <para>
    /// Consumes entropy in a fixed order: for the poor tier, the caregiver d100 roll first, then (only
    /// on success) the natural-healing dice or (only on a fumble) the additional-damage dice; for the
    /// decent and excellent tiers, only the natural-healing dice.
    /// </para>
    /// </summary>
    /// <param name="tier">The care tier (an <see cref="IHealingAdjudicator.DecideConditionsTier"/> ruling).</param>
    /// <param name="caregiverRating">
    /// The caregiver's current rating in whichever skill they apply (an
    /// <see cref="IHealingAdjudicator.DecideCaregiver"/> ruling). Used only by the poor tier's gating roll.
    /// </param>
    /// <param name="caregiverPrintedBaseChance">
    /// The printed base chance of the caregiver's chosen skill (First Aid 30% or Medicine 05%), for the
    /// 5%-floor rule. Used only by the poor tier's gating roll.
    /// </param>
    /// <param name="patient">The healing character.</param>
    /// <param name="wounds">The patient's wound track.</param>
    /// <param name="ruleset">The healing values (the Conditions of Medical Care table).</param>
    /// <param name="entropy">The entropy source, per AGENTS.md invariant 5.</param>
    public static ConditionsOfCareOutcome ResolveConditionsOfCare(
        MedicalCareTier tier,
        Percent caregiverRating,
        Percent caregiverPrintedBaseChance,
        AbilitySet patient,
        WoundTrack wounds,
        HealingRuleset ruleset,
        IEntropySource entropy)
    {
        ArgumentNullException.ThrowIfNull(patient);
        ArgumentNullException.ThrowIfNull(wounds);
        ArgumentNullException.ThrowIfNull(ruleset);
        ArgumentNullException.ThrowIfNull(entropy);

        var row = ruleset.ConditionsOfMedicalCare.ForTier(tier);

        if (!row.RequiresCaregiverRoll)
        {
            // Decent / Excellent: heal the natural rate with no gating roll.
            var healingRate = row.NaturalHealing.Roll(entropy).Total;
            var application = ApplyWeeklyHealing(patient, wounds, healingRate);
            return new ConditionsOfCareOutcome(
                tier, CaregiverRoll: null, HealingRate: healingRate, application, AdditionalDamage: 0,
                row.AllowsAdditionalHealing);
        }

        // Poor: a Difficult First Aid or Medicine roll gates all healing.
        var modifiers = row.CaregiverRollDifficulty == HealingRollDifficulty.Difficult
            ? new Modifier[] { DifficultyModifier.Difficult($"conditions of medical care: {tier}") }
            : [];
        var caregiverRoll = ModifierPipeline.Evaluate(caregiverRating, modifiers)
            .Resolve(caregiverPrintedBaseChance, entropy)
            ?? throw new InvalidOperationException("The conditions-of-care caregiver roll was unexpectedly gated.");

        if (caregiverRoll.Level == SuccessLevel.Fumble)
        {
            // "A fumble inflicts 1D3 additional hit points in damage" (p.157): applied through the
            // non-weapon damage path so hit-point tracking and the wound record are identical to any
            // other injury.
            var additionalDamage = row.FumbleAdditionalDamage is null
                ? 0
                : row.FumbleAdditionalDamage.Roll(entropy).Total;
            if (additionalDamage > 0)
            {
                patient.SetCurrentHitPoints(patient.CurrentHitPoints - additionalDamage);
            }

            return new ConditionsOfCareOutcome(
                tier, caregiverRoll, HealingRate: 0, Application: null, additionalDamage, AllowsAdditionalHealing: false);
        }

        if (!caregiverRoll.Succeeded)
        {
            // "if unsuccessful no healing occurs" (p.157).
            return new ConditionsOfCareOutcome(
                tier, caregiverRoll, HealingRate: 0, Application: null, AdditionalDamage: 0, AllowsAdditionalHealing: false);
        }

        // "If successful, the patient heals normally (1D3 hit points/week)" (p.157).
        var rate = row.NaturalHealing.Roll(entropy).Total;
        var applied = ApplyWeeklyHealing(patient, wounds, rate);
        return new ConditionsOfCareOutcome(
            tier, caregiverRoll, rate, applied, AdditionalDamage: 0, row.AllowsAdditionalHealing);
    }

    private static RollOutcome ResolveFirstAidRoll(
        Percent firstAidRating, FirstAidSupport support, HealingRuleset ruleset, IEntropySource entropy)
    {
        var firstAid = ruleset.FirstAid;
        var modifiers = new List<Modifier>();

        // "may add 1/2 of their Medicine skill rating and 1/5 of their Science (Pharmacy) skill rating
        // as a temporary bonus" (p.39). These are bonuses to the rating itself (part of what the
        // caregiver brings), so they are Permanent -- figured in before any hazardous-conditions
        // Difficult grade halves the roll (ADR 0007). The fractions round down (house choice -- the
        // book prints no rounding; see ADR 0023).
        var medicineBonus = Rounding.Divide(
            support.MedicineRating * firstAid.MedicineBonusNumerator, firstAid.MedicineBonusDenominator, RoundingMode.Down);
        if (medicineBonus > 0)
        {
            modifiers.Add(new AdditiveModifier("+1/2 Medicine", medicineBonus, AdditiveKind.Permanent));
        }

        var pharmacyBonus = Rounding.Divide(
            support.SciencePharmacyRating * firstAid.SciencePharmacyBonusNumerator,
            firstAid.SciencePharmacyBonusDenominator,
            RoundingMode.Down);
        if (pharmacyBonus > 0)
        {
            modifiers.Add(new AdditiveModifier("+1/5 Science (Pharmacy)", pharmacyBonus, AdditiveKind.Permanent));
        }

        // Equipment "may add up to a +20% bonus" (p.39): the caller supplies the actual bonus, capped
        // at the data-defined maximum.
        var equipmentBonus = Math.Min(support.EquipmentBonusPercent, firstAid.EquipmentBonusMaxPercent);
        if (equipmentBonus > 0)
        {
            modifiers.Add(new AdditiveModifier("medical equipment", equipmentBonus, AdditiveKind.Permanent));
        }

        if (support.HazardousConditions)
        {
            // "Hazardous or unsanitary conditions may make rolls Difficult" (p.39).
            modifiers.Add(DifficultyModifier.Difficult("hazardous or unsanitary conditions"));
        }

        return ModifierPipeline.Evaluate(firstAidRating, modifiers)
            .Resolve(firstAid.PrintedBaseChance, entropy)
            ?? throw new InvalidOperationException("The First Aid roll was unexpectedly gated.");
    }

    private static bool ReduceWound(WoundTrack wounds, Wound wound, int healed)
    {
        // A single First Aid attempt heals up to the wound's whole damage, so "reduce" here is either
        // a full removal or a one-shot reduction; there is no running per-wound tally to keep.
        wounds.Remove(wound);
        var remainingDamage = wound.DamageAmount - healed;
        if (remainingDamage <= 0)
        {
            return true;
        }

        wounds.Add(new Wound(wound.Description, remainingDamage));
        return false;
    }

    private sealed class WoundHealingState(Wound original, int remainingDamage)
    {
        public Wound Original { get; } = original;

        public int RemainingDamage { get; set; } = remainingDamage;
    }
}

/// <summary>
/// The stacking support a caregiver brings to a First Aid roll (Ch 3: Skills, "First Aid", p.39):
/// the caregiver's Medicine and Science (Pharmacy) ratings (which add fractions of themselves to the
/// roll), medical equipment (up to +20%), and whether hazardous or unsanitary conditions make the
/// roll Difficult. All default to none -- an unaided First Aid roll passes <c>default</c>.
/// </summary>
/// <param name="MedicineRating">The caregiver's Medicine rating; 1/2 of it is added to the roll.</param>
/// <param name="SciencePharmacyRating">The caregiver's Science (Pharmacy) rating; 1/5 of it is added.</param>
/// <param name="EquipmentBonusPercent">The equipment bonus, capped at the ruleset maximum (+20%).</param>
/// <param name="HazardousConditions">Whether conditions make the roll Difficult.</param>
public readonly record struct FirstAidSupport(
    int MedicineRating = 0,
    int SciencePharmacyRating = 0,
    int EquipmentBonusPercent = 0,
    bool HazardousConditions = false);

/// <summary>
/// The result of a First Aid attempt on a single wound (Ch 3: Skills, "First Aid", p.39).
/// </summary>
/// <param name="Roll">The resolved First Aid skill roll (base chance, effective chance, and grade).</param>
/// <param name="HealedHitPoints">The hit points restored (0 on a Failure or Fumble; capped at the wound's damage).</param>
/// <param name="SelfDamage">The hit points a Fumble dealt the patient (0 otherwise).</param>
/// <param name="WoundFullyHealed">Whether the treated wound was fully healed and removed from the track.</param>
/// <param name="BlocksFurtherAttempts">
/// Whether "no further First Aid attempts may be made" on this wound (true after any Failure or
/// Fumble). Enforcing "only one attempt per wound" across calls -- not re-treating a wound already
/// successfully First-Aided -- is the caller's seam, the same as #111's caller-tracked same-day totals.
/// </param>
/// <param name="ResultingHitPoints">The patient's current hit points after the attempt.</param>
public sealed record FirstAidOutcome(
    RollOutcome Roll,
    int HealedHitPoints,
    int SelfDamage,
    bool WoundFullyHealed,
    bool BlocksFurtherAttempts,
    int ResultingHitPoints);

/// <summary>
/// A reported weekly healing rate (Ch 6, "Healing Naturally", p.157): a rolled quantity of hit points
/// a clock-aware caller applies over game weeks. Deliberately carries no applied effect -- the accrual
/// is a caller seam.
/// </summary>
/// <param name="HitPointsPerWeek">The rolled hit points to heal this week.</param>
/// <param name="Doubled">Whether this is the Medicine-doubled rate (2D3) rather than the natural rate (1D3).</param>
public readonly record struct WeeklyHealingRate(int HitPointsPerWeek, bool Doubled);

/// <summary>
/// A reported Medicine-gated characteristic restoration rate (Ch 3: Skills, "Medicine", p.46): a
/// rolled quantity of characteristic points recovered per week, by the grade of the Medicine roll.
/// Carries no applied effect -- the accrual is a caller seam (<see cref="HealingResolver.ApplyCharacteristicRestoration"/>).
/// </summary>
/// <param name="Grade">The grade of the successful Medicine roll the rate was rolled for.</param>
/// <param name="PointsPerWeek">The rolled characteristic points to recover this week (1D3-1 floors at 0).</param>
public readonly record struct CharacteristicRestorationRate(SuccessLevel Grade, int PointsPerWeek);

/// <summary>One wound's share of an applied healing (Ch 6, "Healing Naturally", p.157).</summary>
/// <param name="Wound">The wound the healing was removed from (the original record).</param>
/// <param name="HealedHitPoints">The hit points removed from this wound.</param>
/// <param name="FullyHealed">Whether the wound was fully healed and removed from the track.</param>
public sealed record WoundHealing(Wound Wound, int HealedHitPoints, bool FullyHealed);

/// <summary>
/// The result of applying accrued healing across a character's wounds (Ch 6, "Healing Naturally",
/// p.157): the total hit points restored (capped at outstanding wound damage), the per-wound spread,
/// and the resulting hit points.
/// </summary>
/// <param name="TotalHealed">The hit points actually restored (may be less than requested if wounds ran out).</param>
/// <param name="PerWound">Each wound that received healing, with its share.</param>
/// <param name="ResultingHitPoints">The patient's current hit points after the healing.</param>
public sealed record HealingApplication(
    int TotalHealed,
    IReadOnlyList<WoundHealing> PerWound,
    int ResultingHitPoints);

/// <summary>
/// The result of resolving a Conditions of Medical Care tier's weekly healing (Ch 6, "Conditions of
/// Medical Care", p.157).
/// </summary>
/// <param name="Tier">The care tier resolved.</param>
/// <param name="CaregiverRoll">
/// The gating caregiver roll (poor tier only), or <see langword="null"/> for the decent and excellent
/// tiers, which heal without a roll.
/// </param>
/// <param name="HealingRate">The hit points healed this week (0 when a poor-tier roll failed or fumbled).</param>
/// <param name="Application">
/// The applied healing spread across wounds, or <see langword="null"/> when no healing occurred.
/// </param>
/// <param name="AdditionalDamage">The extra damage a fumbled poor-tier roll dealt (0 otherwise).</param>
/// <param name="AllowsAdditionalHealing">
/// Whether a further successful First Aid or Medicine use allows possible additional healing (excellent
/// tier only) -- that use is a separate resolver call; no amount is invented here.
/// </param>
public sealed record ConditionsOfCareOutcome(
    MedicalCareTier Tier,
    RollOutcome? CaregiverRoll,
    int HealingRate,
    HealingApplication? Application,
    int AdditionalDamage,
    bool AllowsAdditionalHealing);
