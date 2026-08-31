namespace Brp.Rules.Combat;

/// <summary>
/// A target's condition after hit points change, per the thresholds in
/// <see cref="DamageRuleset"/>. Ch 2: Characters, "Hit Points" (p.13); Ch 6: Combat, "Fatal
/// Wound" (p.156).
/// </summary>
public enum HitPointCondition
{
    /// <summary>Above the unconscious threshold -- no condition change from hit points alone.</summary>
    Unaffected,

    /// <summary>
    /// At or below <see cref="DamageRuleset.UnconsciousHitPointLevel"/> but above
    /// <see cref="DamageRuleset.DeadHitPointLevel"/>.
    /// </summary>
    Unconscious,

    /// <summary>
    /// At or below <see cref="DamageRuleset.DeadHitPointLevel"/>. Ch 6, p.156: "Your character
    /// is immediately knocked prone but unable to take any action of any type" -- this is the
    /// <em>pending</em> flag, not resolved death. Whether the character actually dies is decided
    /// at the end of the following round by <see cref="DamageResolver.ResolvesToDeath"/>, once
    /// piece E's First Aid window (out of scope here) has had its chance to intervene.
    /// </summary>
    FatallyWounded,
}
