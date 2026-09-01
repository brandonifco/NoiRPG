using Brp.Core.Dice;

namespace Brp.Rules.Combat;

/// <summary>
/// The data-defined values the special-damage-effect resolvers read (AGENTS.md invariant 7:
/// rules values are data, not constants): <see cref="CrushingStunResolver"/>,
/// <see cref="ImpalingLodgedWeaponResolver"/>, <see cref="KnockbackResolver"/>,
/// <see cref="BleedingEffectResolver"/>, <see cref="EntanglingEffectResolver"/>, and
/// <see cref="FightingDefensivelyResolver"/>. Loaded from
/// <c>special-damage-effects-ruleset.json</c> by <c>Brp.Data.NoirSpecialDamageEffectsRuleset.Load()</c>.
/// Ch 6: Combat, pp.149-151. These effects were explicitly deferred from #52's damage-number piece
/// (see <c>docs/decisions/0017-damage.md</c>'s "Explicitly out of scope" section) and are built
/// here for #113.
/// </summary>
public sealed class SpecialDamageEffectsRuleset
{
    /// <summary>Creates a special-damage-effects ruleset from data-defined values.</summary>
    public SpecialDamageEffectsRuleset(
        DiceExpression crushingStunDuration,
        DiceExpression impalingSelfExtractionFailureExtraDamage,
        int knockbackMetersPerDamagePoint,
        DiceExpression knockbackObstacleDamagePerIncrement,
        int knockbackObstacleIncrementMeters,
        int bleedingHitPointLossPerRound,
        int bleedingFatiguePointLossPerRound,
        int bleedingStaunchedRoundsUntilPermanentStop,
        bool entanglingImmobilizesRemainderOfCurrentRound,
        int entanglingImmobilizedFollowingRounds,
        int successiveDefensePenaltyPercent)
    {
        ArgumentNullException.ThrowIfNull(crushingStunDuration);
        ArgumentNullException.ThrowIfNull(impalingSelfExtractionFailureExtraDamage);
        ArgumentNullException.ThrowIfNull(knockbackObstacleDamagePerIncrement);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(knockbackMetersPerDamagePoint, 0);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(knockbackObstacleIncrementMeters, 0);
        ArgumentOutOfRangeException.ThrowIfNegative(bleedingHitPointLossPerRound);
        ArgumentOutOfRangeException.ThrowIfNegative(bleedingFatiguePointLossPerRound);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(bleedingStaunchedRoundsUntilPermanentStop, 0);
        ArgumentOutOfRangeException.ThrowIfNegative(entanglingImmobilizedFollowingRounds);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(successiveDefensePenaltyPercent, 0);

        CrushingStunDuration = crushingStunDuration;
        ImpalingSelfExtractionFailureExtraDamage = impalingSelfExtractionFailureExtraDamage;
        KnockbackMetersPerDamagePoint = knockbackMetersPerDamagePoint;
        KnockbackObstacleDamagePerIncrement = knockbackObstacleDamagePerIncrement;
        KnockbackObstacleIncrementMeters = knockbackObstacleIncrementMeters;
        BleedingHitPointLossPerRound = bleedingHitPointLossPerRound;
        BleedingFatiguePointLossPerRound = bleedingFatiguePointLossPerRound;
        BleedingStaunchedRoundsUntilPermanentStop = bleedingStaunchedRoundsUntilPermanentStop;
        EntanglingImmobilizesRemainderOfCurrentRound = entanglingImmobilizesRemainderOfCurrentRound;
        EntanglingImmobilizedFollowingRounds = entanglingImmobilizedFollowingRounds;
        SuccessiveDefensePenaltyPercent = successiveDefensePenaltyPercent;
    }

    /// <summary>Ch 6, "Crushing" (p.149): "be stunned for 1D3 rounds."</summary>
    public DiceExpression CrushingStunDuration { get; }

    /// <summary>
    /// Ch 6, "Impaling" (p.150): a failed self-extraction resistance roll costs "an additional
    /// 1D3 hit points of damage ... from the activity."
    /// </summary>
    public DiceExpression ImpalingSelfExtractionFailureExtraDamage { get; }

    /// <summary>Ch 6, "Knockback" (p.151): "knocked back one meter for every 5 points of damage rolled."</summary>
    public int KnockbackMetersPerDamagePoint { get; }

    /// <summary>Ch 6, "Knockback" (p.151): "1D6 damage for every three meters or fraction thereof."</summary>
    public DiceExpression KnockbackObstacleDamagePerIncrement { get; }

    /// <summary>Ch 6, "Knockback" (p.151): the "three meters" increment the obstacle damage is rolled per.</summary>
    public int KnockbackObstacleIncrementMeters { get; }

    /// <summary>Ch 6, "Bleeding" (p.149): "1 additional hit point damage on DEX rank 1 of each round."</summary>
    public int BleedingHitPointLossPerRound { get; }

    /// <summary>Ch 6, "Bleeding" (p.149): "the target loses 1 additional fatigue point each round they are bleeding."</summary>
    public int BleedingFatiguePointLossPerRound { get; }

    /// <summary>Ch 6, "Bleeding" (p.149): "stopped for five combat rounds, it stops entirely on its own."</summary>
    public int BleedingStaunchedRoundsUntilPermanentStop { get; }

    /// <summary>
    /// Ch 6, "Entangling" (pp.150-151): "prevents the target's movement for the rest of the
    /// combat round."
    /// </summary>
    public bool EntanglingImmobilizesRemainderOfCurrentRound { get; }

    /// <summary>Ch 6, "Entangling" (pp.150-151): "and into the next combat round."</summary>
    public int EntanglingImmobilizedFollowingRounds { get; }

    /// <summary>
    /// Ch 6, "Fighting Defensively" (p.151) / "Parry"/"Dodge" (p.144): "each subsequent Dodge or
    /// parry attempt is at a cumulative -30% modifier."
    /// </summary>
    public int SuccessiveDefensePenaltyPercent { get; }
}
