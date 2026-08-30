namespace Brp.Rules.Gear;

/// <summary>
/// The result of resolving an <see cref="Characters.EquipmentItem"/>'s name against the gear
/// registry. Both properties are <see langword="null"/> for plain gear that carries no combat
/// stats (a flashlight, a set of lockpicks) -- this is the expected, non-error outcome, not a
/// failure case.
/// </summary>
/// <param name="Weapon">The matching weapon definition, if the item's name is a defined weapon.</param>
/// <param name="Armor">The matching armor definition, if the item's name is a defined armor type.</param>
public sealed record GearLookup(WeaponDefinition? Weapon, ArmorDefinition? Armor)
{
    /// <summary>True when the item resolved to either a weapon or an armor definition.</summary>
    public bool HasDefinition => Weapon is not null || Armor is not null;
}
