namespace Brp.Rules.Combat;

/// <summary>
/// The effective grade of hit that lands on the defender once the attack/defense matrix
/// (Ch 6: Combat, "Attack and Defense Matrix", p.147) has cross-referenced the attacker's and
/// defender's success grades. A defender's successful parry or dodge can downgrade an
/// attacker's Critical roll all the way down to a Miss, or only partway to a Normal or
/// Special hit -- this is the grade piece D (damage) needs, not the raw attacker roll grade.
/// </summary>
public enum LandedGrade
{
    /// <summary>No damage lands -- the attack was parried, dodged, or simply failed or fumbled.</summary>
    Miss,

    /// <summary>An ordinary hit: normal damage, no special result. Ch 6, "Success" (p.146).</summary>
    Normal,

    /// <summary>A special hit: full damage plus a special result. Ch 6, "Special Success" (p.146).</summary>
    Special,

    /// <summary>A critical hit: full damage. Ch 6, "Critical Success" (p.146).</summary>
    Critical,
}
