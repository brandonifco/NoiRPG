using System.Text.Json;
using Brp.Rules.Wealth;

namespace Brp.Data;

/// <summary>
/// Loads NoiRPG's Money and Wealth levels from embedded JSON. Sourced: Ch 3: Skills, "Status
/// Skill, Social Status, &amp; Character Wealth" (p.51), the "Victorian/Western/Pulp/Modern Status"
/// table (Issue #229). See <see cref="WealthRuleset"/> for the field-level citation.
/// </summary>
public static class NoirWealthRuleset
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>Loads a new, immutable wealth ruleset from the shipped data.</summary>
    public static WealthRuleset Load()
    {
        var assembly = typeof(NoirWealthRuleset).Assembly;
        using var stream = assembly.GetManifestResourceStream("Brp.Data.wealth-ruleset.json")
            ?? throw new InvalidOperationException("The wealth ruleset data resource is missing.");
        var data = JsonSerializer.Deserialize<WealthRulesetData>(stream, SerializerOptions)
            ?? throw new InvalidOperationException("The wealth ruleset data is empty.");

        var bands = data.WealthTable.Select(row => new WealthBand(
            row.Minimum,
            row.Maximum,
            row.SocialRank,
            Enum.Parse<WealthLevel>(row.WealthRating),
            Enum.Parse<WealthLevel>(row.MaximumWealth)));

        return new WealthRuleset(new WealthTable(bands));
    }

    private sealed class WealthRulesetData
    {
        public required IReadOnlyList<WealthRowData> WealthTable { get; init; }
    }

    private sealed class WealthRowData
    {
        public required int Minimum { get; init; }

        public required int Maximum { get; init; }

        public required string SocialRank { get; init; }

        public required string WealthRating { get; init; }

        public required string MaximumWealth { get; init; }
    }
}
