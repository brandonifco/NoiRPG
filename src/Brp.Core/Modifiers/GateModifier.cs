namespace Brp.Core.Modifiers;

/// <summary>Whether a gate forces an action to auto-succeed or forbids it outright.</summary>
public enum GateKind
{
    /// <summary>The action requires no roll and always succeeds.</summary>
    Automatic,

    /// <summary>The action cannot be attempted at all.</summary>
    Impossible,
}

/// <summary>
/// A modifier that short-circuits the pipeline instead of adjusting the base chance -- for
/// example a task with no possible failure state, or one that cannot be attempted at all under
/// the current circumstances. Per ADR 0007, a gate applies before any other stage, bypasses
/// Override/PermanentAdditive/Multiplicative/SituationalAdditive/Clamp entirely, and consumes
/// no entropy: no roll is drawn for a gated action, so a save-file replay is unaffected by
/// whether one was attempted.
/// </summary>
/// <param name="Source">What produced this gate.</param>
/// <param name="Kind">Whether the gated action always succeeds or cannot be attempted.</param>
public sealed record GateModifier(string Source, GateKind Kind) : Modifier(Source);
