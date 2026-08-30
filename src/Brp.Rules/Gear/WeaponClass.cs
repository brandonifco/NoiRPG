namespace Brp.Rules.Gear;

/// <summary>
/// The weapon class a <see cref="WeaponDefinition"/> belongs to. Sourced: Ch 8: Equipment,
/// "Weapon Classes" (p.196) -- "a rough group the weapon belongs to ... weapons fall into
/// classes, [skills] into specialties." Only the classes the hand-picked modern noir subset
/// actually uses are represented here (`orc-scope-filter.md`, Ch 8): the book's full list also
/// includes Axe, Bow, Crossbow, Explosive, Flail, Grenade, Hammer, Hand, Mace, Machine Gun,
/// Missile, Pistol/Rifle-Energy, Polearm, Spear, Staff, Sword, and Other, none of which the
/// hand-picked subset needs.
/// <para>
/// Also the ordering key the combat layer's DEX-rank tiebreak reads (missile before melee,
/// long before short) -- reserved for the combat-mechanics issues that consume this data, not
/// implemented here.
/// </para>
/// </summary>
public enum WeaponClass
{
    /// <summary>Unarmed strikes and improvised hand weapons like brass knuckles.</summary>
    Brawl,

    /// <summary>Heavy or light clubs: crowbars, baseball bats, tire irons, truncheons.</summary>
    Club,

    /// <summary>Knives: butcher, pocket, switchblade.</summary>
    Dagger,

    /// <summary>Single-action and semi-automatic handguns.</summary>
    Pistol,

    /// <summary>Revolvers.</summary>
    Revolver,

    /// <summary>Rifles, bolt-action and beyond.</summary>
    Rifle,

    /// <summary>Shotguns.</summary>
    Shotgun,

    /// <summary>Submachine guns.</summary>
    SubmachineGun,
}
