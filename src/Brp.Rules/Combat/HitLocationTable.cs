namespace Brp.Rules.Combat;

/// <summary>
/// Data-backed lookup for Ch 6: Combat, "Hit Locations" (p.145), the D20 melee/missile hit-location
/// table. Mirrors <see cref="MajorWoundTable"/>.
/// <para>
/// <strong>A printed-table erratum, corrected here.</strong> The book prints the "Left Leg" row as
/// D20 5-8 and the very next row, "Abdomen", as D20 8-11 -- the two ranges share the value 8, which
/// is impossible for a table that must partition 1-20 exactly once each. Verified against the PDF's
/// glyph bounding boxes (not a whitespace-alignment artifact -- see
/// <c>docs/source-handling.md</c>'s escalation recipe): the source page literally prints the digit
/// "8" twice. This engine implements 9-11 for Abdomen, not the printed 8-11 -- justified not by
/// face-counting alone (5-7/8-11 sums to 20 just as well as 5-8/9-11 does, so that argument alone
/// cannot pick a side) but because the printed Left Leg row, 5-8, is itself clean and unambiguous;
/// the two legs are otherwise printed symmetrically (Right Leg 1-4, Left Leg 5-8, four faces each);
/// and the canonical BRP humanoid hit-location table this one descends from gives Abdomen as 09-11.
/// See <c>docs/decisions/0024-hit-locations.md</c> ("The D20 hit-location table") for the full
/// argument, and <c>NoirHitLocationRulesetTests</c>, which pins both the printed misprint and the
/// corrected value it never uses.
/// </para>
/// </summary>
public sealed class HitLocationTable
{
    private readonly List<HitLocationTableRow> _rows;

    /// <summary>Creates a table from ordered ruleset rows.</summary>
    public HitLocationTable(IEnumerable<HitLocationTableRow> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);
        _rows = rows.ToList();
        if (_rows.Count == 0)
        {
            throw new ArgumentException("Hit location table must contain at least one row.", nameof(rows));
        }
    }

    /// <summary>Every printed row, in load order.</summary>
    public IReadOnlyList<HitLocationTableRow> Rows => _rows;

    /// <summary>Returns the row that covers the given D20 result.</summary>
    public HitLocationTableRow ForRoll(int d20)
    {
        if (d20 is < 1 or > 20)
        {
            throw new ArgumentOutOfRangeException(nameof(d20), d20, "A D20 result is in [1, 20].");
        }

        return _rows.SingleOrDefault(row => row.Contains(d20))
            ?? throw new ArgumentOutOfRangeException(nameof(d20), d20, "No hit location row covers this result.");
    }
}
