namespace Brp.Rules.Wealth;

/// <summary>
/// The data-defined values for Ch 3: Skills, "Status Skill, Social Status, &amp; Character Wealth"
/// (p.51) that a caller reads to turn a character's <c>Status</c> rating into a
/// <see cref="WealthLevel"/> (AGENTS.md invariant 7: rules values are data, not constants). Loaded
/// from <c>wealth-ruleset.json</c> by <c>Brp.Data.NoirWealthRuleset.Load()</c>. See
/// <c>docs/decisions/0030-money-and-wealth-levels.md</c>.
/// <para>
/// Deliberately does not model starting cash amounts, prices, or purchasing power -- Issue #229
/// keeps Money and Wealth "a clean abstraction... that suits a game where the PI's finances are a
/// story element" (<c>orc-scope-filter.md</c> Ch 8, line 129), not a full economy simulation.
/// </para>
/// </summary>
public sealed class WealthRuleset
{
    /// <summary>Creates a wealth ruleset from a data-defined table.</summary>
    public WealthRuleset(WealthTable table)
    {
        ArgumentNullException.ThrowIfNull(table);
        Table = table;
    }

    /// <summary>Ch 3, "Victorian/Western/Pulp/Modern Status" table (p.51), as a banded lookup by Status rating.</summary>
    public WealthTable Table { get; }

    /// <summary>
    /// The typical <see cref="WealthLevel"/> for a character with the given <c>Status</c> skill
    /// rating (the table's "Wealth Rating" column, p.51).
    /// </summary>
    public WealthLevel WealthLevelForStatus(int status) => Table.ForStatus(status).WealthRating;

    /// <summary>
    /// The highest <see cref="WealthLevel"/> a character with the given <c>Status</c> skill rating
    /// can hold (the table's "Wealth Cap" column, p.51).
    /// </summary>
    public WealthLevel MaximumWealthForStatus(int status) => Table.ForStatus(status).MaximumWealth;
}
