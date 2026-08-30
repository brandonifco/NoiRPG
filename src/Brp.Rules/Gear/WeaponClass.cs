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
/// <see cref="Missile"/> is added by #21 even though no weapon in the hand-picked subset uses it
/// yet: Ch 8's "Weapon Classes" list (p.196) files the blowgun, bola, boomerang, dagger, dart,
/// hand axe, javelin, shuriken, sling, and throwing knife all under this one class. <strong>It is
/// deliberately <em>not</em> used to key the Ch 7 (p.171) throwing-weapon range cutoff</strong>
/// ("Small hand-propelled weapons such as the throwing knife and the throwing axe have no chance
/// to hit beyond double base range"): the class mixes hand-thrown weapons with mechanism-launched
/// ones (the sling, the blowgun), and Ch 3 (p.47) treats "entirely self-propelled" missile
/// weapons as their own case for the unrelated damage-modifier halving rule -- evidence the two
/// kinds are not interchangeable. An earlier revision of #21 keyed the cutoff to this whole class
/// and wrongly cut off the sling and blowgun along with the throwing knife and axe; a
/// rules-conformance pass caught it. The cutoff is now a per-weapon fact a caller supplies to
/// <c>Combat.RangeBandResolver.IsBeyondThrowingCutoff</c>, not read from this enum.
/// </para>
/// </summary>
public enum WeaponClass
{
    /// <summary>Unarmed strikes and improvised hand weapons like brass knuckles.</summary>
    Brawl,

    /// <summary>
    /// Hand-propelled and mechanism-launched missile weapons that are neither firearms nor bows:
    /// blowgun, bola, boomerang, dagger (thrown), dart, hand axe, javelin, shuriken, sling,
    /// throwing knife (Ch 8: Equipment, "Weapon Classes", p.196). Not used by any weapon in the
    /// hand-picked subset yet -- see the type remarks, including why this class alone does not
    /// determine the Ch 7 throwing-weapon range cutoff.
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
