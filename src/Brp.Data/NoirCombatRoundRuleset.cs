using System.Text.Json;
using Brp.Rules.Combat;

namespace Brp.Data;

/// <summary>
/// Loads NoiRPG's combat-round and DEX-rank ordering parameters from embedded JSON. Sourced:
/// Ch 6: Combat, "Combat Round Phases" (p.142), "Action" (p.143), and "Move"/"Noncombat
/// Action"/"Attack" (p.144) -- see each field on <see cref="CombatRoundRuleset"/> for its exact
/// citation. Recorded in <c>docs/decisions/0015-combat-round.md</c>.
/// </summary>
public static class NoirCombatRoundRuleset
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>Loads a new, immutable combat-round ruleset from the shipped data.</summary>
    public static CombatRoundRuleset Load()
    {
        var assembly = typeof(NoirCombatRoundRuleset).Assembly;
        using var stream = assembly.GetManifestResourceStream("Brp.Data.combat-round-ruleset.json")
            ?? throw new InvalidOperationException("The combat-round ruleset data resource is missing.");
        var data = JsonSerializer.Deserialize<CombatRoundRulesetData>(stream, SerializerOptions)
            ?? throw new InvalidOperationException("The combat-round ruleset data is empty.");

        var phases = data.CombatRoundPhases
            .Select(ParsePhase)
            .ToList();

        var weaponTypeTiebreakOrder = data.WeaponTypeTiebreakOrder
            .Select(ParseWeaponTypeTier)
            .ToList();

        var movementTiers = data.MovementTiers
            .Select(tier => new MovementTier(
                tier.MinMeters, tier.MaxMeters, tier.DexRankFractionNumerator, tier.DexRankFractionDenominator))
            .ToList();

        return new CombatRoundRuleset(
            phases: phases,
            dexRankSourceCharacteristic: data.DexRankDerivedFromCharacteristic,
            dexRankOrderedDescending: ParseOrderingDirection(data.DexRankOrderingDirection),
            weaponTypeTiebreakOrder: weaponTypeTiebreakOrder,
            movementTiers: movementTiers,
            drawWeaponDexRankPenalty: data.DrawWeaponDexRankPenalty,
            multipleActionDexRankPenalty: data.MultipleActionDexRankPenalty,
            dexRankFloor: data.DexRankFloor);
    }

    private static CombatRoundPhase ParsePhase(string value) => value switch
    {
        "Statements" => CombatRoundPhase.Statements,
        "Action" => CombatRoundPhase.Action,
        "Resolution" => CombatRoundPhase.Resolution,
        _ => throw new InvalidOperationException($"Unknown combat round phase '{value}'."),
    };

    private static WeaponTypeTier ParseWeaponTypeTier(string value) => value switch
    {
        "missile" => WeaponTypeTier.Missile,
        "long" => WeaponTypeTier.LongWeapon,
        "medium" => WeaponTypeTier.MediumWeapon,
        "short" => WeaponTypeTier.ShortOrUnarmed,
        _ => throw new InvalidOperationException($"Unknown weapon-type tiebreak tier '{value}'."),
    };

    private static bool ParseOrderingDirection(string value) => value switch
    {
        "descending" => true,
        "ascending" => false,
        _ => throw new InvalidOperationException($"Unknown DEX rank ordering direction '{value}'."),
    };

    private sealed class CombatRoundRulesetData
    {
        public required IReadOnlyList<string> CombatRoundPhases { get; init; }

        public required string DexRankDerivedFromCharacteristic { get; init; }

        public required string DexRankOrderingDirection { get; init; }

        public required IReadOnlyList<string> WeaponTypeTiebreakOrder { get; init; }

        public required IReadOnlyList<MovementTierData> MovementTiers { get; init; }

        public required int DrawWeaponDexRankPenalty { get; init; }

        public required int MultipleActionDexRankPenalty { get; init; }

        public required int DexRankFloor { get; init; }
    }

    private sealed class MovementTierData
    {
        public required int MinMeters { get; init; }

        public required int MaxMeters { get; init; }

        public required int DexRankFractionNumerator { get; init; }

        public required int DexRankFractionDenominator { get; init; }
    }
}
