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

    /// <summary>
    /// The third printed bleeding-stop path, per p.149: "The most reliable way to stop bleeding
    /// damage is to make a successful First Aid roll on the injury. Success means that the
    /// bleeding stops and will not begin anew. Failure for this First Aid roll means that the
    /// bleeding continues until the target receives successful medical attention (in the form of
    /// a power or another skill like Medicine) or dies from blood loss when they reach 0 hit
    /// points." ("A power" is out of scope, per <c>orc-scope-filter.md</c> -- no magic/powers in
    /// this engine; the in-scope medical-attention route is Medicine.)
    /// <para>
    /// This is a <em>stronger</em> stop than a successful Stamina staunch
    /// (<see cref="AttemptStaunch"/>): a staunch can be undone by dodging or strenuous activity
    /// (<see cref="StaunchConsequences"/>), but a successful First Aid roll stops the bleeding
    /// permanently -- it "will not begin anew."
    /// </para>
    /// <para>
    /// Takes an already-resolved First Aid roll rather than rolling one itself:
    /// <see cref="HealingResolver.ResolveFirstAid"/> already models the complete First Aid skill
    /// roll (support bonuses, hazardous-conditions Difficult grade), and this engine keeps exactly
    /// one First Aid roll implementation rather than a second, narrower copy. This method only
    /// interprets that roll's grade as this specific bleeding wound's stop/continue outcome.
    /// </para>
    /// </summary>
    /// <param name="firstAidRoll">
    /// The already-resolved First Aid roll made against this bleeding wound (e.g. from
    /// <see cref="HealingResolver.ResolveFirstAid"/>'s <c>Roll</c>).
    /// </param>
    public static BleedingFirstAidOutcome ApplyFirstAid(RollOutcome firstAidRoll)
    {
        ArgumentNullException.ThrowIfNull(firstAidRoll);

        if (firstAidRoll.Succeeded)
        {
            return new BleedingFirstAidOutcome(
                firstAidRoll, StoppedPermanently: true, ContinuesUntilMedicalAttentionOrDeath: false);
        }

        return new BleedingFirstAidOutcome(
            firstAidRoll, StoppedPermanently: false, ContinuesUntilMedicalAttentionOrDeath: true);
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

/// <summary>
/// The result of applying an already-resolved First Aid roll to a bleeding wound (Ch 6,
/// "Bleeding", p.149) -- the third of the three printed bleeding-stop paths (alongside a
/// successful Stamina staunch and the five-consecutive-staunched-rounds auto-stop).
/// </summary>
/// <param name="FirstAidRoll">The already-resolved First Aid roll this outcome was derived from.</param>
/// <param name="StoppedPermanently">
/// Whether the bleeding stops permanently and "will not begin anew" (a successful First Aid
/// roll) -- stronger than a successful Stamina staunch, which strenuous activity can restart.
/// </param>
/// <param name="ContinuesUntilMedicalAttentionOrDeath">
/// Whether the bleeding continues (a failed First Aid roll) "until the target receives
/// successful medical attention (... another skill like Medicine) or dies from blood loss when
/// they reach 0 hit points."
/// </param>
public sealed record BleedingFirstAidOutcome(
    RollOutcome FirstAidRoll, bool StoppedPermanently, bool ContinuesUntilMedicalAttentionOrDeath);
