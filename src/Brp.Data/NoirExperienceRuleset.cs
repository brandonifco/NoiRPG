using System.Text.Json;
using Brp.Rules.Advancement;

namespace Brp.Data;

/// <summary>
/// Loads NoiRPG's Layer 3 advancement parameters from embedded JSON. The source is
/// Ch 5: System, "Skill Training" (p.139) -- see <see cref="ExperienceRuleset"/>'s own
/// members for the exact citation of each value.
/// </summary>
public static class NoirExperienceRuleset
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>Loads a new, immutable experience ruleset view from the shipped data.</summary>
    public static ExperienceRuleset Load()
    {
        var assembly = typeof(NoirExperienceRuleset).Assembly;
        using var stream = assembly.GetManifestResourceStream("Brp.Data.experience-ruleset.json")
            ?? throw new InvalidOperationException("The experience ruleset data resource is missing.");
        var data = JsonSerializer.Deserialize<ExperienceRulesetData>(stream, SerializerOptions)
            ?? throw new InvalidOperationException("The experience ruleset data is empty.");

        return new ExperienceRuleset(
            data.TrainingCapPercent, data.ResearchGainDieSides, data.ResearchGainOffset, data.ResearchDefaultGain);
    }

    private sealed class ExperienceRulesetData
    {
        public required int TrainingCapPercent { get; init; }

        public required int ResearchGainDieSides { get; init; }

        public required int ResearchGainOffset { get; init; }

        public required int ResearchDefaultGain { get; init; }
    }
}
