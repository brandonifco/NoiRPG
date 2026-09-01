using Brp.Core.Abilities;
using Brp.Core.Contests;
using Brp.Core.Randomness;
using Brp.Core.Resolution;

namespace Brp.Rules.Combat;

/// <summary>
/// Resolves Ch 6: Combat, "Entangling" (pp.150-151)'s immobilizing effect -- deferred from #52
/// (see <c>docs/decisions/0017-damage.md</c>) and built here for #113. Dormant: no shipped weapon
/// (<c>weapon-ruleset.json</c>) currently uses <see cref="Gear.SpecialDamageType.Entangling"/> (no
/// net, rope, or similar flexible weapon is in the hand-picked gear subset), so this resolver has
/// no live caller yet, but the mechanic is fully modeled per invariant 7.
/// </summary>
public static class EntanglingEffectResolver
{
    private static readonly CharacteristicId Dexterity = new("DEX");

    /// <summary>
    /// Ch 6, p.150-151: "A successful entangle prevents the target's movement for the rest of the
    /// combat round and into the next combat round."
    /// </summary>
    public static EntanglingImmobilization Immobilize(SpecialDamageEffectsRuleset ruleset)
    {
        ArgumentNullException.ThrowIfNull(ruleset);
        return new EntanglingImmobilization(
            ruleset.EntanglingImmobilizesRemainderOfCurrentRound, ruleset.EntanglingImmobilizedFollowingRounds);
    }

    /// <summary>
    /// Ch 6, p.151: "On the round following a successful entangle attack, the target can attempt
    /// an Agility roll to free themselves."
    /// </summary>
    public static RollOutcome AttemptAgilityEscape(AbilitySet target, IEntropySource entropy)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(entropy);

        var agilityRoll = target.Ruleset.StandardCharacteristicRoll(Dexterity);
        return AbilityResolver.Resolve(target, agilityRoll, [], entropy)
            ?? throw new InvalidOperationException("The entangling Agility escape roll was unexpectedly gated.");
    }

    /// <summary>
    /// Ch 6, p.151: "or make a STR vs. STR resistance roll to attempt to pull the entangling
    /// weapon from the attacker's hand(s)." (The same STR-vs-STR shape resolves the flexible-weapon
    /// variant, "a successful STR vs. STR resistance roll allows an entangling weapon to wrench a
    /// parrying weapon from the target's grasp" -- symmetric roles, same resolver.)
    /// </summary>
    public static ResistanceOutcome AttemptStrengthEscape(int targetStrength, int attackerStrength, IEntropySource entropy)
    {
        ArgumentNullException.ThrowIfNull(entropy);
        return ResistanceResolver.Resolve(targetStrength, attackerStrength, entropy);
    }

    /// <summary>
    /// Ch 6, p.151: "A successful Dodge roll or Wrestle roll negates a successful entangle but can
    /// only be attempted on the next combat round. A critical parry negates a critical entangle,
    /// but an ordinary parry success has no effect." The caller rolls Dodge/Wrestle/Parry through
    /// the ordinary skill kernel with its own rating -- this method only names which counter grade
    /// negates which entangle grade, rather than re-rolling the skill itself.
    /// </summary>
    /// <param name="entangleGrade">The grade of the entangle attack that landed.</param>
    /// <param name="counterGrade">The grade of the target's Dodge, Wrestle, or Parry attempt.</param>
    /// <param name="counterIsParry">
    /// Whether the counter is a Parry (which only negates on a mutual Critical) rather than a
    /// Dodge or Wrestle (which negates on any success or better).
    /// </param>
    public static bool NegatesEntangle(SuccessLevel entangleGrade, SuccessLevel counterGrade, bool counterIsParry)
    {
        if (counterIsParry)
        {
            return entangleGrade == SuccessLevel.Critical && counterGrade == SuccessLevel.Critical;
        }

        return counterGrade is SuccessLevel.Success or SuccessLevel.Special or SuccessLevel.Critical;
    }

    /// <summary>
    /// Ch 6, p.151: the follow-up Grapple effects available to an attacker who retains control of
    /// the entangling weapon on the next round ("immobilize limb, immobilize target, throw target,
    /// knockdown target, disarm target, injure target, and strangle target, as appropriate").
    /// Named here as a structured seam only -- each effect's own mechanics live with the
    /// not-yet-built Grapple skill piece, out of scope for #113.
    /// </summary>
    public static readonly IReadOnlyList<GrappleFollowUpEffect> AllowableFollowUpEffects =
    [
        GrappleFollowUpEffect.ImmobilizeLimb,
        GrappleFollowUpEffect.ImmobilizeTarget,
        GrappleFollowUpEffect.ThrowTarget,
        GrappleFollowUpEffect.KnockdownTarget,
        GrappleFollowUpEffect.DisarmTarget,
        GrappleFollowUpEffect.InjureTarget,
        GrappleFollowUpEffect.StrangleTarget,
    ];
}

/// <summary>An entangled target's immobilization window (Ch 6, "Entangling", pp.150-151).</summary>
/// <param name="RemainderOfCurrentRound">Movement is prevented for the rest of the current round.</param>
/// <param name="FollowingRoundsImmobilized">The number of full following rounds movement remains prevented.</param>
public readonly record struct EntanglingImmobilization(bool RemainderOfCurrentRound, int FollowingRoundsImmobilized);

/// <summary>
/// The named follow-up Grapple effects an entangling attacker may choose from (Ch 6,
/// "Entangling", p.151). Each effect's mechanics are a Grapple-skill concern, not modeled here.
/// </summary>
public enum GrappleFollowUpEffect
{
    /// <summary>Immobilize one of the target's limbs.</summary>
    ImmobilizeLimb,

    /// <summary>Immobilize the target entirely.</summary>
    ImmobilizeTarget,

    /// <summary>Throw the target to the ground.</summary>
    ThrowTarget,

    /// <summary>Knock the target down.</summary>
    KnockdownTarget,

    /// <summary>Disarm the target.</summary>
    DisarmTarget,

    /// <summary>Injure the target.</summary>
    InjureTarget,

    /// <summary>Strangle the target.</summary>
    StrangleTarget,
}
