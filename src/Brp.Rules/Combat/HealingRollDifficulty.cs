namespace Brp.Rules.Combat;

/// <summary>
/// Whether a Conditions of Medical Care tier requires a difficulty-graded caregiver roll (Ch 6:
/// Combat, "Conditions of Medical Care", p.157). Only the poor tier gates healing behind a roll, and
/// that roll is Difficult; the decent and excellent tiers heal without a gating roll.
/// </summary>
public enum HealingRollDifficulty
{
    /// <summary>No gating roll is required for healing to occur (the decent and excellent tiers).</summary>
    None,

    /// <summary>The caregiver must succeed a Difficult First Aid or Medicine roll (the poor tier).</summary>
    Difficult,
}
