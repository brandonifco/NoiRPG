namespace Brp.Core.Modifiers;

/// <summary>
/// An independent rational multiplier applied to the running chance -- Ch 5: System,
/// "Modifying Action Rolls". Expressed as a fraction rather than a fixed set of named values
/// because the book's multiplicative modifiers are not limited to doubling or halving.
/// Difficulty grades are a separate, non-stacking state represented by
/// <see cref="DifficultyModifier"/>, not by this type -- see ADR 0007.
/// </summary>
/// <param name="Source">What produced this multiplier.</param>
/// <param name="Numerator">The multiplier's numerator. Must be positive.</param>
/// <param name="Denominator">The multiplier's denominator. Must be positive.</param>
public sealed record MultiplicativeModifier(string Source, int Numerator, int Denominator) : Modifier(Source);
