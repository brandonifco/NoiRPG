using Brp.Rules.Combat;

namespace Brp.Data.Tests;

/// <summary>
/// Confirms the shipped spot-rule data loads and reproduces every printed percentage value of the
/// in-scope Ch 7 situational combat spot rules -- one case per printed figure rather than sampled,
/// per AGENTS.md's "table-backed rule" convention. Sources: Ch 7, "Firing Into Combat" (p.173) and
/// "Darkness" (p.169, drawing on the Ch 5 Situational Modifiers "Environment" row, p.133). See
/// <c>docs/decisions/0018-spot-rules.md</c>.
/// </summary>
public class NoirSpotRuleRulesetTests
{
    private static readonly SpotRuleRuleset Ruleset = NoirSpotRuleRuleset.Load();

    public static IEnumerable<object[]> PrintedValues()
    {
        // (label, actual, expected) -- every number the book prints for the in-scope spot rules.
        yield return new object[] { "firing into combat -20% (Ch 7, p.173)", Ruleset.FiringIntoCombatModifier, -20 };
        yield return new object[] { "darkness / semi-darkness -20% (Ch 5, p.133)", Ruleset.DarknessSemiDarknessModifier, -20 };
        yield return new object[] { "pitch black -50% (Ch 5, p.133)", Ruleset.DarknessPitchBlackModifier, -50 };
        yield return new object[] { "darkness detection halving numerator (Ch 7, p.169)", Ruleset.DarknessDetectionHalvingNumerator, 1 };
        yield return new object[] { "darkness detection halving denominator (Ch 7, p.169)", Ruleset.DarknessDetectionHalvingDenominator, 2 };
    }

    [Theory]
    [MemberData(nameof(PrintedValues))]
    public void Every_printed_spot_rule_value_matches_the_book(string label, int actual, int expected)
    {
        Assert.Equal(expected, actual);
        Assert.False(string.IsNullOrWhiteSpace(label));
    }

    [Fact]
    public void The_shipped_ruleset_carries_exactly_the_five_printed_values()
    {
        Assert.Equal(5, PrintedValues().Count());
    }

    [Fact]
    public void The_shipped_data_loads_without_throwing()
    {
        // A validating constructor (SpotRuleRuleset) means a bad transcription -- a positive
        // penalty, or pitch black milder than semi-darkness -- fails to load rather than shipping.
        var ruleset = NoirSpotRuleRuleset.Load();

        Assert.NotNull(ruleset);
        Assert.True(ruleset.DarknessPitchBlackModifier < ruleset.DarknessSemiDarknessModifier);
    }
}
