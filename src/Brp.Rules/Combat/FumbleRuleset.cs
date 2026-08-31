namespace Brp.Rules.Combat;

/// <summary>
/// The four printed D100 fumble consequence tables of Ch 6: Combat (pp.148-149), keyed by
/// <see cref="FumbleTable"/>. Immutable; the validating constructor requires all four tables to be
/// present. Loaded from <c>Brp.Data/fumble-ruleset.json</c> by <c>NoirFumbleRuleset</c> (AGENTS.md
/// invariant 7). See <c>docs/decisions/0020-fumble-tables.md</c>.
/// </summary>
public sealed class FumbleRuleset
{
    private readonly Dictionary<FumbleTable, FumbleConsequenceTable> _tables;

    /// <summary>Creates a ruleset from the four loaded tables.</summary>
    public FumbleRuleset(IEnumerable<FumbleConsequenceTable> tables)
    {
        ArgumentNullException.ThrowIfNull(tables);
        _tables = tables.ToDictionary(table => table.Table);

        foreach (var expected in Enum.GetValues<FumbleTable>())
        {
            if (!_tables.ContainsKey(expected))
            {
                throw new ArgumentException($"The fumble ruleset is missing the '{expected}' table.", nameof(tables));
            }
        }

        if (_tables.Count != Enum.GetValues<FumbleTable>().Length)
        {
            throw new ArgumentException("The fumble ruleset has a duplicate table.", nameof(tables));
        }
    }

    /// <summary>Returns the consequence table for the given kind.</summary>
    public FumbleConsequenceTable ForTable(FumbleTable table) => _tables[table];
}
