namespace Brp.Rules.Characters;

/// <summary>
/// A single entry in a character's wound list. Structure only: this issue (#40) builds the
/// container a <see cref="Character"/> carries, not the mechanics that populate or resolve it
/// (First Aid per wound, Major Wounds, hit locations) -- those are Layer 4 (#21).
/// Deliberately minimal so nothing here presumes a mechanic that has not been decided yet.
/// </summary>
/// <param name="Description">A free-text note of what happened, for the game layer to render.</param>
public sealed record Wound(string Description);
