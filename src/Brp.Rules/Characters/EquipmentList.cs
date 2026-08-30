namespace Brp.Rules.Characters;

/// <summary>
/// A character's equipment: a reference-only container of <see cref="EquipmentItem"/>
/// entries, carrying no gear stats (see <see cref="EquipmentItem"/>).
/// </summary>
public sealed class EquipmentList
{
    private readonly List<EquipmentItem> _items = [];

    /// <summary>Every item currently carried, in the order added.</summary>
    public IReadOnlyList<EquipmentItem> Items => _items;

    /// <summary>Adds an item.</summary>
    public void Add(EquipmentItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        _items.Add(item);
    }

    /// <summary>Removes an item (e.g. lost, sold, confiscated).</summary>
    public bool Remove(EquipmentItem item) => _items.Remove(item);
}
