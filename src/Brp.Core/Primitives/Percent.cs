namespace Brp.Core.Primitives;

/// <summary>
/// A percentage chance. Floors at zero and is deliberately <em>not</em> capped at 100:
/// the source's results table extends past 100% and continues by fives, so ratings above
/// 100 are a supported state rather than an error.
/// </summary>
public readonly record struct Percent : IComparable<Percent>
{
    /// <summary>The underlying whole-number percentage. Never negative.</summary>
    public int Value { get; private init; }

    /// <summary>Zero percent.</summary>
    public static Percent Zero => default;

    /// <summary>Creates a percentage, flooring at zero.</summary>
    public static Percent Of(int value) => new() { Value = Math.Max(0, value) };

    /// <summary>Adds a signed modifier, flooring at zero.</summary>
    public Percent Add(int delta) => Of(Value + delta);

    /// <summary>Scales by a rational factor using the given rounding mode.</summary>
    public Percent Scale(int numerator, int denominator, RoundingMode mode) =>
        Of(Rounding.Divide(Value * numerator, denominator, mode));

    /// <inheritdoc />
    public int CompareTo(Percent other) => Value.CompareTo(other.Value);

    /// <inheritdoc />
    public override string ToString() => $"{Value}%";

    public static bool operator <(Percent left, Percent right) => left.Value < right.Value;
    public static bool operator >(Percent left, Percent right) => left.Value > right.Value;
    public static bool operator <=(Percent left, Percent right) => left.Value <= right.Value;
    public static bool operator >=(Percent left, Percent right) => left.Value >= right.Value;
}
