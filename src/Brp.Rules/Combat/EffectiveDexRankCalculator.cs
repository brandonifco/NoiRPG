namespace Brp.Rules.Combat;

/// <summary>
/// Computes a combatant's effective DEX rank for the Action phase, per Ch 6: Combat, "Move"
/// (p.144) and "Attack" (p.144): movement fractions apply first, flat penalties (drawing a
/// weapon combined with another action, or spacing successive attacks) apply after, and any
/// result at or below the DEX-rank floor means the action is lost.
/// <para>
/// This is a pure ordering calculation: it does not decide <em>whether</em> a combatant is
/// drawing a weapon or taking more than one action -- those facts, and in particular what
/// triggers more than one action (a multi-attack weapon, &gt;100% skill), are for a caller (and,
/// ultimately, piece C, the attack/defense resolver) to establish. See
/// <c>docs/decisions/0015-combat-round.md</c>.
/// </para>
/// </summary>
public static class EffectiveDexRankCalculator
{
    /// <summary>
    /// Fractions <paramref name="baseDexRank"/> by the movement tier <paramref name="movementMeters"/>
    /// falls into, per Ch 6, "Move" (p.144). Distances outside every tier (including "no movement,"
    /// and movement of 5 meters or less, which the book folds into an ordinary attack's own "up to
    /// 5 meters" allowance) leave the rank unmodified.
    /// <para>
    /// <strong>House interpretation, governed by a nearby printed convention:</strong> Ch 6 never
    /// states a rounding direction for these two fractions, but Ch 5: System, "Characteristic
    /// Increases" (p.140) gives the book's only explicit rounding rule for the same "half of X"
    /// shape: "Any attempts to train or research an increase to the DEX or CHA characteristics are
    /// limited to half again the original characteristic (round up). For example, ... DEX 13 ...
    /// (1/2 of 13 rounds up to 7 ...)." No BRP passage anywhere rounds a "half of a characteristic"
    /// calculation down. This implementation follows that precedent and rounds the halved/quartered
    /// DEX rank up (ceiling) rather than truncating -- the opposite of an earlier draft, which
    /// truncated toward zero and had the pathological effect of a DEX-rank-1 combatant moving 6-15m
    /// truncating to 0 and losing their action outright, when the sole named consequence in Ch 6 for
    /// a rank of 0 or below is losing an action to <em>penalties</em> (p.144), not to a mid-range
    /// walk. See <c>docs/decisions/0015-combat-round.md</c>.
    /// </para>
    /// </summary>
    public static int ApplyMovement(int baseDexRank, int movementMeters, CombatRoundRuleset ruleset)
    {
        ArgumentNullException.ThrowIfNull(ruleset);
        ArgumentOutOfRangeException.ThrowIfNegative(movementMeters);

        foreach (var tier in ruleset.MovementTiers)
        {
            if (movementMeters >= tier.MinMeters && movementMeters <= tier.MaxMeters)
            {
                return CeilDiv(baseDexRank * tier.FractionNumerator, tier.FractionDenominator);
            }
        }

        return baseDexRank;
    }

    /// <summary>
    /// Computes the effective DEX rank a combatant acts on this Action phase: movement fraction
    /// first (Ch 6 p.144: "These modified DEX ranks are cumulative with penalties for additional
    /// actions, with movement modifiers to DEX rank being applied first"), then
    /// <paramref name="flatDexRankPenalty"/> subtracted -- the caller's sum of whichever flat
    /// penalties apply (drawing a weapon combined with another action:
    /// <see cref="CombatRoundRuleset.DrawWeaponDexRankPenalty"/>; the Nth successive attack:
    /// <c>N * CombatRoundRuleset.MultipleActionDexRankPenalty</c>; or both). Returns <c>null</c>
    /// when the result falls at or below <see cref="CombatRoundRuleset.DexRankFloor"/> -- Ch 6,
    /// p.144: "Your character cannot act on DEX rank 0, so any actions that would occur below DEX
    /// rank 1 are lost" -- meaning the action does not occur this round.
    /// </summary>
    public static int? Compute(
        int baseDexRank, int movementMeters, int flatDexRankPenalty, CombatRoundRuleset ruleset)
    {
        ArgumentNullException.ThrowIfNull(ruleset);
        ArgumentOutOfRangeException.ThrowIfNegative(flatDexRankPenalty);

        var afterMovement = ApplyMovement(baseDexRank, movementMeters, ruleset);
        var effective = afterMovement - flatDexRankPenalty;

        return effective > ruleset.DexRankFloor ? effective : null;
    }

    private static int CeilDiv(int value, int divisor) => (value + divisor - 1) / divisor;
}
