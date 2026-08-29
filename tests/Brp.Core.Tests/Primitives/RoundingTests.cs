using Brp.Core.Primitives;

namespace Brp.Core.Tests.Primitives;

public class RoundingTests
{
    [Theory]
    [InlineData(7, 2, 4)] // 3.5 -> 4
    [InlineData(6, 2, 3)] // exact
    [InlineData(-7, 2, -4)] // -3.5 -> -4 (away from zero)
    [InlineData(-6, 2, -3)] // exact, negative
    [InlineData(1, 3, 1)] // 0.33 -> 1
    [InlineData(0, 5, 0)]
    [InlineData(7, -2, -4)] // negative denominator: sign still preserved, magnitude rounds up
    [InlineData(-7, -2, 4)] // double negative -> positive
    public void Up_rounds_away_from_zero(int numerator, int denominator, int expected)
    {
        Assert.Equal(expected, Rounding.Divide(numerator, denominator, RoundingMode.Up));
    }

    [Theory]
    [InlineData(7, 2, 3)] // 3.5 -> 3
    [InlineData(6, 2, 3)] // exact
    [InlineData(-7, 2, -3)] // -3.5 -> -3 (toward zero)
    [InlineData(-6, 2, -3)]
    [InlineData(1, 3, 0)] // 0.33 -> 0
    [InlineData(0, 5, 0)]
    public void Down_rounds_toward_zero(int numerator, int denominator, int expected)
    {
        Assert.Equal(expected, Rounding.Divide(numerator, denominator, RoundingMode.Down));
    }

    [Theory]
    [InlineData(5, 2, 3)] // 2.5 -> 3 (half goes up)
    [InlineData(-5, 2, -3)] // -2.5 -> -3 (half goes up in magnitude)
    [InlineData(4, 2, 2)] // exact
    [InlineData(7, 2, 4)] // 3.5 -> 4
    [InlineData(-7, 2, -4)] // -3.5 -> -4
    [InlineData(6, 4, 2)] // 1.5 -> 2
    [InlineData(1, 4, 0)] // 0.25 -> 0
    [InlineData(0, 5, 0)]
    public void HalfUp_rounds_to_nearest_with_exact_halves_up(int numerator, int denominator, int expected)
    {
        Assert.Equal(expected, Rounding.Divide(numerator, denominator, RoundingMode.HalfUp));
    }

    [Theory]
    [InlineData(RoundingMode.Up)]
    [InlineData(RoundingMode.Down)]
    [InlineData(RoundingMode.HalfUp)]
    public void Divide_by_zero_throws(RoundingMode mode)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Rounding.Divide(5, 0, mode));
    }
}
