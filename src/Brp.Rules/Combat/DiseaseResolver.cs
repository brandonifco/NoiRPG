using Brp.Core.Abilities;
using Brp.Core.Randomness;
using Brp.Core.Resolution;

namespace Brp.Rules.Combat;

/// <summary>
/// Resolves and applies Ch 7: Spot Rules, "Disease" (p.170): a Stamina roll to contract, the
/// CON×N recovery ladder, and the "Illness Severity Table" characteristic drain. Characteristic
/// loss is applied through <see cref="AbilitySet.Set"/> (via <see cref="InjuryDrain"/>) so derived
/// values recompute (Ch 2, p.13). One of the injury/effect spot rules (#96). See
/// <c>docs/decisions/0019-injury-spot-rules.md</c>.
/// <para>
/// <strong>House reading (marked in ADR 0019):</strong> the "Stamina roll" to contract a disease
/// is modeled as the standard CON roll (CON×5, the book's standard characteristic-roll multiplier),
/// the book not restating Stamina's rating in this section.
/// </para>
/// <para>
/// The Illness Severity Table gives a <em>rate</em> of characteristic loss (one point per
/// week/day/hour/minute by degree), not a one-time quantity, so <see cref="ResolveSeverity"/> reports
/// the degree and period and <see cref="ApplyCharacteristicLoss"/> applies the points a clock-aware
/// caller has accrued over time -- the "resolver reports the rate, caller applies it over time" seam
/// this piece shares with poison onset.
/// </para>
/// </summary>
public static class DiseaseResolver
{
    private static readonly CharacteristicId Constitution = new("CON");

    /// <summary>
    /// Rolls to contract a minor disease, per Ch 7, "Disease" (p.170): "make a Stamina roll to see
    /// if the disease is contracted. Success means that it is avoided, while failure means that your
    /// character catches the disease." Modeled as the standard CON roll (see the type remarks).
    /// </summary>
    public static DiseaseContraction RollContraction(AbilitySet abilities, IEntropySource entropy)
    {
        ArgumentNullException.ThrowIfNull(abilities);
        ArgumentNullException.ThrowIfNull(entropy);

        var staminaRoll = abilities.Ruleset.StandardCharacteristicRoll(Constitution);
        var outcome = AbilityResolver.Resolve(abilities, staminaRoll, [], entropy)
            ?? throw new InvalidOperationException("The Stamina contraction roll was unexpectedly gated.");

        return new DiseaseContraction(outcome, Contracted: !outcome.Succeeded);
    }

    /// <summary>
    /// Rolls the CON×N recovery ladder, per Ch 7, "Disease" (p.170): day two is CON×2, rising by
    /// <see cref="DiseaseRuleset.RecoveryLadderMultiplierIncrementPerDay"/> each successive day, a
    /// fumble reducing the multiplier by
    /// <see cref="DiseaseRuleset.RecoveryLadderFumbleMultiplierPenalty"/> and each outstanding
    /// strenuous condition reducing it by
    /// <see cref="DiseaseRuleset.RecoveryLadderStrenuousConditionPenalty"/>. The multiplier is
    /// clamped to the ability ruleset's supported range. Stops on the first successful roll (the
    /// character recovers) or after <paramref name="maxDays"/> days.
    /// </summary>
    /// <param name="abilities">The recovering character (CON is read live for each roll).</param>
    /// <param name="strenuousConditionCount">Outstanding strenuous conditions (0 = rest and care).</param>
    /// <param name="ruleset">The disease values.</param>
    /// <param name="entropy">The entropy source, per AGENTS.md invariant 5.</param>
    /// <param name="maxDays">The maximum number of recovery days to roll before giving up.</param>
    public static RecoveryLadderOutcome ResolveRecoveryLadder(
        AbilitySet abilities,
        int strenuousConditionCount,
        DiseaseRuleset ruleset,
        IEntropySource entropy,
        int maxDays)
    {
        ArgumentNullException.ThrowIfNull(abilities);
        ArgumentNullException.ThrowIfNull(ruleset);
        ArgumentNullException.ThrowIfNull(entropy);
        ArgumentOutOfRangeException.ThrowIfNegative(strenuousConditionCount);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxDays);

        var minMultiplier = abilities.Ruleset.MinimumCharacteristicRollMultiplier;
        var maxMultiplier = abilities.Ruleset.MaximumCharacteristicRollMultiplier;

        var days = new List<DailyRecoveryRoll>();
        var failures = 0;
        var fumblePenalty = 0;
        var recovered = false;

        for (var day = 0; day < maxDays; day++)
        {
            var dayMultiplier = ruleset.RecoveryLadderStartingMultiplier
                + (day * ruleset.RecoveryLadderMultiplierIncrementPerDay);
            var reduced = dayMultiplier
                - (strenuousConditionCount * ruleset.RecoveryLadderStrenuousConditionPenalty)
                - fumblePenalty;
            var effectiveMultiplier = Math.Clamp(reduced, minMultiplier, maxMultiplier);

            var roll = abilities.Ruleset.CharacteristicRoll(Constitution, effectiveMultiplier);
            var outcome = AbilityResolver.Resolve(abilities, roll, [], entropy)
                ?? throw new InvalidOperationException("A CON recovery roll was unexpectedly gated.");
            days.Add(new DailyRecoveryRoll(day, effectiveMultiplier, outcome));

            if (outcome.Succeeded)
            {
                recovered = true;
                break;
            }

            failures++;
            if (outcome.Level == SuccessLevel.Fumble)
            {
                fumblePenalty += ruleset.RecoveryLadderFumbleMultiplierPenalty;
            }
        }

        return new RecoveryLadderOutcome(failures, recovered, days);
    }

    /// <summary>
    /// Looks up the Illness Severity Table degree for <paramref name="failures"/> failed CON rolls
    /// and reports the resulting <em>rate</em> of characteristic loss -- one point per the degree's
    /// period (Mild per week, Acute per day, Severe per hour, Terminal per minute), per Ch 7,
    /// "Disease"/"Illness Severity Table" (p.170). This method applies no drain: the loss accrues
    /// over wall-clock time, so the actual point loss is applied by a clock-aware caller via
    /// <see cref="ApplyCharacteristicLoss"/> -- the same "resolver reports the rate, caller applies
    /// it over time" seam as poison onset (<see cref="PoisonResolver.ResolveOnset"/>). Baking a flat
    /// <paramref name="failures"/>-point quantity would invent a number the book does not print (the
    /// table gives a rate, not a one-time amount).
    /// </summary>
    public static DiseaseSeverity ResolveSeverity(int failures, DiseaseRuleset ruleset)
    {
        ArgumentNullException.ThrowIfNull(ruleset);
        ArgumentOutOfRangeException.ThrowIfNegative(failures);

        var band = ruleset.IllnessSeverityTable.ForFailures(failures);
        return new DiseaseSeverity(band.Degree, band.LossPeriod);
    }

    /// <summary>
    /// Drains disease characteristic loss accrued over time, per Ch 7, "Disease" (p.170): "The first
    /// characteristic point is lost within 24 hours of initially contracting the disease... Each
    /// successive loss is added to the total whenever the CON roll is made to recover." The
    /// <paramref name="points"/> a clock-aware caller has accrued -- from the
    /// <see cref="ResolveSeverity"/> rate over elapsed periods, drained from the disease's affected
    /// characteristic (<see cref="Core.Contests.InjuryDecisionId.DiseaseAffectedCharacteristic"/>) --
    /// are applied through <see cref="AbilitySet.Set"/> (via <see cref="InjuryDrain"/>), so derived
    /// values recompute. Returns the resulting characteristic value.
    /// </summary>
    public static int ApplyCharacteristicLoss(AbilitySet abilities, CharacteristicId characteristic, int points)
    {
        ArgumentNullException.ThrowIfNull(abilities);
        return InjuryDrain.Apply(abilities, characteristic, points);
    }

    /// <summary>
    /// Rolls a minor disease's incidental cost, per Ch 7, "Disease" (p.170): "1 or 2 hit points and
    /// 1D6 fatigue points over a few days." The hit-point loss can be applied through
    /// <see cref="DamageResolver.ApplyDamage(AbilitySet, Characters.WoundTrack, int, DamageRuleset, string)"/>;
    /// the fatigue-point loss is reported for callers but not applied here (no fatigue-point
    /// subsystem exists yet -- out of scope for #96).
    /// </summary>
    public static MinorDiseaseEffect RollMinorDiseaseEffect(DiseaseRuleset ruleset, IEntropySource entropy)
    {
        ArgumentNullException.ThrowIfNull(ruleset);
        ArgumentNullException.ThrowIfNull(entropy);

        var hitPointLoss = ruleset.MinorDiseaseHitPointLoss.Roll(entropy).Total;
        var fatigueLoss = ruleset.MinorDiseaseFatigueLoss.Roll(entropy).Total;
        return new MinorDiseaseEffect(hitPointLoss, fatigueLoss);
    }
}

/// <summary>The result of a Stamina contraction roll (Ch 7, p.170).</summary>
/// <param name="StaminaRoll">The resolved Stamina (CON) roll.</param>
/// <param name="Contracted">Whether the disease was contracted (the roll failed).</param>
public sealed record DiseaseContraction(RollOutcome StaminaRoll, bool Contracted);

/// <summary>One day's recovery roll on the CON×N ladder (Ch 7, p.170).</summary>
/// <param name="Day">The zero-based day index (day 0 is the book's "second day", CON×2).</param>
/// <param name="EffectiveMultiplier">The CON multiplier actually rolled, after penalties and clamping.</param>
/// <param name="Roll">The resolved CON roll.</param>
public sealed record DailyRecoveryRoll(int Day, int EffectiveMultiplier, RollOutcome Roll);

/// <summary>The result of running the CON×N recovery ladder (Ch 7, p.170).</summary>
/// <param name="Failures">The number of failed recovery rolls (the Illness Severity Table index).</param>
/// <param name="Recovered">Whether the character recovered within the day budget.</param>
/// <param name="Days">Each day's roll, in order.</param>
public sealed record RecoveryLadderOutcome(int Failures, bool Recovered, IReadOnlyList<DailyRecoveryRoll> Days);

/// <summary>
/// The severity of a disease looked up from the Illness Severity Table (Ch 7, p.170): a degree and
/// the period over which one characteristic point is lost. Deliberately carries no baked point
/// quantity -- the loss is a rate over wall-clock time, applied by a clock-aware caller via
/// <see cref="DiseaseResolver.ApplyCharacteristicLoss"/>.
/// </summary>
/// <param name="Degree">The degree of illness for the failure count.</param>
/// <param name="LossPeriod">The period over which one characteristic point is lost (None = no loss).</param>
public readonly record struct DiseaseSeverity(IllnessDegree Degree, IllnessLossPeriod LossPeriod);

/// <summary>The incidental cost of a minor disease (Ch 7, p.170).</summary>
/// <param name="HitPointLoss">Hit points lost over the illness.</param>
/// <param name="FatigueLoss">Fatigue points lost (reported only; no fatigue subsystem yet).</param>
public readonly record struct MinorDiseaseEffect(int HitPointLoss, int FatigueLoss);
