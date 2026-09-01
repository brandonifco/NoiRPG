using Brp.Core.Contests;
using Brp.Core.Dice;
using Brp.Core.Modifiers;
using Brp.Core.Primitives;
using Brp.Core.Randomness;
using Brp.Core.Resolution;
using Brp.Rules.Gear;

namespace Brp.Rules.Combat;

/// <summary>
/// Resolves Ch 6: Combat, "Impaling" (pp.149-150)'s lodged-weapon effect -- deferred from #52
/// (see <c>docs/decisions/0017-damage.md</c>) and built here for #113. An Impaling special
/// success (firearms, pointed knives -- <see cref="Gear.SpecialDamageType.Impaling"/>, a live
/// trigger on every shipped Impaling weapon) leaves the weapon lodged until extracted, and the
/// target takes extra damage while it remains lodged and they move significantly.
/// </summary>
public static class ImpalingLodgedWeaponResolver
{
    /// <summary>
    /// The attacker's immediate extraction attempt, made the instant the impaling attack lands,
    /// per p.150: "the attacker may immediately attempt a Difficult weapon skill roll with the
    /// impaling weapon. If successful, the attacker can pull the weapon out after the impaling
    /// attack strikes home." Hand weapons only -- p.150 gives thrown weapons a separate
    /// close-and-grab rule this method does not model.
    /// </summary>
    public static RollOutcome AttemptImmediateExtraction(
        Percent attackerWeaponSkillRating, Percent printedBaseChance, IEntropySource entropy)
    {
        ArgumentNullException.ThrowIfNull(entropy);

        var modifiers = new Modifier[]
        {
            DifficultyModifier.Difficult("Ch 6: Combat, \"Impaling\" (p.150): immediate extraction attempt"),
        };
        return ModifierPipeline.Evaluate(attackerWeaponSkillRating, modifiers).Resolve(printedBaseChance, entropy)
            ?? throw new InvalidOperationException("The immediate extraction roll was unexpectedly gated.");
    }

    /// <summary>
    /// The attacker's focused extraction attempt on a later round, per p.150: "the attacker must
    /// focus on pulling the weapon from the wound. This raises the chance of retrieval to the
    /// attacker's full attack chance with the weapon." Rolled here at full (undifficultied) chance;
    /// the consequences named in <see cref="FocusedConsequences"/> ("any attacks against the
    /// attacker are considered Easy, and they obviously cannot parry or dodge while trying to
    /// extract") are a caller-enforced seam, not modeled by this roll.
    /// </summary>
    public static RollOutcome AttemptFocusedExtraction(
        Percent attackerWeaponSkillRating, Percent printedBaseChance, IEntropySource entropy)
    {
        ArgumentNullException.ThrowIfNull(entropy);
        return ModifierPipeline.Evaluate(attackerWeaponSkillRating, []).Resolve(printedBaseChance, entropy)
            ?? throw new InvalidOperationException("The focused extraction roll was unexpectedly gated.");
    }

    /// <summary>p.150: the consequences of attempting a focused extraction, read by the caller.</summary>
    public static readonly FocusedExtractionConsequences FocusedConsequences = new(
        AttacksAgainstAttackerAreEasy: true, AttackerCannotParryOrDodge: true);

    /// <summary>
    /// The target's own attempt to free a lodged weapon, per p.150: "A target impaled with a
    /// weapon and attempting to remove it must make a resistance roll of their STR vs. the amount
    /// of damage dealt thus far by the weapon. Success means that the weapon has been freed and is
    /// in the hands of the target, while failure means that they are unable to free it that combat
    /// round and they take an additional 1D3 hit points of damage ... from the activity."
    /// </summary>
    /// <param name="targetStrength">The impaled target's STR.</param>
    /// <param name="cumulativeDamageDealt">
    /// The total hit points the lodged weapon has dealt so far (the resistance roll's passive value).
    /// </param>
    /// <param name="ruleset">Supplies the failed-extraction extra-damage dice (1D3).</param>
    /// <param name="entropy">The entropy source, per AGENTS.md invariant 5.</param>
    public static ImpalingSelfExtractionOutcome AttemptSelfExtraction(
        int targetStrength, int cumulativeDamageDealt, SpecialDamageEffectsRuleset ruleset, IEntropySource entropy)
    {
        ArgumentNullException.ThrowIfNull(ruleset);
        ArgumentNullException.ThrowIfNull(entropy);

        var resistance = ResistanceResolver.Resolve(targetStrength, cumulativeDamageDealt, entropy);
        if (resistance.Succeeded)
        {
            return new ImpalingSelfExtractionOutcome(resistance, Freed: true, ExtraDamage: 0, ExtraDamageRoll: null);
        }

        var extra = ruleset.ImpalingSelfExtractionFailureExtraDamage.Roll(entropy);
        return new ImpalingSelfExtractionOutcome(resistance, Freed: false, extra.Total, extra);
    }

    /// <summary>
    /// The extra damage a lodged weapon inflicts when the impaled target "moves in any
    /// significant fashion" (p.150): "they take half the weapon's damage roll (roll again,
    /// without the damage modifier or armor protection) again." Rounds the half up -- a marked
    /// house-rule-by-precedent, since the book states no rounding direction for this specific
    /// halving (see <c>special-damage-effects-ruleset.json</c>'s
    /// <c>impaling.movementDamageRoundingNote</c> for the book's own consistent round-up
    /// convention elsewhere that this follows).
    /// </summary>
    public static ImpalingMovementDamage RollMovementDamage(WeaponDefinition weapon, IEntropySource entropy)
    {
        ArgumentNullException.ThrowIfNull(weapon);
        ArgumentNullException.ThrowIfNull(entropy);

        var freshRoll = weapon.Damage.Roll(entropy);
        var half = Rounding.Divide(freshRoll.Total, 2, RoundingMode.Up);
        return new ImpalingMovementDamage(freshRoll, half);
    }
}

/// <summary>The consequences of a focused extraction attempt (Ch 6, "Impaling", p.150).</summary>
/// <param name="AttacksAgainstAttackerAreEasy">Every attack against the extracting attacker is Easy.</param>
/// <param name="AttackerCannotParryOrDodge">The attacker cannot parry or dodge while extracting.</param>
public readonly record struct FocusedExtractionConsequences(
    bool AttacksAgainstAttackerAreEasy, bool AttackerCannotParryOrDodge);

/// <summary>The result of a target's self-extraction attempt (Ch 6, "Impaling", p.150).</summary>
/// <param name="Resistance">The STR-vs-cumulative-damage resistance roll.</param>
/// <param name="Freed">Whether the weapon was freed (the resistance roll succeeded).</param>
/// <param name="ExtraDamage">The extra hit points taken on a failed attempt (0 on success).</param>
/// <param name="ExtraDamageRoll">The 1D3 roll behind <see cref="ExtraDamage"/>, or <see langword="null"/> on success.</param>
public sealed record ImpalingSelfExtractionOutcome(
    ResistanceOutcome Resistance, bool Freed, int ExtraDamage, DiceRoll? ExtraDamageRoll);

/// <summary>The extra movement damage a lodged impaling weapon inflicts (Ch 6, "Impaling", p.150).</summary>
/// <param name="FreshRoll">The fresh weapon-dice roll (no damage bonus, no armor).</param>
/// <param name="HalvedDamage">Half of <see cref="FreshRoll"/>'s total, rounded up.</param>
public sealed record ImpalingMovementDamage(DiceRoll FreshRoll, int HalvedDamage);
