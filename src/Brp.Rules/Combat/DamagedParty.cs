namespace Brp.Rules.Combat;

/// <summary>
/// Which side's weapon is damaged by a parry attempt, per the attack/defense matrix's
/// footnoted cells (Ch 6, p.147, footnote *).
/// </summary>
public enum DamagedParty
{
    /// <summary>The attacker's weapon takes the damage -- the defender's parry beat the attack outright.</summary>
    Attacker,

    /// <summary>The defender's weapon takes the damage -- the attack partially got through the parry.</summary>
    Defender,
}
