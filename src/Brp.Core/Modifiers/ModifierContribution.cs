using Brp.Core.Primitives;

namespace Brp.Core.Modifiers;

/// <summary>
/// One recorded step in a <see cref="ModifierChain"/>'s derivation: the running chance
/// immediately after this step was applied, and a human-readable description of what caused it.
/// Recording every step, not just the final total, is what lets
/// <see cref="ModifierChain.Render"/> explain itself.
/// </summary>
/// <param name="Source">The modifier source(s) responsible for this step.</param>
/// <param name="Description">A short rendering of what happened, e.g. "firing into combat -20%".</param>
/// <param name="ResultingChance">The running chance immediately after this step.</param>
public sealed record ModifierContribution(string Source, string Description, Percent ResultingChance);
