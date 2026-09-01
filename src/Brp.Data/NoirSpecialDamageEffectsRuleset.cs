using System.Text.Json;
using Brp.Core.Dice;
using Brp.Rules.Combat;

namespace Brp.Data;

/// <summary>
/// Loads NoiRPG's special-damage-effects values from embedded JSON. Sourced: Ch 6: Combat,
/// "Crushing" (p.149), "Bleeding" (p.149), "Impaling" (pp.149-150), "Entangling" (pp.150-151),
/// "Knockback" (p.151), and "Fighting Defensively" (p.151). See
/// <c>docs/decisions/0017-damage.md</c> (the deferral) and #113 (the implementation).
/// </summary>
public static class NoirSpecialDamageEffectsRuleset
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>Loads a new, immutable special-damage-effects ruleset from the shipped data.</summary>
    public static SpecialDamageEffectsRuleset Load()
    {
        var assembly = typeof(NoirSpecialDamageEffectsRuleset).Assembly;
        using var stream = assembly.GetManifestResourceStream("Brp.Data.special-damage-effects-ruleset.json")
            ?? throw new InvalidOperationException("The special-damage-effects ruleset data resource is missing.");
        var data = JsonSerializer.Deserialize<SpecialDamageEffectsRulesetData>(stream, SerializerOptions)
            ?? throw new InvalidOperationException("The special-damage-effects ruleset data is empty.");

        return new SpecialDamageEffectsRuleset(
            crushingStunDuration: DiceExpression.Parse(data.CrushingStun.StunDurationFormula),
            impalingSelfExtractionFailureExtraDamage: DiceExpression.Parse(data.Impaling.SelfExtractionFailureExtraDamageFormula),
            knockbackMetersPerDamagePoint: data.Knockback.MetersPerDamagePoint,
            knockbackObstacleDamagePerIncrement: DiceExpression.Parse(data.Knockback.ObstacleDamageFormula),
            knockbackObstacleIncrementMeters: data.Knockback.ObstacleIncrementMeters,
            bleedingHitPointLossPerRound: data.Bleeding.HitPointLossPerRound,
            bleedingFatiguePointLossPerRound: data.Bleeding.FatiguePointLossPerRound,
            bleedingStaunchedRoundsUntilPermanentStop: data.Bleeding.StaunchedRoundsUntilPermanentStop,
            entanglingImmobilizesRemainderOfCurrentRound: data.Entangling.ImmobilizesRemainderOfCurrentRound,
            entanglingImmobilizedFollowingRounds: data.Entangling.ImmobilizedFollowingRounds,
            successiveDefensePenaltyPercent: data.FightingDefensively.SuccessiveDefensePenaltyPercent);
    }

    private sealed class SpecialDamageEffectsRulesetData
    {
        public required CrushingStunData CrushingStun { get; init; }

        public required ImpalingData Impaling { get; init; }

        public required KnockbackData Knockback { get; init; }

        public required BleedingData Bleeding { get; init; }

        public required EntanglingData Entangling { get; init; }

        public required FightingDefensivelyData FightingDefensively { get; init; }
    }

    private sealed class CrushingStunData
    {
        public required string StunDurationFormula { get; init; }
    }

    private sealed class ImpalingData
    {
        public required string SelfExtractionFailureExtraDamageFormula { get; init; }
    }

    private sealed class KnockbackData
    {
        public required int MetersPerDamagePoint { get; init; }

        public required string ObstacleDamageFormula { get; init; }

        public required int ObstacleIncrementMeters { get; init; }
    }

    private sealed class BleedingData
    {
        public required int HitPointLossPerRound { get; init; }

        public required int FatiguePointLossPerRound { get; init; }

        public required int StaunchedRoundsUntilPermanentStop { get; init; }
    }

    private sealed class EntanglingData
    {
        public required bool ImmobilizesRemainderOfCurrentRound { get; init; }

        public required int ImmobilizedFollowingRounds { get; init; }
    }

    private sealed class FightingDefensivelyData
    {
        public required int SuccessiveDefensePenaltyPercent { get; init; }
    }
}
