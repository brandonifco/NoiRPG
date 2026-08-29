namespace Brp.Core.Modifiers;

/// <summary>
/// One situational adjustment to a skill's base chance, per Ch 5: System, "Modifying Action
/// Rolls". Every modifier carries a <see cref="Source"/> label because the design pillar in
/// <c>noir-rpg-framework.md</c> is that the rating shown to the player is the real probability
/// -- a chain of anonymous numbers cannot be explained back to the table, but a chain of
/// labelled ones can. See ADR 0007 for the ordering and difficulty-stacking rules that these
/// feed into.
/// </summary>
/// <param name="Source">
/// A short label identifying what produced this modifier (e.g. "darkness", "firing into
/// combat"). Rendered verbatim in <see cref="ModifierChain.Render"/>.
/// </param>
public abstract record Modifier(string Source);
