using Brp.Rules.Combat;

namespace Brp.Data.Tests;

/// <summary>
/// Confirms the shipped injury ruleset data loads and reproduces Ch 7: Spot Rules -- "Falling"
/// (pp.171-172), "Poison"/"Poison Antidotes" (p.176), and "Disease"/"Illness Severity Table"
/// (p.170). The Illness Severity Table is reproduced row by row rather than sampled, per
/// AGENTS.md's "table-backed rule" convention. See <c>docs/decisions/0019-injury-spot-rules.md</c>.
/// </summary>
public class NoirInjuryRulesetTests
{
    private static readonly InjuryRuleset Ruleset = NoirInjuryRuleset.Load();

    [Fact]
    public void Falling_values_match_chapter_7_pages_171_to_172()
    {
        var falling = Ruleset.Falling;

        Assert.Equal("1D6", falling.BaseDamagePerIncrement.Notation);
        Assert.Equal(3, falling.MetersPerDamageIncrement);
        Assert.Equal(2, falling.ForceMultiplier);
        Assert.Equal(5, falling.SmallSizeThreshold);
        Assert.Equal("1D6", falling.SmallSizeReduction.Notation);
        Assert.Equal(20, falling.LargeSizeThreshold);
        Assert.Equal(20, falling.LargeSizeBand);
        Assert.Equal("1D6", falling.LargeSizeExtraDamage.Notation);
        Assert.Equal(3, falling.ArmorHalfProtectionMaxMeters);
        Assert.Equal(1, falling.ArmorProtectionNumerator);
        Assert.Equal(2, falling.ArmorProtectionDenominator);
    }

    [Fact]
    public void Poison_values_match_chapter_7_page_176()
    {
        var poison = Ruleset.Poison;

        Assert.Equal(1, poison.NotOvercomeNumerator);
        Assert.Equal(2, poison.NotOvercomeDenominator);
        Assert.Equal(3, poison.OnsetFastActingRounds);
        Assert.Equal(3, poison.OnsetSlowActingTurns);
        Assert.Equal(6, poison.AntidoteWindowTurns);
    }

    [Fact]
    public void Disease_recovery_ladder_values_match_chapter_7_page_170()
    {
        var disease = Ruleset.Disease;

        Assert.Equal("1D2", disease.MinorDiseaseHitPointLoss.Notation);
        Assert.Equal("1D6", disease.MinorDiseaseFatigueLoss.Notation);
        Assert.Equal(2, disease.RecoveryLadderStartingMultiplier);
        Assert.Equal(1, disease.RecoveryLadderMultiplierIncrementPerDay);
        Assert.Equal(1, disease.RecoveryLadderFumbleMultiplierPenalty);
        Assert.Equal(1, disease.RecoveryLadderStrenuousConditionPenalty);
    }

    public static IEnumerable<object?[]> IllnessSeverityRows()
    {
        // Ch 7, "Illness Severity Table" (p.170): Failures -> Degree of Illness.
        yield return new object?[] { 0, 0, IllnessDegree.None, IllnessLossPeriod.None };
        yield return new object?[] { 1, 1, IllnessDegree.Mild, IllnessLossPeriod.Week };
        yield return new object?[] { 2, 2, IllnessDegree.Acute, IllnessLossPeriod.Day };
        yield return new object?[] { 3, 3, IllnessDegree.Severe, IllnessLossPeriod.Hour };
        yield return new object?[] { 4, null, IllnessDegree.Terminal, IllnessLossPeriod.Minute };
    }

    [Theory]
    [MemberData(nameof(IllnessSeverityRows))]
    public void Every_printed_illness_severity_row_matches_the_book(
        int minimumFailures, int? maximumFailures, IllnessDegree expectedDegree, IllnessLossPeriod expectedPeriod)
    {
        var band = Ruleset.Disease.IllnessSeverityTable.Bands.Single(b => b.MinimumFailures == minimumFailures);

        Assert.Equal(maximumFailures, band.MaximumFailures);
        Assert.Equal(expectedDegree, band.Degree);
        Assert.Equal(expectedPeriod, band.LossPeriod);
    }

    [Fact]
    public void The_illness_severity_table_has_exactly_the_five_printed_rows()
    {
        Assert.Equal(5, Ruleset.Disease.IllnessSeverityTable.Bands.Count);
    }

    [Theory]
    [InlineData(0, IllnessDegree.None)]
    [InlineData(1, IllnessDegree.Mild)]
    [InlineData(2, IllnessDegree.Acute)]
    [InlineData(3, IllnessDegree.Severe)]
    [InlineData(4, IllnessDegree.Terminal)]
    [InlineData(9, IllnessDegree.Terminal)]
    public void For_failures_resolves_to_the_printed_degree_including_the_open_ended_terminal_row(
        int failures, IllnessDegree expected)
    {
        Assert.Equal(expected, Ruleset.Disease.IllnessSeverityTable.ForFailures(failures).Degree);
    }
}
