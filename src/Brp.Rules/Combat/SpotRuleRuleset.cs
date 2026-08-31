namespace Brp.Rules.Combat;

/// <summary>
/// The data-defined percentage values the situational combat spot rules read (AGENTS.md
/// invariant 7: rules values are data, not constants). Every value is sourced on its own member
/// below. Loaded from <c>spot-rule-ruleset.json</c> by <c>Brp.Data.NoirSpotRuleRuleset.Load()</c>.
/// <para>
/// Carries only the values the book prints as <em>numbers</em>. The spot rules that work purely by
/// difficulty grade -- Ambushes and Backstabs (Easy attacks, Difficult defenses), Cover (Difficult
/// attacks), and firing while engaged (Difficult) -- contribute
/// <see cref="Brp.Core.Modifiers.DifficultyModifier"/>s, whose Easy/Difficult multipliers already
/// live as data on <see cref="Brp.Core.Modifiers.ModifierPolicy"/>; they need no per-rule number
/// here. See <see cref="SpotRuleResolver"/> and <c>docs/decisions/0018-spot-rules.md</c>.
/// </para>
/// </summary>
public sealed class SpotRuleRuleset
{
    /// <summary>Creates a spot-rule ruleset from data-defined values.</summary>
    public SpotRuleRuleset(
        int firingIntoCombatModifier,
        int darknessSemiDarknessModifier,
        int darknessPitchBlackModifier,
        int darknessDetectionHalvingNumerator,
        int darknessDetectionHalvingDenominator)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(
            firingIntoCombatModifier, 0, nameof(firingIntoCombatModifier));
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(
            darknessSemiDarknessModifier, 0, nameof(darknessSemiDarknessModifier));
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(
            darknessPitchBlackModifier, 0, nameof(darknessPitchBlackModifier));

        // Pitch black must be the more severe (more negative) penalty than semi-darkness; the
        // book prints them as distinct Environment tiers (Ch 5, p.133) and darkness-severity keys
        // on the difference.
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(
            darknessPitchBlackModifier, darknessSemiDarknessModifier, nameof(darknessPitchBlackModifier));

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(
            darknessDetectionHalvingNumerator, nameof(darknessDetectionHalvingNumerator));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(
            darknessDetectionHalvingDenominator, nameof(darknessDetectionHalvingDenominator));

        FiringIntoCombatModifier = firingIntoCombatModifier;
        DarknessSemiDarknessModifier = darknessSemiDarknessModifier;
        DarknessPitchBlackModifier = darknessPitchBlackModifier;
        DarknessDetectionHalvingNumerator = darknessDetectionHalvingNumerator;
        DarknessDetectionHalvingDenominator = darknessDetectionHalvingDenominator;
    }

    /// <summary>
    /// Ch 7: Spot Rules, "Firing Into Combat" (p.173): "Firing a missile weapon into combat is
    /// modified by -20%." The flat situational penalty for a shot passing into a melee others are
    /// engaged in -- distinct from firing <em>while engaged</em> oneself, which is a Difficult
    /// grade rather than this additive penalty (see <see cref="SpotRuleResolver.FiringIntoCombat"/>).
    /// Stored as the signed penalty (-20), applied as a situational
    /// <see cref="Brp.Core.Modifiers.AdditiveModifier"/> so its stated weight is not itself halved
    /// by any difficulty grade in play (Ch 5, "Modifying Action Rolls", p.132; ADR 0007).
    /// </summary>
    public int FiringIntoCombatModifier { get; }

    /// <summary>
    /// Ch 7, "Darkness" (p.169) directs the reader to the Ch 5 Situational Modifiers table for the
    /// modifier; the Environment tier "Unpleasant or unsanitary conditions, unsteady footing,
    /// darkness, bad weather, etc." is -20% (Ch 5, p.133). Applied for
    /// <see cref="Brp.Core.Contests.DarknessSeverity.SemiDarkness"/>.
    /// </summary>
    public int DarknessSemiDarknessModifier { get; }

    /// <summary>
    /// Ch 5 Situational Modifiers, Environment tier "Distracting environment, highly unstable
    /// ground, pitch black, stormy, etc." at -50% (p.133), reached from Ch 7, "Darkness" (p.169).
    /// Applied for <see cref="Brp.Core.Contests.DarknessSeverity.PitchBlack"/>.
    /// </summary>
    public int DarknessPitchBlackModifier { get; }

    /// <summary>
    /// Ch 7, "Darkness" (p.169): "To detect an opponent in complete darkness, you must make a
    /// successful Difficult Sense or Listen roll. If successful, reduce the darkness modifier by
    /// half." The fraction the darkness penalty is scaled by once the opponent is detected;
    /// numerator of 1 over <see cref="DarknessDetectionHalvingDenominator"/> of 2 gives "by half."
    /// Kept as data so a campaign could tune the fraction without touching code.
    /// </summary>
    public int DarknessDetectionHalvingNumerator { get; }

    /// <summary>See <see cref="DarknessDetectionHalvingNumerator"/>.</summary>
    public int DarknessDetectionHalvingDenominator { get; }
}
