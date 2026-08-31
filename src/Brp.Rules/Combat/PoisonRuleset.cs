namespace Brp.Rules.Combat;

/// <summary>
/// The data-defined values of Ch 7: Spot Rules, "Poison" and "Poison Antidotes" (p.176)
/// (AGENTS.md invariant 7: rules values are data, not constants). Loaded from
/// <c>injury-ruleset.json</c> by <c>Brp.Data.NoirInjuryRuleset.Load()</c>. See
/// <c>docs/decisions/0019-injury-spot-rules.md</c>.
/// </summary>
public sealed class PoisonRuleset
{
    /// <summary>Creates a poison ruleset from data-defined values.</summary>
    public PoisonRuleset(
        int notOvercomeNumerator,
        int notOvercomeDenominator,
        int onsetFastActingRounds,
        int onsetSlowActingTurns,
        int antidoteWindowTurns)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(notOvercomeNumerator);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(notOvercomeDenominator);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(onsetFastActingRounds);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(onsetSlowActingTurns);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(antidoteWindowTurns);

        NotOvercomeNumerator = notOvercomeNumerator;
        NotOvercomeDenominator = notOvercomeDenominator;
        OnsetFastActingRounds = onsetFastActingRounds;
        OnsetSlowActingTurns = onsetSlowActingTurns;
        AntidoteWindowTurns = antidoteWindowTurns;
    }

    /// <summary>
    /// Ch 7, "Poison" (p.176): "If the poison does not overcome the character's CON, it has a
    /// lessened effect -- usually only doing half the poison's POT in damage (round up)." Numerator
    /// of the fraction of POT dealt when the poison fails to overcome CON (1 of 2 = half).
    /// </summary>
    public int NotOvercomeNumerator { get; }

    /// <summary>See <see cref="NotOvercomeNumerator"/>. Denominator of the not-overcome fraction.</summary>
    public int NotOvercomeDenominator { get; }

    /// <summary>
    /// Ch 7, "Poison" (p.176): "Unless otherwise specified by the gamemaster, the delay is three
    /// combat rounds for fast-acting poisons." The default onset delay, in combat rounds, for a
    /// fast-acting poison.
    /// </summary>
    public int OnsetFastActingRounds { get; }

    /// <summary>
    /// Ch 7, "Poison" (p.176): "...or three full turns for slower poisons." The default onset delay,
    /// in full turns, for a slower poison.
    /// </summary>
    public int OnsetSlowActingTurns { get; }

    /// <summary>
    /// Ch 7, "Poison Antidotes" (p.176): "If your character takes a poison's antidote no more than
    /// six full turns before being poisoned, the antidote's POT is subtracted from the poison's POT
    /// before damage is figured." The window, in full turns, within which an antidote still counts.
    /// </summary>
    public int AntidoteWindowTurns { get; }
}
