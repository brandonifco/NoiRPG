using Brp.Core.Abilities;
using Brp.Core.Dice;
using Brp.Core.Modifiers;
using Brp.Core.Randomness;
using Brp.Core.Resolution;

namespace Brp.Rules.Combat;

/// <summary>
/// Resolves Ch 6: Combat, "Crushing" (pp.149-150)'s stunning effect -- deferred from #52's
/// damage-number piece (see <c>docs/decisions/0017-damage.md</c>) and built here for #113. A
/// target hit by a Crushing special success (clubs, brass knuckles -- <see cref="Gear.SpecialDamageType.Crushing"/>,
/// a live trigger on every shipped Crushing weapon) must make a successful Stamina roll or be
/// stunned for the ruleset's duration (1D3 rounds).
/// <para>
/// <strong>The Stamina roll is the standard CON roll</strong> (CON x the ruleset's standard
/// characteristic-roll multiplier), the same modeling <see cref="DiseaseResolver.RollContraction"/>
/// uses for the same-named roll (Ch 2, "Characteristic Rolls", pp.11-12; the book does not restate
/// Stamina's rating in this section, so this is the identical mapping, not a fresh interpretation).
/// </para>
/// </summary>
public static class CrushingStunResolver
{
    private static readonly CharacteristicId Constitution = new("CON");
    private static readonly CharacteristicId Intelligence = new("INT");
    private static readonly CharacteristicId Dexterity = new("DEX");

    /// <summary>
    /// Ch 6, p.149: "A target suffering a crushing special success must also make a successful
    /// Stamina roll or be stunned for 1D3 rounds."
    /// </summary>
    public static CrushingStunOutcome ResolveStun(
        AbilitySet target, SpecialDamageEffectsRuleset ruleset, IEntropySource entropy)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(ruleset);
        ArgumentNullException.ThrowIfNull(entropy);

        var staminaRoll = target.Ruleset.StandardCharacteristicRoll(Constitution);
        var outcome = AbilityResolver.Resolve(target, staminaRoll, [], entropy)
            ?? throw new InvalidOperationException("The Stamina stun roll was unexpectedly gated.");

        if (outcome.Succeeded)
        {
            return new CrushingStunOutcome(outcome, Stunned: false, DurationRounds: null, DurationRoll: null);
        }

        var durationRoll = ruleset.CrushingStunDuration.Roll(entropy);
        return new CrushingStunOutcome(outcome, Stunned: true, durationRoll.Total, durationRoll);
    }

    /// <summary>
    /// Ch 6, pp.149-150's printed penalties on a stunned target: "cannot attack while stunned and
    /// can only attempt to dodge or parry an attack if they make a successful Idea roll for each
    /// attempt. Furthermore, all attacks against the target are Easy. The stunned target can
    /// attempt to flee, but to do so requires a successful Idea roll to discern an escape path and
    /// a successful Agility roll to get out of danger." Exposed as data a caller reads and applies
    /// -- this layer does not own a round loop to enforce it against.
    /// </summary>
    public static readonly StunnedTargetRules Penalties = new(
        CannotAttack: true,
        DefenseRequiresSuccessfulIdeaRoll: true,
        AttacksAgainstTargetAreEasy: true,
        FleeingRequiresIdeaThenAgilityRoll: true);

    /// <summary>
    /// The Easy-attack modifier Ch 6, p.150 grants any attacker targeting a stunned character --
    /// the caller adds this to the attacker's own modifier list before rolling the attack.
    /// </summary>
    public static DifficultyModifier AttackAgainstStunnedTargetModifier(string source) =>
        DifficultyModifier.Easy(source);

    /// <summary>The Idea roll (INT roll) a stunned target needs for each dodge/parry attempt, or to flee.</summary>
    public static CharacteristicRoll IdeaRollFor(AbilitySet target)
    {
        ArgumentNullException.ThrowIfNull(target);
        return target.Ruleset.StandardCharacteristicRoll(Intelligence);
    }

    /// <summary>The Agility roll (DEX roll) a stunned target needs to get out of danger while fleeing.</summary>
    public static CharacteristicRoll AgilityRollFor(AbilitySet target)
    {
        ArgumentNullException.ThrowIfNull(target);
        return target.Ruleset.StandardCharacteristicRoll(Dexterity);
    }
}

/// <summary>The result of a Crushing special success's stun check (Ch 6, pp.149-150).</summary>
/// <param name="StaminaRoll">The resolved Stamina (CON) roll.</param>
/// <param name="Stunned">Whether the target is stunned (the roll failed).</param>
/// <param name="DurationRounds">The stun duration in rounds, or <see langword="null"/> if not stunned.</param>
/// <param name="DurationRoll">The 1D3 duration roll, or <see langword="null"/> if not stunned.</param>
public sealed record CrushingStunOutcome(
    RollOutcome StaminaRoll, bool Stunned, int? DurationRounds, DiceRoll? DurationRoll);

/// <summary>
/// The printed penalties a stunned target suffers (Ch 6, pp.149-150), as a caller-read data seam.
/// </summary>
/// <param name="CannotAttack">The stunned target cannot attack.</param>
/// <param name="DefenseRequiresSuccessfulIdeaRoll">
/// Dodge/parry attempts each require a preceding successful Idea roll.
/// </param>
/// <param name="AttacksAgainstTargetAreEasy">All attacks against the stunned target are Easy.</param>
/// <param name="FleeingRequiresIdeaThenAgilityRoll">
/// Fleeing requires a successful Idea roll (find the path), then a successful Agility roll (get away).
/// </param>
public readonly record struct StunnedTargetRules(
    bool CannotAttack,
    bool DefenseRequiresSuccessfulIdeaRoll,
    bool AttacksAgainstTargetAreEasy,
    bool FleeingRequiresIdeaThenAgilityRoll);
