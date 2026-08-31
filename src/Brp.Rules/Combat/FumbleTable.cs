namespace Brp.Rules.Combat;

/// <summary>
/// Which of the four printed D100 fumble consequence tables applies, per Ch 6: Combat (pp.148-149).
/// The book prints <strong>four</strong> tables -- not one -- selected by the fumbling combatant's
/// weapon nature and action; this enum is their identity, the key the ruleset data
/// (<see cref="FumbleRuleset"/>) files each table under. <see cref="FumbleResolver.SelectTable"/>
/// maps the existing combat context (<see cref="Gear.WeaponClass"/> + <see cref="DefenseType"/>)
/// onto one of these. See <c>docs/decisions/0020-fumble-tables.md</c>.
/// </summary>
public enum FumbleTable
{
    /// <summary>Ch 6, "Melee Weapon Attack Fumbles" table (p.148): a fumbled melee attack.</summary>
    MeleeAttack,

    /// <summary>Ch 6, "Melee Weapon Parry Fumbles" table (p.148): a fumbled melee parry.</summary>
    MeleeParry,

    /// <summary>Ch 6, "Missile Weapon Attack Fumbles" table (p.148): a fumbled missile attack.</summary>
    MissileAttack,

    /// <summary>
    /// Ch 6, "Natural Weapon Attack and Parry Fumbles" table (p.149): a fumbled unarmed/natural
    /// attack <em>or</em> parry -- the book uses one combined table for both actions.
    /// </summary>
    Natural,
}
