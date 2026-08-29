using Brp.Core.Dice;

namespace Brp.Core.Abilities;

/// <summary>
/// Data-backed lookup for Ch 2: Characters, "Damage Modifier Table" (p.13). The table's
/// irregular lower bands and open-ended +16 continuation are represented as ruleset rows.
/// </summary>
public sealed class DamageModifierTable
{
    private readonly List<DamageModifierBand> _bands;

    /// <summary>Creates a table from ordered ruleset rows.</summary>
    public DamageModifierTable(IEnumerable<DamageModifierBand> bands)
    {
        ArgumentNullException.ThrowIfNull(bands);
        _bands = bands.ToList();
        if (_bands.Count == 0)
        {
            throw new ArgumentException("Damage modifier table must contain at least one band.", nameof(bands));
        }
    }

    /// <summary>Returns the modifier for a STR+SIZ total, or null for the printed None row.</summary>
    public DiceExpression? ForTotal(int strengthPlusSize)
    {
        var band = _bands.SingleOrDefault(b => b.Contains(strengthPlusSize));
        if (band is null)
        {
            throw new ArgumentOutOfRangeException(nameof(strengthPlusSize));
        }

        return band.Continuation?.ExpressionAt(strengthPlusSize, band.Minimum) ?? band.Modifier;
    }
}
