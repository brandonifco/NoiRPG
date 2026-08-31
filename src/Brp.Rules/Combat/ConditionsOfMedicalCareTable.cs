using Brp.Core.Contests;

namespace Brp.Rules.Combat;

/// <summary>
/// Data-backed lookup for Ch 6: Combat, "Conditions of Medical Care" (p.157). The three printed rows
/// (poor, decent, excellent) are represented as <see cref="MedicalCareRow"/> entries, cross-indexed by
/// <see cref="MedicalCareTier"/>. Mirrors <see cref="MajorWoundTable"/> / <see cref="IllnessSeverityTable"/>.
/// </summary>
public sealed class ConditionsOfMedicalCareTable
{
    private readonly List<MedicalCareRow> _rows;

    /// <summary>Creates a table from ordered ruleset rows (one per care tier).</summary>
    public ConditionsOfMedicalCareTable(IEnumerable<MedicalCareRow> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);
        _rows = rows.ToList();
        if (_rows.Count == 0)
        {
            throw new ArgumentException("Conditions of medical care table must contain at least one row.", nameof(rows));
        }

        var duplicateTier = _rows.GroupBy(row => row.Tier).FirstOrDefault(group => group.Count() > 1);
        if (duplicateTier is not null)
        {
            throw new ArgumentException(
                $"Conditions of medical care table has more than one row for tier '{duplicateTier.Key}'.", nameof(rows));
        }
    }

    /// <summary>Every printed row, in load order.</summary>
    public IReadOnlyList<MedicalCareRow> Rows => _rows;

    /// <summary>Returns the row for the given care tier.</summary>
    public MedicalCareRow ForTier(MedicalCareTier tier) =>
        _rows.SingleOrDefault(row => row.Tier == tier)
            ?? throw new ArgumentOutOfRangeException(nameof(tier), tier, "No conditions-of-care row covers this tier.");
}
