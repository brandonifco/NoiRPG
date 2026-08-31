namespace Brp.Rules.Combat;

/// <summary>
/// Models one combat round, per Ch 6: Combat, "Combat Round Phases" (p.142) and "Action" (p.143):
/// the round's phases, and the Action phase's ordered sequence of combatant turns. Deliberately
/// does not resolve any turn -- attacks, parries, and dodges are piece C's concern
/// (<c>AttackDefenseMatrix</c>). See <c>docs/decisions/0015-combat-round.md</c> for the full
/// rationale, including the 3-phase scope decision and the weapon-type tiebreak reading.
/// </summary>
public sealed class CombatRound
{
    private CombatRound(IReadOnlyList<CombatRoundPhase> phases, IReadOnlyList<CombatantTurn> actionPhaseOrder)
    {
        Phases = phases;
        ActionPhaseOrder = actionPhaseOrder;
    }

    /// <summary>The round's phases in order, per <see cref="CombatRoundRuleset.Phases"/>.</summary>
    public IReadOnlyList<CombatRoundPhase> Phases { get; }

    /// <summary>
    /// The Action phase's combatant turns, ordered high-to-low by effective DEX rank (Ch 6, p.142)
    /// with the weapon-type tiebreak applied to ties (Ch 6, p.143). Combatants whose effective DEX
    /// rank fell at or below the floor (Ch 6, p.144) are absent -- their action was lost, not
    /// ordered last.
    /// </summary>
    public IReadOnlyList<CombatantTurn> ActionPhaseOrder { get; }

    /// <summary>
    /// Builds a <see cref="CombatRound"/> from a set of combatants' action requests: computes each
    /// combatant's effective DEX rank (dropping any whose action is lost to the DEX-rank floor),
    /// then orders the rest high-to-low by that rank, breaking ties with the weapon-type tiebreak
    /// order (Ch 6, p.143). Combatants tied on both DEX rank and weapon tier keep their relative
    /// input order -- the book's own fallback beyond weapon type (skill rating, then simultaneity;
    /// p.143) is out of scope for this piece.
    /// </summary>
    public static CombatRound Create(IEnumerable<CombatActionRequest> requests, CombatRoundRuleset ruleset)
    {
        ArgumentNullException.ThrowIfNull(requests);
        ArgumentNullException.ThrowIfNull(ruleset);

        var turns = new List<CombatantTurn>();
        foreach (var request in requests)
        {
            var effectiveRank = EffectiveDexRankCalculator.Compute(
                request.BaseDexRank, request.MovementMeters, request.FlatDexRankPenalty, ruleset);

            if (effectiveRank is int rank)
            {
                turns.Add(new CombatantTurn(request.CombatantId, rank, request.WeaponTier));
            }
        }

        var rankOrdered = ruleset.DexRankOrderedDescending
            ? turns.OrderByDescending(turn => turn.EffectiveDexRank)
            : turns.OrderBy(turn => turn.EffectiveDexRank);

        var ordered = rankOrdered
            .ThenBy(turn => TiebreakIndex(turn.WeaponTier, ruleset))
            .ToList();

        return new CombatRound(ruleset.Phases, ordered);
    }

    private static int TiebreakIndex(WeaponTypeTier tier, CombatRoundRuleset ruleset)
    {
        var order = ruleset.WeaponTypeTiebreakOrder;
        for (var i = 0; i < order.Count; i++)
        {
            if (order[i] == tier)
            {
                return i;
            }
        }

        return order.Count;
    }
}
