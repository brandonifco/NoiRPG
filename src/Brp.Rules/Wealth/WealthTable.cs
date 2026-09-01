namespace Brp.Rules.Wealth;

/// <summary>
/// Data-backed lookup for Ch 3: Skills, "Status Skill, Social Status, &amp; Character Wealth", the
/// "Victorian/Western/Pulp/Modern Status" table (p.51). The six printed rows (01-14 through 96-00)
/// are represented as ruleset bands, cross-indexed by a character's <c>Status</c> skill rating.
/// Mirrors <see cref="Combat.MajorWoundTable"/> / <see cref="Combat.IllnessSeverityTable"/>.
/// </summary>
public sealed class WealthTable
{
    private readonly List<WealthBand> _bands;

    /// <summary>Creates a table from ordered ruleset rows.</summary>
    public WealthTable(IEnumerable<WealthBand> bands)
    {
        ArgumentNullException.ThrowIfNull(bands);
        _bands = bands.ToList();
        if (_bands.Count == 0)
        {
            throw new ArgumentException("Wealth table must contain at least one row.", nameof(bands));
        }
    }

    /// <summary>Every printed row, in load order.</summary>
    public IReadOnlyList<WealthBand> Bands => _bands;

    /// <summary>
    /// Returns the row that covers the given Status skill rating (a printed 00 read as 100, matching
    /// the book's own d100 convention).
    /// </summary>
    public WealthBand ForStatus(int status)
    {
        if (status is < 1 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(status), status, "A Status rating is in [1, 100].");
        }

        return _bands.SingleOrDefault(band => band.Contains(status))
            ?? throw new ArgumentOutOfRangeException(nameof(status), status, "No wealth band covers this Status rating.");
    }
}
