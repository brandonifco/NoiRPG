namespace Brp.Rules.Combat;

/// <summary>
/// Data-backed lookup for Ch 6: Combat, "Major Wounds Table" (pp.155-156). The fifteen printed rows
/// (01-10 through 00) are represented as ruleset bands, cross-indexed by a 1D100 result (a printed
/// 00 read as 100, per <see cref="Core.Randomness.IEntropySource.NextD100"/>). Mirrors
/// <see cref="IllnessSeverityTable"/> / <see cref="Core.Abilities.DamageModifierTable"/>.
/// </summary>
public sealed class MajorWoundTable
{
    private readonly List<MajorWoundRow> _rows;

    /// <summary>Creates a table from ordered ruleset rows.</summary>
    public MajorWoundTable(IEnumerable<MajorWoundRow> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);
        _rows = rows.ToList();
        if (_rows.Count == 0)
        {
            throw new ArgumentException("Major wound table must contain at least one row.", nameof(rows));
        }
    }

    /// <summary>Every printed row, in load order.</summary>
    public IReadOnlyList<MajorWoundRow> Rows => _rows;

    /// <summary>Returns the row that covers the given 1D100 result (a printed 00 read as 100).</summary>
    public MajorWoundRow ForRoll(int d100)
    {
        if (d100 is < 1 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(d100), d100, "A 1D100 result is in [1, 100].");
        }

        return _rows.SingleOrDefault(row => row.Contains(d100))
            ?? throw new ArgumentOutOfRangeException(nameof(d100), d100, "No major wound row covers this result.");
    }
}
