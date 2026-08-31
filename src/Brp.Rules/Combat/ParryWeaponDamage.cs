namespace Brp.Rules.Combat;

/// <summary>
/// Damage to a weapon used in a parry attempt, per the attack/defense matrix's footnoted cells
/// (Ch 6, p.147, footnote *: "If the parrying weapon or shield is destroyed during the parry
/// attempt..."). Present only when the matched matrix cell states it <em>and</em> the defense
/// used was a <see cref="DefenseType.Parry"/> -- <see cref="AttackDefenseResolver"/> strips this
/// from the outcome for <see cref="DefenseType.Dodge"/>, since a dodge has no weapon to damage.
/// <para>
/// Shields are cut from scope (orc-scope-filter.md: the Shield skill is out). The book's cell
/// text says "parrying weapon or shield" -- this type models only "defending weapon," with no
/// shield concept anywhere in its shape.
/// </para>
/// </summary>
public sealed record ParryWeaponDamage(DamagedParty Party, int Points);
