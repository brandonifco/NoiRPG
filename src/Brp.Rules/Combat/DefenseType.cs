namespace Brp.Rules.Combat;

/// <summary>
/// The defensive option a defender uses against an attack, per Ch 6: Combat, "Parry" and
/// "Dodge" (p.144). Defensive options are limited to the in-scope skills -- a melee weapon or
/// Brawl/Grapple/Martial Arts for parry, Dodge for dodge; shields are cut from scope
/// (orc-scope-filter.md) and have no representation here.
/// </summary>
public enum DefenseType
{
    /// <summary>
    /// The defender takes no defensive action at all. Not itself a printed matrix column --
    /// see <see cref="AttackDefenseResolver"/>'s undefended-case handling and
    /// <c>docs/decisions/0016-attack-defense-matrix.md</c>.
    /// </summary>
    None,

    /// <summary>Ch 6, "Parry" (p.144): a parrying weapon, or Brawl/Grapple/Martial Arts, blocks the blow.</summary>
    Parry,

    /// <summary>Ch 6, "Dodge" (p.144): the defender evades the blow entirely.</summary>
    Dodge,
}
