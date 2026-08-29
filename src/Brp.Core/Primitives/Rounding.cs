namespace Brp.Core.Primitives;

/// <summary>
/// Named rounding modes. Every formula in the engine states which mode it uses,
/// because the source book rounds differently in different places and a silent
/// mismatch is the classic way an engine drifts from its rulebook.
/// </summary>
public enum RoundingMode
{
    /// <summary>Toward positive infinity for positives; magnitude-preserving for negatives.</summary>
    Up,

    /// <summary>Toward zero.</summary>
    Down,

    /// <summary>Nearest, with exact halves going up.</summary>
    HalfUp,
}

/// <summary>Applies a <see cref="RoundingMode"/> to integer division.</summary>
public static class Rounding
{
    /// <summary>
    /// Divides <paramref name="numerator"/> by <paramref name="denominator"/> using the
    /// given mode. Sign is preserved: rounding operates on the magnitude, so a mode of
    /// <see cref="RoundingMode.Up"/> makes a negative result more negative, matching the
    /// intuition that "round up" means "away from zero" when the book applies it to a
    /// penalty rather than a bonus.
    /// </summary>
    public static int Divide(int numerator, int denominator, RoundingMode mode)
    {
        ArgumentOutOfRangeException.ThrowIfZero(denominator);

        var negative = (numerator < 0) ^ (denominator < 0);
        var a = Math.Abs((long)numerator);
        var b = Math.Abs((long)denominator);

        var magnitude = mode switch
        {
            RoundingMode.Up => (a + b - 1) / b,
            RoundingMode.Down => a / b,
            RoundingMode.HalfUp => (2 * a + b) / (2 * b),
            _ => throw new ArgumentOutOfRangeException(nameof(mode)),
        };

        return (int)(negative ? -magnitude : magnitude);
    }
}
