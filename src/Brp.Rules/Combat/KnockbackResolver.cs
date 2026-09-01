using Brp.Core.Abilities;
using Brp.Core.Contests;
using Brp.Core.Dice;
using Brp.Core.Primitives;
using Brp.Core.Randomness;
using Brp.Core.Resolution;

namespace Brp.Rules.Combat;

/// <summary>
/// Resolves Ch 6: Combat, "Knockback" (p.151)'s effect -- deferred from #52 (see
/// <c>docs/decisions/0017-damage.md</c>) and built here for #113. Dormant: no shipped weapon
/// (<c>weapon-ruleset.json</c>) currently uses <see cref="Gear.SpecialDamageType.Knockback"/>, so
/// this resolver has no live caller yet, but the mechanic is fully modeled per invariant 7.
/// </summary>
public static class KnockbackResolver
{
    private static readonly CharacteristicId Dexterity = new("DEX");

    /// <summary>
    /// Ch 6, p.151: "The total damage rolled (before armor is subtracted) is pitted against the
    /// target's SIZ in a resistance roll." On a target win: "they are not moved, and if their SIZ
    /// is higher than the attacker's SIZ, the attacker staggers back one meter." On a target loss:
    /// "knocked back one meter for every 5 points of damage rolled (before armor)" and "must also
    /// make a successful Agility roll or fall prone."
    /// </summary>
    /// <param name="totalDamageRolled">The total damage rolled before armor (the resistance roll's active value).</param>
    /// <param name="attackerSiz">The attacker's SIZ.</param>
    /// <param name="effectiveTargetSiz">
    /// The target's SIZ, already adjusted by the gamemaster for "unevenness of ground, slope, or
    /// the target's condition (fatigued, stunned, etc.)" if applicable (p.151) -- that judgment
    /// call is the caller's, not this resolver's.
    /// </param>
    /// <param name="target">The knocked-back character, whose DEX supplies the prone-check Agility roll.</param>
    /// <param name="ruleset">Supplies the meters-per-damage-point figure.</param>
    /// <param name="entropy">The entropy source, per AGENTS.md invariant 5.</param>
    public static KnockbackOutcome Resolve(
        int totalDamageRolled,
        int attackerSiz,
        int effectiveTargetSiz,
        AbilitySet target,
        SpecialDamageEffectsRuleset ruleset,
        IEntropySource entropy)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(ruleset);
        ArgumentNullException.ThrowIfNull(entropy);

        // Active = the damage rolled, passive = the target's SIZ: Ch 5, "Resistance Rolls"
        // (p.129), "active" is "the party or force trying to influence the passive factor" -- the
        // knockback FORCE is what is trying to move the target, resisted by the target's SIZ. A
        // higher damage total (relative to SIZ) must raise the chance of a successful knockback,
        // which only happens with this active/passive assignment (chance rises with
        // damage - SIZ). The printed prose's "if unsuccessful, knocked back" / "if the target
        // wins, not moved" phrasing narrates the identical roll from the target's point of view
        // (their resistance "succeeding" means the knockback force failed to move them) rather
        // than restating which side is mechanically active; <see cref="ResistanceOutcome.Succeeded"/>
        // here means the damage overcame the target's SIZ resistance, i.e. a successful knockback.
        var resistance = ResistanceResolver.Resolve(totalDamageRolled, effectiveTargetSiz, entropy);

        if (!resistance.Succeeded)
        {
            // "If the target wins the resistance roll, they are not moved, and if their SIZ is
            // higher than the attacker's SIZ, the attacker staggers back one meter."
            var staggerMeters = effectiveTargetSiz > attackerSiz ? 1 : 0;
            return new KnockbackOutcome(
                resistance, KnockedBack: false, DistanceMeters: 0, AttackerStaggerMeters: staggerMeters,
                ProneCheck: null, KnockedProne: false);
        }

        // "one meter for every 5 points of damage rolled" -- unlike the obstacle rule below, the
        // book states no "or fraction thereof" here, so this is a straight floor division.
        var distance = totalDamageRolled / ruleset.KnockbackMetersPerDamagePoint;

        var agilityRoll = target.Ruleset.StandardCharacteristicRoll(Dexterity);
        var proneCheck = AbilityResolver.Resolve(target, agilityRoll, [], entropy)
            ?? throw new InvalidOperationException("The knockback Agility roll was unexpectedly gated.");

        return new KnockbackOutcome(
            resistance, KnockedBack: true, distance, AttackerStaggerMeters: 0, proneCheck, KnockedProne: !proneCheck.Succeeded);
    }

    /// <summary>
    /// Ch 6, p.151: "If the knockback target also hits an obstacle in their path, they take 1D6
    /// damage for every three meters or fraction thereof they have left to travel." One roll of
    /// the ruleset's obstacle-damage dice per full-or-partial increment remaining, summed.
    /// </summary>
    /// <param name="metersRemainingAfterObstacle">
    /// How much of the knockback distance was still left to travel when the obstacle was struck.
    /// </param>
    /// <param name="ruleset">Supplies the per-increment damage dice and the increment length.</param>
    /// <param name="entropy">The entropy source, per AGENTS.md invariant 5.</param>
    public static ObstacleImpactOutcome RollObstacleDamage(
        int metersRemainingAfterObstacle, SpecialDamageEffectsRuleset ruleset, IEntropySource entropy)
    {
        ArgumentNullException.ThrowIfNull(ruleset);
        ArgumentNullException.ThrowIfNull(entropy);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(metersRemainingAfterObstacle);

        // "or fraction thereof" -- explicit round-up, unlike the base knockback-distance formula above.
        var increments = Rounding.Divide(
            metersRemainingAfterObstacle, ruleset.KnockbackObstacleIncrementMeters, RoundingMode.Up);

        var rolls = new List<DiceRoll>();
        var total = 0;
        for (var i = 0; i < increments; i++)
        {
            var roll = ruleset.KnockbackObstacleDamagePerIncrement.Roll(entropy);
            rolls.Add(roll);
            total += roll.Total;
        }

        return new ObstacleImpactOutcome(increments, rolls, total);
    }
}

/// <summary>The result of a Knockback special success's resistance roll (Ch 6, p.151).</summary>
/// <param name="Resistance">The damage-rolled-vs-SIZ resistance roll.</param>
/// <param name="KnockedBack">Whether the target was moved (the resistance roll was lost).</param>
/// <param name="DistanceMeters">The distance knocked back, in meters (0 if not knocked back).</param>
/// <param name="AttackerStaggerMeters">
/// The distance the attacker staggers back (1 meter, only when the target won the resistance roll
/// with a higher SIZ than the attacker's).
/// </param>
/// <param name="ProneCheck">The Agility roll made to avoid falling prone, or <see langword="null"/> if not knocked back.</param>
/// <param name="KnockedProne">Whether the target fell prone (the Agility roll failed).</param>
public sealed record KnockbackOutcome(
    ResistanceOutcome Resistance,
    bool KnockedBack,
    int DistanceMeters,
    int AttackerStaggerMeters,
    RollOutcome? ProneCheck,
    bool KnockedProne);

/// <summary>The result of rolling obstacle-impact damage during a knockback (Ch 6, p.151).</summary>
/// <param name="Increments">The number of three-meter-or-fraction increments rolled for.</param>
/// <param name="Rolls">Each increment's individual damage roll.</param>
/// <param name="TotalDamage">The summed obstacle damage.</param>
public sealed record ObstacleImpactOutcome(int Increments, IReadOnlyList<DiceRoll> Rolls, int TotalDamage);
