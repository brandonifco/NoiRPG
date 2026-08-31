using Brp.Core.Dice;
using Brp.Rules.Gear;

namespace Brp.Rules.Combat;

/// <summary>
/// The result of rolling damage for one landed (or missed) hit -- <see cref="DamageResolver"/>'s
/// primary output. Carries the full breakdown, not just the final number, so a replay log or a
/// test can show its work. See <c>docs/decisions/0017-damage.md</c>.
/// </summary>
/// <param name="LandedGrade">The grade of hit this damage was rolled for.</param>
/// <param name="SpecialDamageTypeApplied">
/// The weapon's special-success damage type, present only when <paramref name="LandedGrade"/> is
/// <see cref="Combat.LandedGrade.Special"/> -- kept for citation, since it determines which of
/// the type-dependent formulas in <see cref="DamageResolver"/> ran.
/// </param>
/// <param name="WeaponRolls">
/// The weapon dice roll(s). One roll for a Normal hit or a Special hit of a type that does not
/// change the damage number (Bleeding/Entangling/Knockback -- Ch 6, pp.149-151); <em>two</em>
/// independent rolls of the same weapon dice for an Impaling special (Ch 6, p.150: "doubles the
/// dice and modifier" -- summing two independent rolls of the same expression has the identical
/// distribution to doubling its dice count and constant, see <c>docs/decisions/0017-damage.md</c>).
/// Empty for a Critical (which uses <see cref="WeaponMaximum"/> instead) and for a Miss.
/// </param>
/// <param name="DamageBonusRolls">
/// The damage bonus (db) roll(s), present only when the weapon's <c>ApplyDamageBonus</c> flag is
/// set and a damage bonus expression was supplied (or, for a Crushing special with no damage
/// bonus at all, the ruleset's flat substitute -- Ch 6, p.149). Zero entries when no db applies;
/// one entry for a normal db application; two entries for a Crushing special's doubled positive
/// db (rolled twice and summed, same technique as <see cref="WeaponRolls"/>'s doubling).
/// </param>
/// <param name="WeaponMaximum">
/// The weapon's maximum possible damage, used only for a Critical hit. Ch 6, "Critical
/// Success" (p.146): "the maximum possible damage for the weapon used."
/// </param>
/// <param name="ArmorApplied">
/// The armor points actually subtracted (0 for a Critical hit, whose armor treatment is always
/// ignore -- Ch 6, p.146: "a critical attack result always ignores armor").
/// </param>
/// <param name="DamageDealt">The final damage total, floored at zero, after armor.</param>
/// <param name="SourceText">The printed rule this computation followed, for citation.</param>
public sealed record DamageRoll(
    LandedGrade LandedGrade,
    SpecialDamageType? SpecialDamageTypeApplied,
    IReadOnlyList<DiceRoll> WeaponRolls,
    IReadOnlyList<DiceRoll> DamageBonusRolls,
    int? WeaponMaximum,
    int ArmorApplied,
    int DamageDealt,
    string SourceText);
