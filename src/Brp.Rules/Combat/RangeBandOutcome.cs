using Brp.Core.Modifiers;
using Brp.Core.Primitives;

namespace Brp.Rules.Combat;

/// <summary>
/// What <see cref="RangeBandResolver.Resolve"/> produces for a given <see cref="RangeBand"/>.
/// A closed hierarchy rather than a single "modifiers + a flag" shape, so the non-stacking
/// guarantee for <see cref="ExclusiveOverride"/> is structural rather than advisory: unlike an
/// earlier revision, this type never exposes a bare <see cref="Modifier"/> for long range that a
/// caller could pull out of a list and recompose with other modifiers themselves (the exact
/// misuse that would produce the forbidden base &#247; 10 -- see Issue #21's post-review fix). A
/// caller can only get a resolved roll for an exclusive result via
/// <see cref="RangeBandResolver.Evaluate"/>, which builds the <see cref="OverrideModifier"/>
/// internally and discards everything else.
/// </summary>
public abstract record RangeBandOutcome(RangeBand Band)
{
    /// <summary>
    /// Point Blank, Normal, or Medium range: the band contributes zero or more modifiers that
    /// compose normally alongside a shot's other situational and permanent modifiers through
    /// <see cref="ModifierPipeline"/>.
    /// </summary>
    /// <param name="Band">The band this result was computed for.</param>
    /// <param name="Modifiers">The range-band modifier(s) to feed to the pipeline alongside the rest.</param>
    public sealed record Composable(RangeBand Band, IReadOnlyList<Modifier> Modifiers) : RangeBandOutcome(Band);

    /// <summary>
    /// Long range: per the settled decision on Issue #21, "1/5 normal skill chance" (Ch 6,
    /// "Long Range"; Ch 7, "Extended Range") is an override against the character's current
    /// rating -- with any <see cref="AdditiveKind.Permanent"/> modifiers already folded in, since
    /// Ch 5: System, "Situational Modifiers" (p.132) figures those into the rating before a
    /// Difficult/Easy grade (and, by the same logic, before this override) touches it -- not a
    /// multiplier stacked on top of whatever else the shot's modifier list carries. Situational
    /// and difficulty modifiers from elsewhere are discarded rather than composed with this
    /// result: composing them would let a Difficult grade halve the override again and arrive at
    /// base &#247; 10, which the book never sanctions.
    /// </summary>
    /// <param name="Band">Always <see cref="RangeBand.LongRange"/>.</param>
    /// <param name="Chance">
    /// The resolved override chance: <c>(baseChance + permanent additives) &#247; 5</c>, rounded
    /// up, with the divisor further dampened when aimed with targeting equipment. Exposed as a
    /// plain <see cref="Percent"/>, not a <see cref="Modifier"/>, so it cannot accidentally be
    /// dropped into a list and combined with unrelated modifiers by a caller bypassing
    /// <see cref="RangeBandResolver.Evaluate"/>.
    /// </param>
    /// <param name="Source">The label describing this override, for rendering.</param>
    public sealed record ExclusiveOverride(RangeBand Band, Percent Chance, string Source) : RangeBandOutcome(Band);
}
