namespace Brp.Rules.Combat;

/// <summary>
/// How the defender's armor value applies to a landed hit, per the attack/defense matrix's
/// per-cell result text (Ch 6: Combat, "Attack and Defense Matrix", p.147). Kept close to the
/// book's own wording rather than collapsed into a single "ignore armor" flag: the printed
/// matrix uses two distinct phrases -- "armor value is bypassed" (Critical attack vs. Failed
/// defense) and "armor value does not apply" (Critical attack vs. Fumbled defense) -- for cells
/// that may or may not be the identical rule; see <c>docs/decisions/0016-attack-defense-matrix.md</c>
/// for why both are preserved rather than merged. The arithmetic of subtracting armor is piece
/// D's concern; this enum only names which treatment applies.
/// </summary>
public enum ArmorTreatment
{
    /// <summary>No damage lands, so armor is not in question.</summary>
    NotApplicable,

    /// <summary>The defender's armor value is subtracted from the damage rolled.</summary>
    Subtracted,

    /// <summary>The defender's armor value is bypassed.</summary>
    Bypassed,

    /// <summary>The defender's armor value does not apply.</summary>
    DoesNotApply,
}
