namespace Brp.Core.Modifiers;

/// <summary>
/// The named stages a <see cref="ModifierPipeline"/> applies, in policy order. Additive
/// modifiers split into two stages -- Ch 5: System, "Modifying Action Rolls" figures a
/// permanent modifier into the rating before Difficult/Easy doubles or halves it, and applies a
/// situational modifier afterward, so its stated weight is not itself doubled or halved.
/// </summary>
public enum ModifierStage
{
    /// <summary>Automatic/Impossible short-circuits. Consumes no entropy.</summary>
    Gate,

    /// <summary>Replaces the running chance outright.</summary>
    Override,

    /// <summary>Flat +/- adjustments integral to the skill rating. Applied before Multiplicative.</summary>
    PermanentAdditive,

    /// <summary>Net difficulty state, plus independent rational multipliers.</summary>
    Multiplicative,

    /// <summary>Flat +/- adjustments describing the moment. Applied after Multiplicative.</summary>
    SituationalAdditive,

    /// <summary>Floors the result at zero. No ceiling.</summary>
    Clamp,
}
