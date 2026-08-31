using System.Text.Json;
using Brp.Core.Dice;
using Brp.Rules.Combat;

namespace Brp.Data;

/// <summary>
/// Loads NoiRPG's four D100 fumble consequence tables from embedded JSON. Sourced: Ch 6: Combat --
/// "Melee Weapon Attack Fumbles", "Melee Weapon Parry Fumbles", "Missile Weapon Attack Fumbles"
/// (p.148), and "Natural Weapon Attack and Parry Fumbles" (p.149). Recorded in
/// <c>docs/decisions/0020-fumble-tables.md</c>.
/// </summary>
public static class NoirFumbleRuleset
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>Loads a new, immutable fumble ruleset from the shipped data.</summary>
    public static FumbleRuleset Load()
    {
        var assembly = typeof(NoirFumbleRuleset).Assembly;
        using var stream = assembly.GetManifestResourceStream("Brp.Data.fumble-ruleset.json")
            ?? throw new InvalidOperationException("The fumble ruleset data resource is missing.");
        var data = JsonSerializer.Deserialize<FumbleRulesetData>(stream, SerializerOptions)
            ?? throw new InvalidOperationException("The fumble ruleset data is empty.");

        var tables = data.Tables.Select(entry => BuildTable(Enum.Parse<FumbleTable>(entry.Key), entry.Value));
        return new FumbleRuleset(tables);
    }

    private static FumbleConsequenceTable BuildTable(FumbleTable table, FumbleTableData data)
    {
        var rows = data.Rows.Select(row => new FumbleConsequenceRow(
            row.MinRoll,
            row.MaxRoll,
            Enum.Parse<FumbleEffectKind>(row.Kind),
            row.Effect,
            row.Amount is null ? null : DiceExpression.Parse(row.Amount),
            row.Magnitude,
            row.HitGrade is null ? null : Enum.Parse<LandedGrade>(row.HitGrade),
            row.Fallback is null
                ? null
                : new FumbleFallback(
                    Enum.Parse<FumbleFallbackCondition>(row.Fallback.Condition),
                    row.Fallback.MinRoll,
                    row.Fallback.MaxRoll),
            row.RerollCount));

        return new FumbleConsequenceTable(table, rows);
    }

    private sealed class FumbleRulesetData
    {
        public required IReadOnlyDictionary<string, FumbleTableData> Tables { get; init; }
    }

    private sealed class FumbleTableData
    {
        public required IReadOnlyList<FumbleRowData> Rows { get; init; }
    }

    private sealed class FumbleRowData
    {
        public required int MinRoll { get; init; }

        public required int MaxRoll { get; init; }

        public required string Kind { get; init; }

        public required string Effect { get; init; }

        public string? Amount { get; init; }

        public int? Magnitude { get; init; }

        public string? HitGrade { get; init; }

        public FumbleFallbackData? Fallback { get; init; }

        public int? RerollCount { get; init; }
    }

    private sealed class FumbleFallbackData
    {
        public required string Condition { get; init; }

        public required int MinRoll { get; init; }

        public required int MaxRoll { get; init; }
    }
}
