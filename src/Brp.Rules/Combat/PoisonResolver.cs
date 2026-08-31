using Brp.Core.Abilities;
using Brp.Core.Contests;
using Brp.Core.Primitives;
using Brp.Core.Randomness;
using Brp.Rules.Characters;

namespace Brp.Rules.Combat;

/// <summary>
/// Resolves and applies Ch 7: Spot Rules, "Poison" and "Poison Antidotes" (pp.175-176): a poison's
/// POT is matched against a character's CON on the resistance table
/// (<see cref="ResistanceResolver"/>); overcoming CON deals the full POT, otherwise half (round up).
/// The damage lands on hit points (through the non-weapon overload of
/// <see cref="DamageResolver.ApplyDamage(AbilitySet, WoundTrack, int, DamageRuleset, string)"/>) or
/// drains a characteristic (through <see cref="AbilitySet.Set"/>, so derived values recompute). One
/// of the injury/effect spot rules (#96). See <c>docs/decisions/0019-injury-spot-rules.md</c>.
/// <para>
/// An antidote taken within the window (<see cref="PoisonRuleset.AntidoteWindowTurns"/>) subtracts
/// its POT from the poison POT before the resistance roll and damage are figured; a mismatched
/// antidote's benefit is a gamemaster call (<see cref="InjuryDecisionId.AntidoteCrossType"/>). Two
/// doses are two separate <see cref="ResolvePoison"/> calls, each drawing its own resistance roll --
/// "two doses of a POT 10 poison are not the same as one dose of a POT 20 poison" (p.175).
/// </para>
/// </summary>
public static class PoisonResolver
{
    /// <summary>
    /// The effective POT a taken antidote contributes against a poison, per Ch 7, "Poison
    /// Antidotes" (p.176). Outside the window (<paramref name="turnsBeforePoisoning"/> greater than
    /// <see cref="PoisonRuleset.AntidoteWindowTurns"/>) the antidote is spent and contributes
    /// nothing; within it, a same-type antidote contributes its full POT and a cross-type antidote
    /// contributes the gamemaster's lessened benefit
    /// (<see cref="IInjuryAdjudicator.DecideAntidoteCrossTypePotency"/>).
    /// </summary>
    public static int EffectiveAntidotePotency(
        int antidotePotency,
        int turnsBeforePoisoning,
        bool sameType,
        PoisonRuleset ruleset,
        IInjuryAdjudicator adjudicator)
    {
        ArgumentNullException.ThrowIfNull(ruleset);
        ArgumentNullException.ThrowIfNull(adjudicator);
        ArgumentOutOfRangeException.ThrowIfNegative(antidotePotency);
        ArgumentOutOfRangeException.ThrowIfNegative(turnsBeforePoisoning);

        if (turnsBeforePoisoning > ruleset.AntidoteWindowTurns)
        {
            return 0;
        }

        if (sameType)
        {
            return antidotePotency;
        }

        var lessened = adjudicator.DecideAntidoteCrossTypePotency(antidotePotency);
        return Math.Clamp(lessened, 0, antidotePotency);
    }

    /// <summary>
    /// Resolves one dose of poison of the given <paramref name="poisonPotency"/> against
    /// <paramref name="constitution"/>, after subtracting any <paramref name="effectiveAntidotePotency"/>
    /// (from <see cref="EffectiveAntidotePotency"/>). Draws one resistance roll from
    /// <paramref name="entropy"/> unless the antidote fully neutralizes the poison, in which case no
    /// roll is drawn and no damage is dealt.
    /// </summary>
    public static PoisonOutcome ResolvePoison(
        int poisonPotency,
        int constitution,
        int effectiveAntidotePotency,
        PoisonRuleset ruleset,
        IEntropySource entropy)
    {
        ArgumentNullException.ThrowIfNull(ruleset);
        ArgumentNullException.ThrowIfNull(entropy);
        ArgumentOutOfRangeException.ThrowIfNegative(poisonPotency);
        ArgumentOutOfRangeException.ThrowIfNegative(effectiveAntidotePotency);

        var effectivePotency = Math.Max(0, poisonPotency - effectiveAntidotePotency);
        if (effectivePotency == 0)
        {
            // Fully neutralized (or a POT 0 poison): no resistance roll, no damage.
            return new PoisonOutcome(effectivePotency, Resistance: null, Overcame: false, Damage: 0);
        }

        var resistance = ResistanceResolver.Resolve(active: effectivePotency, passive: constitution, entropy);
        var damage = resistance.Succeeded
            ? effectivePotency
            : Rounding.Divide(
                effectivePotency * ruleset.NotOvercomeNumerator, ruleset.NotOvercomeDenominator, RoundingMode.Up);

        return new PoisonOutcome(effectivePotency, resistance, resistance.Succeeded, damage);
    }

    /// <summary>
    /// Applies a poison's <see cref="PoisonOutcome.Damage"/> to <paramref name="target"/>'s hit
    /// points via the non-weapon damage overload, recording a wound.
    /// </summary>
    public static DamageApplicationResult ApplyHitPointDamage(
        AbilitySet target,
        WoundTrack wounds,
        PoisonOutcome outcome,
        DamageRuleset damageRuleset,
        string woundDescription)
    {
        ArgumentNullException.ThrowIfNull(outcome);
        return DamageResolver.ApplyDamage(target, wounds, outcome.Damage, damageRuleset, woundDescription);
    }

    /// <summary>
    /// Drains a poison's <see cref="PoisonOutcome.Damage"/> from <paramref name="characteristic"/>
    /// via <see cref="AbilitySet.Set"/>, so derived values (hit points, category modifiers)
    /// recompute. The new value is floored at the characteristic's ruleset minimum. Returns the
    /// resulting characteristic value.
    /// </summary>
    public static int ApplyCharacteristicDrain(
        AbilitySet abilities, CharacteristicId characteristic, PoisonOutcome outcome)
    {
        ArgumentNullException.ThrowIfNull(abilities);
        ArgumentNullException.ThrowIfNull(outcome);

        return InjuryDrain.Apply(abilities, characteristic, outcome.Damage);
    }

    /// <summary>
    /// The onset delay before a resolved poison's damage takes effect, per Ch 7, "Poison" (p.175),
    /// as decided by the gamemaster (<see cref="InjuryDecisionId.PoisonOnset"/>): a bespoke delay if
    /// specified, otherwise the printed default for the poison's onset speed.
    /// </summary>
    public static PoisonOnset ResolveOnset(PoisonOnsetRuling ruling, PoisonRuleset ruleset)
    {
        ArgumentNullException.ThrowIfNull(ruleset);

        return ruling.Speed switch
        {
            PoisonOnsetSpeed.FastActing => new PoisonOnset(
                ruling.GamemasterSpecifiedDelay ?? ruleset.OnsetFastActingRounds, PoisonOnsetUnit.CombatRounds),
            PoisonOnsetSpeed.SlowActing => new PoisonOnset(
                ruling.GamemasterSpecifiedDelay ?? ruleset.OnsetSlowActingTurns, PoisonOnsetUnit.FullTurns),
            _ => throw new ArgumentOutOfRangeException(nameof(ruling)),
        };
    }
}

/// <summary>The resolved result of one dose of poison (<see cref="PoisonResolver.ResolvePoison"/>).</summary>
/// <param name="EffectivePotency">The poison POT after antidote subtraction (the value matched against CON).</param>
/// <param name="Resistance">
/// The resistance roll matching <see cref="EffectivePotency"/> against CON, or <see langword="null"/>
/// when the antidote fully neutralized the poison and no roll was drawn.
/// </param>
/// <param name="Overcame">Whether the poison overcame CON (full POT) rather than being resisted (half POT).</param>
/// <param name="Damage">The hit points or characteristic points the poison deals.</param>
public sealed record PoisonOutcome(
    int EffectivePotency,
    ResistanceOutcome? Resistance,
    bool Overcame,
    int Damage);

/// <summary>The time unit a <see cref="PoisonOnset"/> delay is measured in (Ch 7, p.175).</summary>
public enum PoisonOnsetUnit
{
    /// <summary>Combat rounds -- the unit for a fast-acting poison's default delay.</summary>
    CombatRounds,

    /// <summary>Full turns -- the unit for a slower poison's default delay.</summary>
    FullTurns,
}

/// <summary>A resolved poison onset delay (Ch 7, p.175).</summary>
/// <param name="Delay">The number of <paramref name="Unit"/> before the poison's damage takes effect.</param>
/// <param name="Unit">The time unit the delay is measured in.</param>
public readonly record struct PoisonOnset(int Delay, PoisonOnsetUnit Unit);
