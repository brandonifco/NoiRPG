using Brp.Core.Primitives;

namespace Brp.Core.Modifiers;

/// <summary>
/// A modifier that replaces the running chance outright rather than adjusting it -- for example
/// a shield's flat parry chance against missiles (Ch 7, Spot Rules). Per ADR 0007 this stage
/// runs immediately after Gate and before PermanentAdditive/Multiplicative/SituationalAdditive,
/// so later stages adjust the replacement value rather than the original base.
/// </summary>
/// <param name="Source">What produced this override, e.g. "shield parry".</param>
/// <param name="Value">The chance to replace the running value with.</param>
public sealed record OverrideModifier(string Source, Percent Value) : Modifier(Source);
