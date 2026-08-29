namespace Brp.Core.Modifiers;

/// <summary>
/// Distinguishes a modifier that is integral to the skill from one that reflects the situation
/// the roll is attempted in. Per Ch 5: System, "Modifying Action Rolls", a permanent modifier --
/// one built into the rating itself, such as specialized training -- is figured in before a
/// Difficult or Easy grade doubles or halves the chance. A situational modifier -- conditions of
/// the moment, such as darkness or firing into a melee -- is applied afterward, specifically so
/// that its stated weight is not itself doubled or halved by the difficulty grade. See ADR 0007
/// for the resulting stage order.
/// </summary>
public enum AdditiveKind
{
    /// <summary>Applied after the difficulty multiplier, so its stated weight survives intact.</summary>
    Situational,

    /// <summary>Applied before the difficulty multiplier, as part of the rating itself.</summary>
    Permanent,
}

/// <summary>
/// A flat percentage adjustment to the running chance -- a penalty or bonus, e.g. firing into
/// combat at -20% (Ch 7, "Firing Into Combat"). Situational by default: the large majority of
/// additive modifiers describe the moment rather than the skill -- the book's
/// situational-modifier tables are the typical case. A caller must opt in to
/// <see cref="AdditiveKind.Permanent"/> for the exceptional case of a modifier integral to the
/// rating itself.
/// </summary>
/// <param name="Source">What produced this adjustment, e.g. "firing into combat".</param>
/// <param name="Delta">The signed percentage-point change. Negative values are penalties.</param>
/// <param name="Kind">Whether this is figured in before or after the difficulty multiplier.</param>
public sealed record AdditiveModifier(string Source, int Delta, AdditiveKind Kind = AdditiveKind.Situational)
    : Modifier(Source);
