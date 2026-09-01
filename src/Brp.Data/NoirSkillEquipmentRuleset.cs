using System.Text.Json;
using Brp.Core.Skills;
using Brp.Rules.Gear;

namespace Brp.Data;

/// <summary>
/// Loads NoiRPG's Skills-and-Equipment mapping from embedded JSON. Sourced: Ch 8: Equipment,
/// "Skills and Equipment" (pp.185-186), hand-picked to the modern noir subset per
/// <c>orc-scope-filter.md</c> -- see <c>skill-equipment-ruleset.json</c>'s own <c>source</c>
/// field for the exact citation and cut list.
/// </summary>
public static class NoirSkillEquipmentRuleset
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>Loads a new, immutable skill-equipment ruleset from the shipped data.</summary>
    public static SkillEquipmentRuleset Load()
    {
        var assembly = typeof(NoirSkillEquipmentRuleset).Assembly;
        using var stream = assembly.GetManifestResourceStream("Brp.Data.skill-equipment-ruleset.json")
            ?? throw new InvalidOperationException("The skill-equipment ruleset data resource is missing.");
        var data = JsonSerializer.Deserialize<SkillEquipmentData>(stream, SerializerOptions)
            ?? throw new InvalidOperationException("The skill-equipment ruleset data is empty.");

        var links = data.Links.Select(link => new SkillEquipmentLink(new SkillId(link.SkillId), link.PotentialEquipment));
        return new SkillEquipmentRuleset(links);
    }

    private sealed class SkillEquipmentData
    {
        public required List<SkillEquipmentLinkData> Links { get; init; }
    }

    private sealed class SkillEquipmentLinkData
    {
        public required string SkillId { get; init; }

        public required string PotentialEquipment { get; init; }
    }
}
