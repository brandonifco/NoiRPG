using System.Text.Json;
using Brp.Rules.Combat;

namespace Brp.Data;

/// <summary>
/// Loads NoiRPG's named poison/drug catalog from embedded JSON. Sourced: Ch 8: Equipment,
/// "Poisons", the Sample Poisons Table (p.221) -- see <c>poison-catalog.json</c>'s
/// <c>source</c> field for the full citation and the column-misalignment errata correction.
/// Every entry's POT reuses the existing Ch 7 poison mechanic (<see cref="PoisonRuleset"/>,
/// <see cref="PoisonResolver"/>, loaded by <see cref="NoirInjuryRuleset"/>); this loader adds
/// no new mechanic, only named data.
/// </summary>
public static class NoirPoisonCatalog
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>Loads a new, immutable poison catalog from the shipped data.</summary>
    public static PoisonCatalog Load()
    {
        var assembly = typeof(NoirPoisonCatalog).Assembly;
        using var stream = assembly.GetManifestResourceStream("Brp.Data.poison-catalog.json")
            ?? throw new InvalidOperationException("The poison catalog data resource is missing.");
        var data = JsonSerializer.Deserialize<PoisonCatalogData>(stream, SerializerOptions)
            ?? throw new InvalidOperationException("The poison catalog data is empty.");

        return new PoisonCatalog(data.Poisons.Select(ToEntry));
    }

    private static PoisonCatalogEntry ToEntry(PoisonEntryData entry) => new(
        Id: new PoisonId(entry.Id),
        Name: entry.Name,
        SpeedOfEffect: entry.SpeedOfEffect,
        Potency: entry.Potency,
        Symptoms: entry.Symptoms,
        Source: entry.Source);

    private sealed class PoisonCatalogData
    {
        public required IReadOnlyList<PoisonEntryData> Poisons { get; init; }
    }

    private sealed class PoisonEntryData
    {
        public required string Id { get; init; }

        public required string Name { get; init; }

        public required string SpeedOfEffect { get; init; }

        public required int Potency { get; init; }

        public string? Symptoms { get; init; }

        public required string Source { get; init; }
    }
}
