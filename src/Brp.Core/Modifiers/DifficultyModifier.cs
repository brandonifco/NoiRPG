namespace Brp.Core.Modifiers;

/// <summary>Which way a difficulty grade pushes the chance.</summary>
public enum DifficultyDirection
{
    /// <summary>An Easy grade: contributes toward a net doubling.</summary>
    Easier,

    /// <summary>A Difficult grade: contributes toward a net halving.</summary>
    Harder,
}

/// <summary>
/// A difficulty-grade contribution -- Ch 5: System, "Modifying Action Rolls". Per ADR 0007,
/// difficulty is a <em>state</em>, not a stack: any number of <see cref="DifficultyDirection.Harder"/>
/// sources collapse into one halving, any number of <see cref="DifficultyDirection.Easier"/>
/// sources collapse into one doubling, and the two cancel pairwise.
/// <para>
/// Deliberately carries no numerator or denominator of its own. The multiplier that a net
/// difficulty grade actually applies is declared once, as data, on <see cref="ModifierPolicy"/>,
/// and read from whichever policy a given <see cref="ModifierPipeline.Evaluate"/> call uses --
/// the same precedent as <see cref="Resolution.ResolutionPolicy"/> owning the resolver's
/// threshold constants rather than the roll owning them. An earlier revision let a difficulty
/// contribution carry its own ratio, which meant an off-model ratio (e.g. 1/5) was silently
/// discarded in favor of a hardcoded halving; removing the fields removes the possibility.
/// </para>
/// </summary>
/// <param name="Source">What produced this grade, e.g. "darkness" or "firing into a melee".</param>
/// <param name="Direction">Whether this pushes toward Easy or toward Difficult.</param>
public sealed record DifficultyModifier(string Source, DifficultyDirection Direction) : Modifier(Source)
{
    /// <summary>An Easy condition.</summary>
    public static DifficultyModifier Easy(string source) => new(source, DifficultyDirection.Easier);

    /// <summary>A Difficult condition.</summary>
    public static DifficultyModifier Difficult(string source) => new(source, DifficultyDirection.Harder);
}
