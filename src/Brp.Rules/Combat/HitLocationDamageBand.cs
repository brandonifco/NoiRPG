namespace Brp.Rules.Combat;

/// <summary>
/// Which of Ch 6: Combat's three printed damage-vs-location-hit-points thresholds a single blow's
/// raw (uncapped) damage reaches, per "Damage and hit Locations (Option)" (pp.156-157). The
/// per-location narrative effects each band triggers (a leg going prone, an arm dropping whatever it
/// held, bleeding rates, Stamina rolls to stay conscious, instant death for head/chest/abdomen at
/// triple) are the book's own text at the cited pages and are not modeled here -- this band is the
/// mechanical classification a caller's narrative/turn-economy layer would key off of, the same
/// deferral <c>docs/decisions/0021-major-wounds.md</c> made for the Major Wounds Table's flavor text.
/// </summary>
public enum HitLocationDamageBand
{
    /// <summary>Below the location's hit points: an ordinary hit, no location-specific effect.</summary>
    Unaffected,

    /// <summary>
    /// Ch 6, "Damage Equal to or More Than the Location's Hit Points" (p.156): the location is
    /// disabled (a leg or arm useless, the character falls or drops what they held; head, chest, or
    /// abdomen have their own printed effect).
    /// </summary>
    EqualOrExceedsLocationHitPoints,

    /// <summary>
    /// Ch 6, "Damage Equals or Exceeds Double the Location's Hit Points" (p.157) -- this band's own
    /// boundary is <strong>&gt;=2x</strong>, matching that heading exactly, and is what a limb hit
    /// (the incapacitating/bleeding effect) keys off of: "cannot take more than twice the possible
    /// points of damage in an arm or leg from a single blow" is itself a cap set at exactly twice,
    /// not a strict-greater-than trigger.
    /// <para>
    /// <strong>Caution for a head/chest/abdomen consequence, at a stricter threshold than this
    /// band.</strong> That location's own worded trigger is "more than twice" (p.157) -- i.e.
    /// <strong>&gt;2x</strong>, not &gt;=2x. A caller wiring the head/chest/abdomen
    /// unconscious-and-bleeding effect must not fire it at a blow reaching exactly this band; it
    /// needs a value strictly greater than 2x the location's hit points, one step past where this
    /// band itself starts. This band only identifies which printed section governs a blow -- it
    /// does not encode that section's per-location strict/non-strict comparison, which differs
    /// between the limb and head/chest/abdomen bullets of the same heading.
    /// </para>
    /// </summary>
    EqualOrExceedsDoubleLocationHitPoints,

    /// <summary>
    /// Ch 6, "Damage Equals or Exceeds Triple the Location's Hit Points" (p.157): a limb is severed
    /// or maimed; a hit to the head, chest, or abdomen is instantly fatal.
    /// </summary>
    EqualOrExceedsTripleLocationHitPoints,
}
