namespace Brp.Rules.Combat;

/// <summary>
/// The degree of illness a diseased character suffers, one row of Ch 7: Spot Rules, "Illness
/// Severity Table" (p.170), cross-indexed by the number of failed CON recovery rolls.
/// </summary>
public enum IllnessDegree
{
    /// <summary>0 failures: "None" -- no characteristic loss (p.170).</summary>
    None,

    /// <summary>1 failure: "Mild: lose 1 characteristic point per week" (p.170).</summary>
    Mild,

    /// <summary>2 failures: "Acute: lose 1 characteristic point per day" (p.170).</summary>
    Acute,

    /// <summary>3 failures: "Severe: lose 1 characteristic point per hour" (p.170).</summary>
    Severe,

    /// <summary>4+ failures: "Terminal: lose 1 characteristic point per minute" (p.170).</summary>
    Terminal,
}

/// <summary>
/// The period over which a diseased character loses one characteristic point, the "per week / per
/// day / per hour / per minute" clause of each Ch 7 "Illness Severity Table" row (p.170). Carried
/// alongside <see cref="IllnessDegree"/> so the printed table is reproduced in full rather than
/// collapsed to the degree name alone.
/// </summary>
public enum IllnessLossPeriod
{
    /// <summary>No ongoing loss (the "None" degree).</summary>
    None,

    /// <summary>One characteristic point per week (the "Mild" degree).</summary>
    Week,

    /// <summary>One characteristic point per day (the "Acute" degree).</summary>
    Day,

    /// <summary>One characteristic point per hour (the "Severe" degree).</summary>
    Hour,

    /// <summary>One characteristic point per minute (the "Terminal" degree).</summary>
    Minute,
}
