namespace Brp.Rules.Combat;

/// <summary>
/// The weapon-type tiebreak tier used to order actions within a tied DEX rank, per Ch 6: Combat,
/// "Action" (p.143): "Within a particular DEX rank, attacks usually go in order of weapon type.
/// Attackers armed with missile weapons (bows, guns, etc.) are considered to act before those in
/// hand-to-hand (melee) combat. After these go characters armed with long weapons (spears,
/// lances, etc.), then those with medium-length weapons (swords, axes, etc.) and finally those
/// with short weapons (daggers, etc.) or who are unarmed."
/// <para>
/// <strong>Four tiers, not five.</strong> The passage's "hand-to-hand (melee)" is the umbrella
/// term for the three hand-to-hand tiers that follow it (long, medium, short/unarmed), not a
/// fifth tier standing beside them -- there is no example of a "melee" weapon distinct from a
/// long, medium, or short one anywhere in the weapon tables. Reading "melee" as a separate tier
/// (as an earlier transcription of this ruleset did) double-counts the umbrella as a member of
/// the list it introduces. See <c>docs/decisions/0015-combat-round.md</c>.
/// </para>
/// </summary>
public enum WeaponTypeTier
{
    /// <summary>Bows, guns, and other missile weapons: act first within a tied DEX rank.</summary>
    Missile,

    /// <summary>Long weapons such as spears and lances.</summary>
    LongWeapon,

    /// <summary>Medium-length weapons such as swords and axes.</summary>
    MediumWeapon,

    /// <summary>
    /// Short weapons such as daggers, "or who are unarmed" (p.143) -- the book folds unarmed
    /// combatants into this last tier rather than giving them a separate one.
    /// </summary>
    ShortOrUnarmed,
}
