namespace Brp.Rules.Combat;

/// <summary>
/// The kind of consequence a single fumble-table row inflicts, per the four D100 fumble tables of
/// Ch 6: Combat (pp.148-149). This categorizes each printed row so a caller can dispatch on it
/// without parsing the row's prose; the row also carries the exact printed effect text (for
/// citation) and any structured quantities (dice, magnitudes, hit grade, fallback). Every effect
/// that touches another subsystem -- dropping or throwing a weapon, weapon hit-point loss, a
/// movement penalty, hitting an ally or oneself -- is returned as one of these named outcomes for a
/// caller to apply; this layer applies none of it (no encounter model here, the same caller seam as
/// #50/#96). See <c>docs/decisions/0020-fumble-tables.md</c>.
/// </summary>
public enum FumbleEffectKind
{
    /// <summary>
    /// Lose the next combat round (or this one if no action has been taken yet), often "effectively
    /// helpless" -- and, on the missile table, phrased as losing the next attack or activity. The
    /// exact wording is in the row's effect text.
    /// </summary>
    LoseNextCombatRound,

    /// <summary>Lose the next several combat rounds; the count is the row's rolled quantity (1D3).</summary>
    LoseMultipleCombatRounds,

    /// <summary>Fall prone.</summary>
    FallProne,

    /// <summary>
    /// Fall prone and twist an ankle: a movement penalty (the row's magnitude, -1 MOV) for the row's
    /// rolled duration (1D10 full turns). Natural-weapon table only (p.149).
    /// </summary>
    FallProneAndTwistAnkle,

    /// <summary>Drop the weapon being used, where it falls.</summary>
    DropWeapon,

    /// <summary>
    /// Drop the weapon and have it slide or bounce away by the row's rolled distance (1D6-1 meters).
    /// Missile-attack table only (p.148).
    /// </summary>
    DropWeaponAndScatter,

    /// <summary>Throw the weapon away by the row's rolled distance (1D10 meters).</summary>
    ThrowWeapon,

    /// <summary>Lose weapon hit points equal to the row's rolled quantity (1D10 or 1D6).</summary>
    LoseWeaponHitPoints,

    /// <summary>Break the weapon regardless of its current hit points.</summary>
    BreakWeapon,

    /// <summary>
    /// Vision obscured: a skill penalty (the row's magnitude, -30%) to all appropriate skills for the
    /// row's rolled duration (1D3 combat rounds).
    /// </summary>
    VisionObscured,

    /// <summary>
    /// Miss and strain something: lose hit points equal to the row's magnitude (1 HP), in the
    /// attacking limb if hit locations are used. Natural-weapon table only (p.149).
    /// </summary>
    StrainSelf,

    /// <summary>
    /// Hit a hard surface and take the row's hit grade (normal) damage to oneself, in the attacking
    /// limb if hit locations are used. Natural-weapon table only (p.149).
    /// </summary>
    HitHardSurface,

    /// <summary>
    /// Hit the nearest ally for the row's hit grade (normal/special/critical) of damage, or -- if no
    /// ally is nearby -- use the printed fallback result (the row's <see cref="FumbleConsequenceRow.Fallback"/>).
    /// Whether an ally is in range is the <see cref="Core.Contests.FumbleDecisionId.AllyInRange"/>
    /// call. No damage is applied here.
    /// </summary>
    HitNearestAlly,

    /// <summary>
    /// Left wide open: the foe automatically hits with the row's hit grade
    /// (normal/special/critical). Melee-parry table only (p.148).
    /// </summary>
    FoeAutomaticHit,

    /// <summary>
    /// Blow it: roll on the same table again the row's <see cref="FumbleConsequenceRow.RerollCount"/>
    /// times more (two for "blow it," three for "blow it badly"), cumulatively -- a reroll landing
    /// here again adds further rolls. Not itself a consequence to apply.
    /// </summary>
    Reroll,
}
