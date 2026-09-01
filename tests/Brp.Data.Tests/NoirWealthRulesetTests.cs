using Brp.Rules.Wealth;

namespace Brp.Data.Tests;

/// <summary>
/// Confirms the shipped wealth data loads and reproduces every row of Ch 3: Skills, "Status Skill,
/// Social Status, &amp; Character Wealth", the "Victorian/Western/Pulp/Modern Status" table (p.51)
/// -- row by row rather than sampled, per AGENTS.md's "table-backed rule" convention. See
/// <c>docs/decisions/0030-money-and-wealth-levels.md</c>.
/// </summary>
public class NoirWealthRulesetTests
{
    private static readonly WealthRuleset Ruleset = NoirWealthRuleset.Load();

    // (minimum, maximum, socialRank, wealthRating, maximumWealth) -- one case per printed row, p.51.
    public static IEnumerable<object?[]> PrintedRows()
    {
        yield return new object?[] { 1, 14, "Lower Class", WealthLevel.Destitute, WealthLevel.Poor };
        yield return new object?[] { 15, 29, "Lower Class", WealthLevel.Poor, WealthLevel.Average };
        yield return new object?[] { 30, 39, "Lower Middle Class", WealthLevel.Average, WealthLevel.Affluent };
        yield return new object?[] { 40, 75, "Middle Class", WealthLevel.Average, WealthLevel.Affluent };
        yield return new object?[] { 76, 95, "Upper Middle Class", WealthLevel.Affluent, WealthLevel.Wealthy };
        yield return new object?[] { 96, 100, "Upper Class", WealthLevel.Wealthy, WealthLevel.Wealthy };
    }

    [Theory]
    [MemberData(nameof(PrintedRows))]
    public void Every_printed_table_row_matches_the_book(
        int minimum, int maximum, string socialRank, WealthLevel wealthRating, WealthLevel maximumWealth)
    {
        // A row is looked up by its own range so a mis-ordered or mis-ranged transcription surfaces here.
        var band = Ruleset.Table.ForStatus(minimum);
        Assert.Same(band, Ruleset.Table.ForStatus(maximum));

        Assert.Equal(minimum, band.MinimumStatus);
        Assert.Equal(maximum, band.MaximumStatus);
        Assert.Equal(socialRank, band.SocialRank);
        Assert.Equal(wealthRating, band.WealthRating);
        Assert.Equal(maximumWealth, band.MaximumWealth);

        Assert.Equal(wealthRating, Ruleset.WealthLevelForStatus(minimum));
        Assert.Equal(maximumWealth, Ruleset.MaximumWealthForStatus(minimum));
    }

    [Fact]
    public void The_shipped_table_has_exactly_the_six_printed_rows()
    {
        Assert.Equal(6, Ruleset.Table.Bands.Count);
    }

    [Fact]
    public void The_table_covers_every_status_result_exactly_once()
    {
        // No gaps and no overlaps across the whole 1-100 range (a printed 00 read as 100).
        for (var status = 1; status <= 100; status++)
        {
            Assert.Single(Ruleset.Table.Bands, band => band.Contains(status));
        }
    }
}
