using System.Text.Json;
using Brp.Rules.Gear;

namespace Brp.Data;

/// <summary>
/// Loads NoiRPG's Layer 4 breakable-item list (item SIZ/hit points for forcing doors, windows,
/// and locks) from embedded JSON. Source: Ch 8: Equipment, "General Qualities of Objects",
/// "Damage to Inanimate Objects", "Armor Value of Substances" (p.224), and "SIZ of Common
/// Objects" (pp.225-226). Hand-picked to a small realistic noir subset per `orc-scope-filter.md`,
/// Ch 8, line 137 -- see each entry's own <c>source</c> field in the ruleset JSON for the exact
/// citation, and <c>docs/decisions/0033-item-hit-points.md</c> for the two entries the book
/// prints no exact row for.
/// </summary>
public static class NoirItemHitPointsRuleset
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>Loads a new, immutable breakable-item registry from the shipped data.</summary>
    public static BreakableItemRegistry Load()
    {
        var assembly = typeof(NoirItemHitPointsRuleset).Assembly;
        using var stream = assembly.GetManifestResourceStream("Brp.Data.item-hit-points-ruleset.json")
            ?? throw new InvalidOperationException("The item hit points ruleset data resource is missing.");
        var data = JsonSerializer.Deserialize<ItemHitPointsRulesetData>(stream, SerializerOptions)
            ?? throw new InvalidOperationException("The item hit points ruleset data is empty.");

        var items = data.Items.Select(ToDefinition).ToList();
        return new BreakableItemRegistry(items);
    }

    private static BreakableItemDefinition ToDefinition(BreakableItemEntryData entry) => new(
        Id: new BreakableItemId(entry.Id),
        Name: entry.Name,
        Siz: entry.Siz,
        HitPoints: entry.HitPoints,
        ArmorValue: entry.ArmorValue,
        Source: entry.Source);

    private sealed class ItemHitPointsRulesetData
    {
        public required List<BreakableItemEntryData> Items { get; init; }
    }

    private sealed class BreakableItemEntryData
    {
        public required string Id { get; init; }

        public required string Name { get; init; }

        public required int Siz { get; init; }

        public required int HitPoints { get; init; }

        public required int ArmorValue { get; init; }

        public required string Source { get; init; }
    }
}
