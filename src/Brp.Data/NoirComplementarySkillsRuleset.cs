using System.Text.Json;
using Brp.Rules.Skills;

namespace Brp.Data;

/// <summary>
/// Loads NoiRPG's Complementary Skills / Augment fraction from embedded JSON. Sourced: Ch 3:
/// Skills, "Augments and Complementary skills" (p.34) (Issue #114). See
/// <see cref="ComplementarySkillRuleset"/> for the field-level citation.
/// </summary>
public static class NoirComplementarySkillsRuleset
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>Loads a new, immutable complementary-skills ruleset from the shipped data.</summary>
    public static ComplementarySkillRuleset Load()
    {
        var assembly = typeof(NoirComplementarySkillsRuleset).Assembly;
        using var stream = assembly.GetManifestResourceStream("Brp.Data.complementary-skills-ruleset.json")
            ?? throw new InvalidOperationException("The complementary-skills ruleset data resource is missing.");
        var data = JsonSerializer.Deserialize<ComplementarySkillsData>(stream, SerializerOptions)
            ?? throw new InvalidOperationException("The complementary-skills ruleset data is empty.");

        return new ComplementarySkillRuleset(data.BonusNumerator, data.BonusDenominator);
    }

    private sealed class ComplementarySkillsData
    {
        public required int BonusNumerator { get; init; }

        public required int BonusDenominator { get; init; }
    }
}
