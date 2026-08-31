using Brp.Core.Dice;
using Brp.Core.Skills;

namespace Brp.Rules.Gear;

/// <summary>
/// A weapon's identity and combat stats: the skill that fires it, its damage, weapon class, and
/// -- for firearms -- the range/malfunction/capacity data the combat layer needs. Sourced: Ch 8:
/// Equipment, Modern Melee Weapons (p.201) and Modern Missile Weapons (p.201-202) tables, plus
/// the Primitive Melee Weapons table (p.196) for the two Club entries (see
/// <c>docs/decisions/</c> for why those are included despite the table's name). Hand-picked to
/// the modern noir subset per `orc-scope-filter.md`, Ch 8: "a dozen firearms and three armor
/// types ... not two hundred rows."
/// </summary>
/// <param name="Id">The stable ruleset identifier.</param>
/// <param name="Name">The display name, as printed in the book.</param>
/// <param name="SkillId">
/// The combat skill this weapon is fired or wielded with, keyed to the Layer 2 skill list
/// (<c>skill-ruleset.json</c>). Every value here must already exist in that ruleset -- this
/// issue does not add new skill specialties (see <see cref="Source"/> notes for the one
/// weapon, the submachine gun, where the book's own specialty has no matching entry there).
/// </param>
/// <param name="WeaponClass">The weapon class this weapon belongs to (Ch 8, "Weapon Classes", p.196).</param>
/// <param name="Damage">
/// The base damage dice. For weapons whose damage falls off by range (shotguns), this is the
/// closest-range figure and <see cref="DamageByRange"/> carries the full increment list.
/// </param>
/// <param name="ApplyDamageBonus">
/// Whether the character's damage bonus (db) is added on a hit. Ch 6: Combat, "Damage Bonus":
/// firearms do not add db; most melee weapons do.
/// </param>
/// <param name="DamageByRange">
/// The full range-increment damage list for weapons whose damage falls off by range (note 6,
/// p.201) -- currently only the two shotguns in the subset. Empty for every other weapon.
/// </param>
/// <param name="Firearm">
/// Firearm-only stats (range, malfunction number, ammo capacity/rate, printed base chance).
/// <see langword="null"/> for melee and brawl weapons.
/// </param>
/// <param name="SpecialDamageType">
/// The special-success damage type this weapon inflicts (Ch 6, "Special Successes and Damage",
/// pp.148-149) -- e.g. firearms and pointed knives are <see cref="Gear.SpecialDamageType.Impaling"/>
/// (p.150), clubs and brass knuckles are <see cref="Gear.SpecialDamageType.Crushing"/> (p.149).
/// Read by <c>Brp.Rules.Combat.DamageResolver</c> to compute special-success damage.
/// </param>
/// <param name="Source">The book table (and any note) this entry was transcribed from.</param>
public sealed record WeaponDefinition(
    WeaponId Id,
    string Name,
    SkillId SkillId,
    WeaponClass WeaponClass,
    DiceExpression Damage,
    bool ApplyDamageBonus,
    IReadOnlyList<RangeIncrementDamage> DamageByRange,
    FirearmProfile? Firearm,
    SpecialDamageType SpecialDamageType,
    string Source)
{
    /// <summary>True for weapons with <see cref="Firearm"/> data; false for melee/brawl weapons.</summary>
    public bool IsFirearm => Firearm is not null;
}
