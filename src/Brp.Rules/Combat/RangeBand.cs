namespace Brp.Rules.Combat;

/// <summary>
/// The named range brackets a missile or firearm attack falls into, per Ch 6: Combat, "Missile
/// Weapons" (p.154) and corroborated by Ch 7: Spot Rules, "Extended Range" (p.171). See
/// <see cref="RangeBandResolver"/> for how a distance, DEX, and a weapon's listed range map to
/// one of these.
/// </summary>
public enum RangeBand
{
    /// <summary>
    /// Within ceil(DEX/3) meters of the target. Easy. Ch 6, "Point Blank" (p.154); Ch 7,
    /// "Extended Range" (p.171): "The rules for point blank range still apply: missile attacks
    /// are Easy at a target less than DEX/3 in meters."
    /// </summary>
    PointBlank,

    /// <summary>
    /// Within the weapon's listed range. Unmodified. Ch 6, "Normal Range" (p.154).
    /// </summary>
    Normal,

    /// <summary>
    /// Beyond the listed range, up to double it. Difficult. Ch 6, "Medium Range" (p.154); Ch 7
    /// (p.171): "At medium range (double the basic range), it becomes Difficult."
    /// </summary>
    Medium,

    /// <summary>
    /// Beyond double the listed range, up to quadruple it -- and, since the book defines no
    /// band past quadruple range, beyond that too. This implementation does not invent an
    /// upper cutoff the source never states (see AGENTS.md invariant 2's warning about the
    /// fabricated "beyond three times range is impossible" rule from an earlier draft). 1/5 of
    /// the base rating. Ch 6, "Long Range" (p.154); Ch 7 (p.171): "at long range (four times
    /// basic range) it becomes 1/5 the normal skill chance (equal to the chance of a special
    /// success, though the result is a normal hit)."
    /// </summary>
    LongRange,
}
