namespace Brp.Rules.Gear;

/// <summary>
/// The set of defined breakable items for a ruleset, keyed by their stable ids. Loaded from data
/// by <c>Brp.Data.NoirItemHitPointsRuleset.Load()</c> -- mirrors <see cref="GearRegistry"/>, the
/// Layer 4 pattern this issue follows.
/// </summary>
public sealed class BreakableItemRegistry
{
    private readonly Dictionary<BreakableItemId, BreakableItemDefinition> _items;

    /// <summary>Creates a registry from a data-defined item list.</summary>
    public BreakableItemRegistry(IEnumerable<BreakableItemDefinition> items)
    {
        ArgumentNullException.ThrowIfNull(items);

        _items = items.ToDictionary(item => item.Id);
        if (_items.Count == 0)
        {
            throw new ArgumentException("At least one breakable item definition is required.", nameof(items));
        }
    }

    /// <summary>Every defined breakable item, by id.</summary>
    public IReadOnlyDictionary<BreakableItemId, BreakableItemDefinition> Items => _items;

    /// <summary>Looks up a breakable item by id, throwing if it is not defined.</summary>
    public BreakableItemDefinition ById(BreakableItemId id) => _items.TryGetValue(id, out var definition)
        ? definition
        : throw new KeyNotFoundException($"Unknown breakable item '{id}'.");
}
