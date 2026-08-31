using Brp.Core.Dice;

namespace Brp.Rules.Combat;

/// <summary>
/// One printed row of one of the four D100 fumble tables of Ch 6: Combat (pp.148-149), as a banded
/// D100 lookup. Mirrors <see cref="Core.Abilities.DamageModifierBand"/> / <see cref="IllnessSeverityBand"/>:
/// an inclusive roll band, plus the structured pieces of the printed effect. A percentile roll of
/// <c>00</c> is read as <c>100</c> (per <see cref="Core.Randomness.IEntropySource.NextD100"/>), so the
/// tables' "00" row is stored with bounds of 100. Every numeric rules value the row carries lives in
/// ruleset data (AGENTS.md invariant 7); this record only shapes it.
/// </summary>
/// <param name="MinimumRoll">The lowest D100 result this row covers (1-100).</param>
/// <param name="MaximumRoll">The highest D100 result this row covers (1-100), with 00 stored as 100.</param>
/// <param name="Kind">Which kind of consequence this row inflicts.</param>
/// <param name="Effect">The exact printed effect text, kept for citation and row identity.</param>
/// <param name="Amount">
/// The row's rolled quantity, if any -- combat rounds lost (1D3), meters thrown/scattered (1D10,
/// 1D6-1), or weapon hit points lost (1D10, 1D6). Left unrolled: the resolver returns it for a caller
/// to roll and apply, never rolling it here.
/// </param>
/// <param name="Magnitude">
/// The row's flat numeric value, if any -- the vision skill penalty (-30), the twisted-ankle
/// movement penalty (-1), or the strain hit-point loss (1).
/// </param>
/// <param name="HitGrade">
/// The grade of hit the row inflicts, if any -- on an ally (<see cref="FumbleEffectKind.HitNearestAlly"/>),
/// on oneself against a hard surface (<see cref="FumbleEffectKind.HitHardSurface"/>), or from a foe
/// left an opening (<see cref="FumbleEffectKind.FoeAutomaticHit"/>). Reuses <see cref="LandedGrade"/>
/// (Normal/Special/Critical) -- the same hit-grade vocabulary the attack/defense matrix produces.
/// </param>
/// <param name="Fallback">
/// The printed "or use result NN-NN" alternative, if any, and the caller-known condition that
/// selects it (no ally nearby; weapon has no hit points). Present on the hit-ally rows and the one
/// missile weapon-hit-point row.
/// </param>
/// <param name="RerollCount">
/// For a <see cref="FumbleEffectKind.Reroll"/> row, how many further rolls to make on this table
/// (two for "blow it," three for "blow it badly").
/// </param>
public sealed record FumbleConsequenceRow(
    int MinimumRoll,
    int MaximumRoll,
    FumbleEffectKind Kind,
    string Effect,
    DiceExpression? Amount = null,
    int? Magnitude = null,
    LandedGrade? HitGrade = null,
    FumbleFallback? Fallback = null,
    int? RerollCount = null)
{
    /// <summary>Whether this row covers the given D100 result (1-100, with 00 read as 100).</summary>
    public bool Contains(int roll) => roll >= MinimumRoll && roll <= MaximumRoll;
}

/// <summary>
/// A fumble row's printed "or use result NN-NN" alternative, per Ch 6 (pp.148-149), and the
/// caller-known condition under which it applies instead of the row's primary effect. The range
/// names another row on the same table (by that row's own roll band); the resolver looks it up
/// without consuming entropy -- "use result 41-50" applies that row's effect, it does not reroll.
/// </summary>
/// <param name="Condition">The caller fact that selects the fallback over the primary effect.</param>
/// <param name="MinimumRoll">The low end of the referenced result band.</param>
/// <param name="MaximumRoll">The high end of the referenced result band.</param>
public sealed record FumbleFallback(FumbleFallbackCondition Condition, int MinimumRoll, int MaximumRoll);
