using Brp.Core.Modifiers;

namespace Brp.Rules.Combat;

/// <summary>
/// What <see cref="RangeBandResolver.Resolve"/> produces for a given <see cref="RangeBand"/>:
/// the modifier(s) representing the range effect, and whether they must be the <em>only</em>
/// modifiers a caller feeds to <see cref="ModifierPipeline"/> for this shot.
/// </summary>
/// <param name="Band">The band this result was computed for.</param>
/// <param name="Modifiers">The range-band modifier(s) to apply.</param>
/// <param name="IsExclusive">
/// True only for <see cref="RangeBand.LongRange"/>. Per the settled decision on Issue #21, long
/// range's "1/5 normal skill chance" (Ch 6, "Long Range"; Ch 7, "Extended Range") is an override
/// against the character's current, otherwise-unmodified rating -- not a multiplier stacked on
/// top of whatever else the shot's modifier list carries. When true, a caller must resolve the
/// roll against <em>only</em> <see cref="Modifiers"/>, discarding any other pending modifiers
/// for that shot (darkness, firing into combat, and so on): combining them would let a Difficult
/// grade from elsewhere halve the override again, arriving at base ÷ 10, which the book never
/// sanctions. See <see cref="RangeBandResolver.Evaluate"/> for the caller that does this.
/// </param>
public sealed record RangeBandModifiers(RangeBand Band, IReadOnlyList<Modifier> Modifiers, bool IsExclusive)
{
    /// <summary>A result that composes normally alongside a shot's other modifiers.</summary>
    public static RangeBandModifiers NonExclusive(RangeBand band, IReadOnlyList<Modifier> modifiers) =>
        new(band, modifiers, IsExclusive: false);

    /// <summary>A result that must replace a shot's other modifiers outright. See <see cref="IsExclusive"/>.</summary>
    public static RangeBandModifiers Exclusive(RangeBand band, IReadOnlyList<Modifier> modifiers) =>
        new(band, modifiers, IsExclusive: true);
}
