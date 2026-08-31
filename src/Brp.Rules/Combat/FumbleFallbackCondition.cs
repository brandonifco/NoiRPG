namespace Brp.Rules.Combat;

/// <summary>
/// The caller-known condition that selects between a fumble row's primary effect and its printed
/// "use result NN-NN" fallback, per Ch 6: Combat (pp.148-149). Both conditions are facts this
/// combat-rules layer does not model (who is standing where; whether a weapon has hit points), so a
/// row carrying a <see cref="FumbleFallback"/> names its condition and the resolver reports both
/// branches. See <c>docs/decisions/0020-fumble-tables.md</c>.
/// </summary>
public enum FumbleFallbackCondition
{
    /// <summary>
    /// The "hit nearest ally ... or use result NN-NN if no ally nearby" rows. Resolved through the
    /// <see cref="Core.Contests.FumbleDecisionId.AllyInRange"/> adjudicator port: an ally in range
    /// selects the hit-ally primary; no ally selects the fallback.
    /// </summary>
    NoAllyNearby,

    /// <summary>
    /// The missile "do 1D6 damage to weapon's hit points (or use 81-85 if the weapon has no hit
    /// points)" row (p.148). Whether the weapon has hit points is a weapon-data fact this layer does
    /// not hold (weapon hit points are not modeled here), so the resolver names both branches and
    /// leaves the selection to the caller rather than routing it through an adjudicator.
    /// </summary>
    WeaponHasNoHitPoints,
}
