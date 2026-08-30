namespace Brp.Rules.Gear;

/// <summary>
/// The firearm-only stats a <see cref="WeaponDefinition"/> carries. Sourced: Ch 8: Equipment,
/// Modern Missile Weapons table (p.201-202). <see langword="null"/> on
/// <see cref="WeaponDefinition.Firearm"/> for melee weapons, which have no range, malfunction
/// number, or ammo capacity.
/// </summary>
/// <param name="ListedRange">
/// The printed "Rng" column value, verbatim, e.g. <c>"20"</c> or, for shotguns whose damage
/// falls off by increment (note 6), the full slash-separated string <c>"10/20/50"</c>. This is
/// the value #21's range-band math (Ch 6/7, "Extended Range") multiplies by 1/2/4 for point
/// blank/medium/long -- #21 still has to decide how a banded shotgun range feeds that formula;
/// this field only guarantees the printed value is present and typed.
/// </param>
/// <param name="ListedRangeMeters">
/// <see cref="ListedRange"/> parsed to a single number, when the printed range is a plain
/// number rather than a slash-separated increment list. <see langword="null"/> for the two
/// shotguns in the subset.
/// </param>
/// <param name="MalfunctionNumber">The printed "Mal" column, e.g. <c>"00"</c> or <c>"98–00"</c>.</param>
/// <param name="AmmoCapacity">The printed "SIZ/Enc" ammo count column, e.g. <c>"8"</c> or <c>"1 or 2"</c>.</param>
/// <param name="AttacksPerRound">The printed "Attk" column, e.g. <c>"1"</c>, <c>"0.5"</c>, or <c>"2 or burst"</c>.</param>
/// <param name="BaseChance">
/// The printed "Base" column: Ch 3, "Firearm (various)" (p.39), "Base Chance: As per weapon
/// specialty" -- the value <see cref="Brp.Core.Skills.WeaponDerivedBaseChance"/> could not
/// supply on its own.
/// </param>
/// <param name="ListedRangeWithoutScope">
/// Sniper rifle only (note 5, p.202): the range without a telescopic scope, half of
/// <see cref="ListedRangeMeters"/>. <see langword="null"/> for every other firearm.
/// </param>
/// <param name="BaseChanceWithBipod">
/// Sniper rifle only (note 4, p.202): the base chance with the bipod it is usually equipped
/// with -- equal to <see cref="BaseChance"/>, since the printed 20% already assumes the bipod.
/// <see langword="null"/> for every other firearm.
/// </param>
/// <param name="BaseChanceWithoutBipod">
/// Sniper rifle only (note 4, p.202): the reduced base chance without a bipod or similar
/// stabilizer. <see langword="null"/> for every other firearm.
/// </param>
public sealed record FirearmProfile(
    string ListedRange,
    int? ListedRangeMeters,
    string MalfunctionNumber,
    string AmmoCapacity,
    string AttacksPerRound,
    int BaseChance,
    int? ListedRangeWithoutScope = null,
    int? BaseChanceWithBipod = null,
    int? BaseChanceWithoutBipod = null);
