namespace Brp.Rules.Combat;

/// <summary>
/// One of the four printed D100 fumble tables of Ch 6: Combat (pp.148-149), as an immutable, banded
/// D100 lookup. Mirrors <see cref="IllnessSeverityTable"/> in shape. The validating constructor
/// pins the invariants that make a transcription error surface as a failure rather than a silent
/// misread: the rows must tile the whole <c>[1, 100]</c> percentile range with no gap or overlap,
/// and every "use result NN-NN" fallback must name a band an actual row covers.
/// </summary>
public sealed class FumbleConsequenceTable
{
    private readonly List<FumbleConsequenceRow> _rows;

    /// <summary>Creates a table from ordered ruleset rows.</summary>
    /// <param name="table">Which of the four tables this is.</param>
    /// <param name="rows">The printed rows, in ascending roll order.</param>
    public FumbleConsequenceTable(FumbleTable table, IEnumerable<FumbleConsequenceRow> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);
        Table = table;
        _rows = rows.ToList();

        if (_rows.Count == 0)
        {
            throw new ArgumentException($"Fumble table '{table}' must contain at least one row.", nameof(rows));
        }

        // The rows must tile [1, 100] exactly: each starts where the previous ended, no gap, no
        // overlap. A dropped or mistyped band shows up here instead of as a roll that silently
        // matches the wrong row (or none).
        var expectedNext = 1;
        foreach (var row in _rows)
        {
            if (row.MinimumRoll != expectedNext)
            {
                throw new ArgumentException(
                    $"Fumble table '{table}' has a gap or overlap: expected the next row to start at " +
                    $"{expectedNext} but it starts at {row.MinimumRoll}.",
                    nameof(rows));
            }

            if (row.MaximumRoll < row.MinimumRoll || row.MaximumRoll > 100)
            {
                throw new ArgumentException(
                    $"Fumble table '{table}' has an invalid band {row.MinimumRoll}-{row.MaximumRoll}.",
                    nameof(rows));
            }

            expectedNext = row.MaximumRoll + 1;
        }

        if (expectedNext != 101)
        {
            throw new ArgumentException(
                $"Fumble table '{table}' does not cover the full 1-100 range (stops at {expectedNext - 1}).",
                nameof(rows));
        }

        // Every fallback must point at a band a row actually covers -- "use result 41-50" has to be a
        // real row, or the resolver would have nothing to resolve the fallback to.
        foreach (var row in _rows.Where(r => r.Fallback is not null))
        {
            var fallback = row.Fallback!;
            if (!_rows.Any(r => r.MinimumRoll == fallback.MinimumRoll && r.MaximumRoll == fallback.MaximumRoll))
            {
                throw new ArgumentException(
                    $"Fumble table '{table}' row {row.MinimumRoll}-{row.MaximumRoll} names fallback result " +
                    $"{fallback.MinimumRoll}-{fallback.MaximumRoll}, which is not a row on this table.",
                    nameof(rows));
            }
        }
    }

    /// <summary>Which of the four tables this is.</summary>
    public FumbleTable Table { get; }

    /// <summary>Every printed row, in ascending roll order.</summary>
    public IReadOnlyList<FumbleConsequenceRow> Rows => _rows;

    /// <summary>Returns the row that covers the given D100 result (1-100, with 00 read as 100).</summary>
    public FumbleConsequenceRow ForRoll(int roll)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(roll, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(roll, 100);
        return _rows.Single(row => row.Contains(roll));
    }
}
