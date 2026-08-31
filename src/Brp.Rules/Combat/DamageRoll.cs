using Brp.Core.Dice;

namespace Brp.Rules.Combat;

/// <summary>
/// The result of rolling damage for one landed (or missed) hit -- <see cref="DamageResolver"/>'s
/// primary output. Carries the full breakdown, not just the final number, so a replay log or a
/// test can show its work. See <c>docs/decisions/0017-damage.md</c>.
/// </summary>
/// <param name="LandedGrade">The grade of hit this damage was rolled for.</param>
/// <param name="WeaponRoll">
/// The weapon dice roll for a Normal or Special hit (Ch 6, p.146-147: special repeats the
/// normal dice, it does not add the weapon's maximum -- see <see cref="DamageResolver"/>'s
/// remarks). <see langword="null"/> for Critical (which uses <see cref="WeaponMaximum"/>
/// instead of rolling) and for Miss (no damage rolled at all).
/// </param>
/// <param name="DamageBonusRoll">
/// The damage bonus (db) roll, present only when the weapon's <c>ApplyDamageBonus</c> flag is
/// set and a damage bonus expression was supplied. Ch 6, p.147 footnote **: "Damage modifier,
/// in all cases, is rolled separately and added afterwards."
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
    DiceRoll? WeaponRoll,
    DiceRoll? DamageBonusRoll,
    int? WeaponMaximum,
    int ArmorApplied,
    int DamageDealt,
    string SourceText);
