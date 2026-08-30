namespace Brp.Rules.Characters;

/// <summary>
/// A reference to an item a character carries. Deliberately name-only: gear stats (weapon
/// damage, armor value, ranges) are Layer 4/8 (#21) and do not exist yet. This lets
/// <see cref="Character"/> carry "what the player is holding" as a list of names without
/// inventing a stats schema this issue was not asked to design.
/// </summary>
/// <param name="Name">A free-text label for the item, e.g. "revolver" or "flashlight".</param>
public sealed record EquipmentItem(string Name);
