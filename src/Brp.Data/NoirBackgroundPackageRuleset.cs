using System.Text.Json;
using Brp.Core.Skills;
using Brp.Rules.Creation;

namespace Brp.Data;

/// <summary>
/// Loads Freeform Profession background packages from embedded JSON (Ch 2, "Freeform
/// Professions (Option)" checklist entry, p.229; see <see cref="BackgroundPackage"/> for the
/// mechanic this data drives). Ships one placeholder/test fixture package -- the actual noir
/// packages (ex-cop, ex-journalist, ex-lawyer, ex-soldier, ex-accountant) are Layer 5 content
/// authored on top of this mechanism, not this issue's concern.
/// </summary>
public static class NoirBackgroundPackageRuleset
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>Loads every background package shipped in data.</summary>
    public static IReadOnlyList<BackgroundPackage> LoadAll()
    {
        var assembly = typeof(NoirBackgroundPackageRuleset).Assembly;
        using var stream = assembly.GetManifestResourceStream("Brp.Data.background-packages.json")
            ?? throw new InvalidOperationException("The background package data resource is missing.");
        var data = JsonSerializer.Deserialize<List<BackgroundPackageData>>(stream, SerializerOptions)
            ?? throw new InvalidOperationException("The background package data is empty.");

        return data.Select(entry => new BackgroundPackage(
            entry.Name,
            entry.ProfessionalSkillPoints.ToDictionary(pair => new SkillId(pair.Key), pair => pair.Value)))
            .ToList();
    }

    private sealed class BackgroundPackageData
    {
        public required string Name { get; init; }

        public required Dictionary<string, int> ProfessionalSkillPoints { get; init; }
    }
}
