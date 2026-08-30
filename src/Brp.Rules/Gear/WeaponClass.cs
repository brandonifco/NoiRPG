namespace Brp.Rules.Gear;

/// <summary>
/// The weapon class a <see cref="WeaponDefinition"/> belongs to. Sourced: Ch 8: Equipment,
/// "Weapon Classes" (p.196) -- "a rough group the weapon belongs to ... weapons fall into
/// classes, [skills] into specialties." Only the classes the hand-picked modern noir subset
/// actually uses, plus <see cref="Missile"/> (see below), are represented here
/// (`orc-scope-filter.md`, Ch 8): the book's full list also includes Axe, Bow, Crossbow,
/// Explosive, Flail, Grenade, Hammer, Hand, Mace, Machine Gun, Pistol/Rifle-Energy, Polearm,
/// Spear, Staff, Sword, and Other, none of which the hand-picked subset needs.
/// <para>
/// Also the ordering key the combat layer's DEX-rank tiebreak reads (missile before melee,
/// long before short) -- reserved for the combat-mechanics issues that consume this data, not
/// implemented here.
/// </para>
/// <para>
/// <see cref="Missile"/> is added by #21 even though no weapon in the hand-picked subset uses
/// it yet: Ch 7: Spot Rules, "Extended Range" (p.170) keys the throwing-weapon range cutoff to
/// this class ("Small hand-propelled weapons such as the throwing knife and the throwing axe"),
/// and Ch 8's own "Weapon Classes" list (p.196) files both under "Missile" alongside the blowgun,
/// bola, boomerang, dagger, dart, hand axe, javelin, shuriken, and sling. The rule is implemented
/// against the class now so it is correct the day a thrown weapon is added to the data.
/// </para>
/// </summary>
public enum WeaponClass
{
    /// <summary>Unarmed strikes and improvised hand weapons like brass knuckles.</summary>
    Brawl,

    /// <summary>
    /// Small hand-thrown weapons: throwing knife, throwing axe, javelin, shuriken, and similar
    /// (Ch 8: Equipment, "Weapon Classes", p.196). Not used by any weapon in the hand-picked
    /// subset yet -- see the type remarks.
    /// </summary>
    Missile,

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
