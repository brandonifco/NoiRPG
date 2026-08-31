namespace Brp.Rules.Characters;

/// <summary>
/// A single entry in a character's wound list: a free-text note of what happened plus the hit
/// points that wound dealt. The damage amount, added in #111, is the prerequisite for the two
/// mechanics that classify a wound by its size: the Major Wounds trigger (Ch 6: Combat, "Major
/// Wounds", p.155 -- a single wound of half the character's total hit points or more) and First
/// Aid's per-wound healing cap (#109). <see cref="WoundTrack"/> lists wounds individually, so a
/// caller can compare any one wound, or a same-day sum of them, against
/// <see cref="Core.Abilities.AbilitySet.MajorWoundLevel"/>.
/// </summary>
/// <param name="Description">A free-text note of what happened, for the game layer to render.</param>
/// <param name="DamageAmount">
/// The hit points this wound dealt (non-negative). The figure the Major Wounds trigger and the
/// First Aid cap compare against -- not the character's remaining hit points, but the size of this
/// single blow.
/// </param>
public sealed record Wound(string Description, int DamageAmount);
