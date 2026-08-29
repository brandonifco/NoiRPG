using Brp.Core.Primitives;

namespace Brp.Core.Tests.Primitives;

public class PercentTests
{
    [Theory]
    [InlineData(-50, 0)]
    [InlineData(-1, 0)]
    [InlineData(0, 0)]
    [InlineData(37, 37)]
    [InlineData(100, 100)]
    [InlineData(150, 150)] // the source's results table continues past 100%
    public void Of_floors_at_zero_and_permits_values_above_100(int input, int expected)
    {
        Assert.Equal(expected, Percent.Of(input).Value);
    }

    [Fact]
    public void Zero_is_the_default_value()
    {
        Assert.Equal(0, Percent.Zero.Value);
        Assert.Equal(default, Percent.Zero);
    }

    [Theory]
    [InlineData(50, 10, 60)]
    [InlineData(50, -60, 0)] // floors at zero rather than going negative
    [InlineData(0, -5, 0)]
    [InlineData(100, 20, 120)] // above 100 is a supported state
    public void Add_applies_a_signed_delta_and_floors_at_zero(int start, int delta, int expected)
    {
        Assert.Equal(expected, Percent.Of(start).Add(delta).Value);
    }

    [Fact]
    public void Scale_uses_the_given_rounding_mode()
    {
        Assert.Equal(50, Percent.Of(100).Scale(1, 2, RoundingMode.Up).Value);
        Assert.Equal(51, Percent.Of(101).Scale(1, 2, RoundingMode.Up).Value); // ceil(50.5)
        Assert.Equal(50, Percent.Of(101).Scale(1, 2, RoundingMode.Down).Value); // floor(50.5)
    }

    [Fact]
    public void Comparison_operators_and_CompareTo_order_by_value()
    {
        var low = Percent.Of(10);
        var high = Percent.Of(90);

        Assert.True(low < high);
        Assert.True(high > low);
        Assert.True(low <= Percent.Of(10));
        Assert.True(high >= Percent.Of(90));
        Assert.True(low.CompareTo(high) < 0);
        Assert.True(high.CompareTo(low) > 0);
        Assert.Equal(0, low.CompareTo(Percent.Of(10)));
    }

    [Fact]
    public void ToString_renders_a_percent_sign()
    {
        Assert.Equal("42%", Percent.Of(42).ToString());
    }
}
