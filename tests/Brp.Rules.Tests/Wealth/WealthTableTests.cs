using Brp.Rules.Wealth;

namespace Brp.Rules.Tests.Wealth;

/// <summary>
/// Construction and lookup edge cases for <see cref="WealthTable"/>, independent of the shipped
/// data (covered row-by-row in <c>Brp.Data.Tests.NoirWealthRulesetTests</c>).
/// </summary>
public class WealthTableTests
{
    private static WealthBand Band(int minimum, int maximum) =>
        new(minimum, maximum, "Test Rank", WealthLevel.Average, WealthLevel.Affluent);

    [Fact]
    public void Constructor_rejects_an_empty_band_list()
    {
        Assert.Throws<ArgumentException>(() => new WealthTable([]));
    }

    [Fact]
    public void Constructor_rejects_a_null_band_list()
    {
        Assert.Throws<ArgumentNullException>(() => new WealthTable(null!));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public void ForStatus_rejects_a_result_outside_1_to_100(int status)
    {
        var table = new WealthTable([Band(1, 100)]);
        Assert.Throws<ArgumentOutOfRangeException>(() => table.ForStatus(status));
    }

    [Fact]
    public void ForStatus_throws_when_no_band_covers_the_result()
    {
        var table = new WealthTable([Band(1, 50)]);
        Assert.Throws<ArgumentOutOfRangeException>(() => table.ForStatus(75));
    }

    [Fact]
    public void ForStatus_returns_the_band_covering_the_boundary_values()
    {
        var band = Band(40, 75);
        var table = new WealthTable([Band(1, 39), band, Band(76, 100)]);

        Assert.Same(band, table.ForStatus(40));
        Assert.Same(band, table.ForStatus(75));
    }

    [Fact]
    public void WealthLevel_is_ordered_ascending_by_the_book()
    {
        Assert.True(WealthLevel.Destitute < WealthLevel.Poor);
        Assert.True(WealthLevel.Poor < WealthLevel.Average);
        Assert.True(WealthLevel.Average < WealthLevel.Affluent);
        Assert.True(WealthLevel.Affluent < WealthLevel.Wealthy);
    }
}
