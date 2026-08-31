namespace Brp.Rules.Combat;

/// <summary>
/// The result of resolving one attack against one defense (or its absence) -- everything piece
/// D (damage) needs to compute damage numbers without re-reading the attack/defense matrix
/// itself. See Ch 6: Combat, "Attack and Defense Matrix" (p.147), and
/// <c>docs/decisions/0016-attack-defense-matrix.md</c> for the seam this carries.
/// </summary>
/// <param name="LandedGrade">
/// The effective grade of hit that landed, after the matrix's downgrade (or lack of one).
/// </param>
/// <param name="ArmorTreatment">How the defender's armor value applies, if a hit landed.</param>
/// <param name="ParryWeaponDamage">
/// Present only when the matrix cell specifies weapon damage <em>and</em> the defense used was
/// a Parry (never a Dodge -- see <see cref="Combat.ParryWeaponDamage"/>'s remarks).
/// </param>
/// <param name="DefenderRollsOnFumbleTable">
/// The defender must roll on the appropriate fumble table (Ch 6, p.147; the table itself is
/// piece F -- this is only the flag that one applies).
/// </param>
/// <param name="AttackerRollsOnFumbleTable">
/// The attacker must roll on the appropriate fumble table (Ch 6, p.147; same scope note).
/// </param>
/// <param name="SourceText">
/// The printed (or derived, for the undefended case) result text this outcome came from, kept
/// for citation and debugging -- not itself part of the contract piece D depends on.
/// </param>
public sealed record AttackDefenseOutcome(
    LandedGrade LandedGrade,
    ArmorTreatment ArmorTreatment,
    ParryWeaponDamage? ParryWeaponDamage,
    bool DefenderRollsOnFumbleTable,
    bool AttackerRollsOnFumbleTable,
    string SourceText);
