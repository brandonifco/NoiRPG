using System.Text.Json;
using Brp.Rules.Combat;

namespace Brp.Data;

/// <summary>
/// Loads NoiRPG's hit-location values from embedded JSON. Sourced: Ch 6: Combat, "Melee Hit
/// Location Table (Option)" (p.145) and "Damage and hit Locations (Option)" (pp.156-157, the limb
/// damage cap multiplier); Ch 2: Characters, "Hit Points by Hit Location (Option)" (p.14). Recorded
/// in <c>docs/decisions/0024-hit-locations.md</c>.
/// </summary>
public static class NoirHitLocationRuleset
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>Loads a new, immutable hit-location ruleset from the shipped data.</summary>
    public static HitLocationRuleset Load()
    {
        var assembly = typeof(NoirHitLocationRuleset).Assembly;
        using var stream = assembly.GetManifestResourceStream("Brp.Data.hit-location-ruleset.json")
            ?? throw new InvalidOperationException("The hit location ruleset data resource is missing.");
        var data = JsonSerializer.Deserialize<HitLocationRulesetData>(stream, SerializerOptions)
            ?? throw new InvalidOperationException("The hit location ruleset data is empty.");

        var rows = data.HitLocationTable.Select(row => new HitLocationTableRow(
            row.Minimum, row.Maximum, Enum.Parse<HitLocation>(row.Location, ignoreCase: true), row.Description));

        return new HitLocationRuleset(
            table: new HitLocationTable(rows),
            limbHeadAbdomenDivisor: data.PerLocationHitPoints.LimbHeadAbdomenDivisor,
            chestNumerator: data.PerLocationHitPoints.ChestNumerator,
            chestDenominator: data.PerLocationHitPoints.ChestDenominator,
            armDivisor: data.PerLocationHitPoints.ArmDivisor,
            limbDamageCapMultiplier: data.LimbDamageCapMultiplier);
    }

    private sealed class HitLocationRulesetData
    {
        public required List<HitLocationRowData> HitLocationTable { get; init; }

        public required PerLocationHitPointsData PerLocationHitPoints { get; init; }

        public required int LimbDamageCapMultiplier { get; init; }
    }

    private sealed class HitLocationRowData
    {
        public required int Minimum { get; init; }

        public required int Maximum { get; init; }

        public required string Location { get; init; }

        public required string Description { get; init; }

        public required string Source { get; init; }
    }

    private sealed class PerLocationHitPointsData
    {
        public required int LimbHeadAbdomenDivisor { get; init; }

        public required int ChestNumerator { get; init; }

        public required int ChestDenominator { get; init; }

        public required int ArmDivisor { get; init; }

        public required string Source { get; init; }
    }
}
