namespace Brp.Rules.Combat;

/// <summary>
/// Data-backed lookup for Ch 7: Spot Rules, "Illness Severity Table" (p.170). The table's rows
/// (0 None, 1 Mild, 2 Acute, 3 Severe, 4+ Terminal) are represented as ruleset bands, cross-indexed
/// by the number of times a character has failed their CON recovery roll. Mirrors
/// <see cref="Core.Abilities.DamageModifierTable"/>.
/// </summary>
public sealed class IllnessSeverityTable
{
    private readonly List<IllnessSeverityBand> _bands;

    /// <summary>Creates a table from ordered ruleset rows.</summary>
    public IllnessSeverityTable(IEnumerable<IllnessSeverityBand> bands)
    {
        ArgumentNullException.ThrowIfNull(bands);
        _bands = bands.ToList();
        if (_bands.Count == 0)
        {
            throw new ArgumentException("Illness severity table must contain at least one band.", nameof(bands));
        }
    }

    /// <summary>Every printed row, in load order.</summary>
    public IReadOnlyList<IllnessSeverityBand> Bands => _bands;

    /// <summary>Returns the row that covers the given number of failed CON recovery rolls.</summary>
    public IllnessSeverityBand ForFailures(int failures)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(failures);
        return _bands.SingleOrDefault(band => band.Contains(failures))
            ?? throw new ArgumentOutOfRangeException(
                nameof(failures), failures, "No illness severity band covers this failure count.");
    }
}
