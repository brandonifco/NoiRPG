using Brp.Core.Abilities;
using Brp.Core.Modifiers;
using Brp.Core.Randomness;
using Brp.Core.Resolution;

namespace Brp.Rules.Combat;

/// <summary>
/// Resolves Ch 6: Combat, "Bleeding" (p.149)'s ongoing-damage effect -- deferred from #52 (see
/// <c>docs/decisions/0017-damage.md</c>) and built here for #113. Dormant: no shipped weapon
/// (<c>weapon-ruleset.json</c>) currently uses <see cref="Gear.SpecialDamageType.Bleeding"/> (no
/// edged slashing weapon is in the hand-picked gear subset), so this resolver has no live caller
/// yet, but the mechanic is fully modeled per invariant 7.
/// </summary>
public static class BleedingEffectResolver
{
    private static readonly CharacteristicId Constitution = new("CON");

    /// <summary>
    /// One round's ongoing loss, per p.149: "This does 1 additional hit point damage on DEX rank
    /// 1 of each round after the round in which the wound is inflicted. If fatigue points are
    /// used, the target loses 1 additional fatigue point each round they are bleeding." The
    /// caller applies this at DEX rank 1 while the bleeding is active and unstaunched.
    /// </summary>
    public static BleedingRoundLoss RoundLoss(SpecialDamageEffectsRuleset ruleset)
    {
        ArgumentNullException.ThrowIfNull(ruleset);
        return new BleedingRoundLoss(ruleset.BleedingHitPointLossPerRound, ruleset.BleedingFatiguePointLossPerRound);
    }

    /// <summary>
    /// A Stamina roll attempt to staunch the bleeding by hand, per p.149: "At the end of each
    /// round, the target can try a Stamina roll to determine if the bleeding stops. If
    /// successful, the wound is held closed, and the target will not suffer any more bleeding
    /// damage." Modeled as the standard CON roll (the same mapping <see cref="DiseaseResolver"/>
    /// and <see cref="CrushingStunResolver"/> use for the same-named roll).
    /// </summary>
    public static RollOutcome AttemptStaunch(AbilitySet target, IEntropySource entropy)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(entropy);

        var staminaRoll = target.Ruleset.StandardCharacteristicRoll(Constitution);
        return AbilityResolver.Resolve(target, staminaRoll, [], entropy)
            ?? throw new InvalidOperationException("The bleeding staunch roll was unexpectedly gated.");
    }

    /// <summary>
    /// p.149: "While doing so, any attacks, parries, or physical actions they attempt are
    /// Difficult. Attempting to dodge cancels the attempt to stop the bleeding. If unsuccessful,
    /// the bleeding continues, and if the target dodges or does any strenuous activity, the
    /// bleeding begins again." Caller-enforced consequences; this resolver rolls only the Stamina
    /// check itself (<see cref="AttemptStaunch"/>).
    /// </summary>
    public static readonly StaunchingConsequences StaunchConsequences = new(
        OtherActionsAreDifficult: true,
        DodgingCancelsTheAttempt: true,
        StrenuousActivityRestartsBleedingAfterSuccess: true);

    /// <summary>The Difficult modifier the caller adds to any other action attempted while staunching.</summary>
    public static DifficultyModifier StaunchingActionModifier(string source) => DifficultyModifier.Difficult(source);

    /// <summary>
    /// p.149: "If the bleeding is stopped for five combat rounds, it stops entirely on its own."
    /// </summary>
    public static bool StopsPermanently(int consecutiveStaunchedRounds, SpecialDamageEffectsRuleset ruleset)
    {
        ArgumentNullException.ThrowIfNull(ruleset);
        ArgumentOutOfRangeException.ThrowIfNegative(consecutiveStaunchedRounds);
        return consecutiveStaunchedRounds >= ruleset.BleedingStaunchedRoundsUntilPermanentStop;
    }
}

/// <summary>One round's ongoing bleeding loss (Ch 6, "Bleeding", p.149).</summary>
/// <param name="HitPoints">Hit points lost this round.</param>
/// <param name="FatiguePoints">Fatigue points lost this round, if a fatigue-point subsystem is in play.</param>
public readonly record struct BleedingRoundLoss(int HitPoints, int FatiguePoints);

/// <summary>The consequences of attempting to staunch bleeding by hand (Ch 6, "Bleeding", p.149).</summary>
/// <param name="OtherActionsAreDifficult">Attacks, parries, and other physical actions are Difficult while staunching.</param>
/// <param name="DodgingCancelsTheAttempt">Attempting to dodge cancels the staunch attempt.</param>
/// <param name="StrenuousActivityRestartsBleedingAfterSuccess">
/// After a successful staunch, dodging or strenuous activity restarts the bleeding.
/// </param>
public readonly record struct StaunchingConsequences(
    bool OtherActionsAreDifficult, bool DodgingCancelsTheAttempt, bool StrenuousActivityRestartsBleedingAfterSuccess);
