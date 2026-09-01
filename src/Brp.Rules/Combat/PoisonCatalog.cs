namespace Brp.Rules.Combat;

/// <summary>
/// The set of named poison/drug entries from Ch 8: Equipment, the Sample Poisons Table
/// (p.221), keyed by stable id. Loaded from data by <c>Brp.Data.NoirPoisonCatalog.Load()</c> --
/// mirrors <see cref="Brp.Rules.Gear.GearRegistry"/>, the same "data catalog, not a mechanic"
/// pattern. Each entry's <see cref="PoisonCatalogEntry.Potency"/> is fed straight into the
/// existing <see cref="PoisonResolver"/>/<see cref="PoisonRuleset"/> POT-vs-CON path; this
/// catalog adds no new resolution logic.
/// </summary>
public sealed class PoisonCatalog
{
    private readonly Dictionary<PoisonId, PoisonCatalogEntry> _entries;

    /// <summary>Creates a catalog from a data-defined entry list.</summary>
    public PoisonCatalog(IEnumerable<PoisonCatalogEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);

        _entries = entries.ToDictionary(entry => entry.Id);
        if (_entries.Count == 0)
        {
            throw new ArgumentException("At least one poison catalog entry is required.", nameof(entries));
        }
    }

    /// <summary>Every defined poison/drug entry, by id.</summary>
    public IReadOnlyDictionary<PoisonId, PoisonCatalogEntry> Entries => _entries;

    /// <summary>Looks up a poison/drug entry by id, throwing if it is not defined.</summary>
    public PoisonCatalogEntry EntryById(PoisonId id) => _entries.TryGetValue(id, out var entry)
        ? entry
        : throw new KeyNotFoundException($"Unknown poison catalog entry '{id}'.");
}
