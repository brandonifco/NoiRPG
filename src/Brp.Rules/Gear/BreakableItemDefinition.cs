namespace Brp.Rules.Gear;

/// <summary>
/// The SIZ, hit points, and armor value of a hand-picked inanimate object a noir detective might
/// need to force or bash through -- a door, a window, a lock. Sourced: Ch 8: Equipment, "General
/// Qualities of Objects" and "Damage to Inanimate Objects" (p.224), "Armor Value of Substances"
/// (p.224), and "SIZ of Common Objects" (pp.225-226). Hand-picked per `orc-scope-filter.md`, Ch 8,
/// line 137 ("Item SIZ/hit points for breaking doors, windows, locks"). See
/// <c>docs/decisions/0033-item-hit-points.md</c> for the per-entry sourcing, including the two
/// entries the book prints no exact row for (the glass door's armor value and the padlock
/// entirely), each marked as a house extrapolation there and in this entry's own
/// <see cref="Source"/> field.
/// </summary>
/// <param name="Id">The stable ruleset identifier.</param>
/// <param name="Name">The display name.</param>
/// <param name="Siz">
/// The object's SIZ, per the book's "SIZ of Common Objects" table (pp.225-226) or, where the book
/// prints no entry, the "Comparative Sizes" table (p.225) used to estimate one.
/// </param>
/// <param name="HitPoints">
/// The object's hit points. Ch 8, p.225: "a simple guideline for destroying objects is that an
/// average object has hit points roughly equivalent to its SIZ" -- every hand-picked entry here
/// follows that guideline, so this value always equals <see cref="Siz"/>, but is stored
/// separately (rather than derived) because it is the ruleset-data value AGENTS.md invariant 7
/// requires, not a hardcoded computation.
/// </param>
/// <param name="ArmorValue">
/// The object's starting armor value, from the "Armor Value of Substances" table (p.224) for the
/// material the object is made of, or a house extrapolation where the book prints no matching
/// substance -- see <see cref="Source"/>. Degrades as the object takes damage (p.224: "that many
/// damage points reduce its armor value") -- <see cref="Combat.BreakableItemResolver"/> applies
/// that degradation; this is only the starting value.
/// </param>
/// <param name="Source">The book table (and any house-extrapolation note) this entry was drawn from.</param>
public sealed record BreakableItemDefinition(
    BreakableItemId Id,
    string Name,
    int Siz,
    int HitPoints,
    int ArmorValue,
    string Source);
