namespace Brp.Rules.Gear;

/// <summary>
/// The set of defined weapons, armor, and cars for a ruleset, keyed by their stable ids. Loaded
/// from data by <c>Brp.Data.NoirGearRuleset.Load()</c> -- mirrors
/// <see cref="Brp.Core.Skills.SkillRegistry"/>, the Layer 2 pattern this issue follows.
/// </summary>
public sealed class GearRegistry
{
    private readonly Dictionary<WeaponId, WeaponDefinition> _weapons;
    private readonly Dictionary<ArmorId, ArmorDefinition> _armor;
    private readonly Dictionary<VehicleId, VehicleDefinition> _vehicles;
    private readonly Dictionary<string, WeaponDefinition> _weaponsByName;
    private readonly Dictionary<string, ArmorDefinition> _armorByName;

    /// <summary>Creates a registry from data-defined weapon, armor, and vehicle lists.</summary>
    public GearRegistry(
        IEnumerable<WeaponDefinition> weapons,
        IEnumerable<ArmorDefinition> armor,
        IEnumerable<VehicleDefinition> vehicles)
    {
        ArgumentNullException.ThrowIfNull(weapons);
        ArgumentNullException.ThrowIfNull(armor);
        ArgumentNullException.ThrowIfNull(vehicles);

        _weapons = weapons.ToDictionary(weapon => weapon.Id);
        _armor = armor.ToDictionary(item => item.Id);
        _vehicles = vehicles.ToDictionary(vehicle => vehicle.Id);
        if (_weapons.Count == 0)
        {
            throw new ArgumentException("At least one weapon definition is required.", nameof(weapons));
        }

        if (_armor.Count == 0)
        {
            throw new ArgumentException("At least one armor definition is required.", nameof(armor));
        }

        if (_vehicles.Count == 0)
        {
            throw new ArgumentException("At least one vehicle definition is required.", nameof(vehicles));
        }

        _weaponsByName = _weapons.Values.ToDictionary(weapon => weapon.Name, StringComparer.OrdinalIgnoreCase);
        _armorByName = _armor.Values.ToDictionary(item => item.Name, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Every defined weapon, by id.</summary>
    public IReadOnlyDictionary<WeaponId, WeaponDefinition> Weapons => _weapons;

    /// <summary>Every defined armor type, by id.</summary>
    public IReadOnlyDictionary<ArmorId, ArmorDefinition> Armor => _armor;

    /// <summary>Every defined car, by id.</summary>
    public IReadOnlyDictionary<VehicleId, VehicleDefinition> Vehicles => _vehicles;

    /// <summary>Looks up a weapon by id, throwing if it is not defined.</summary>
    public WeaponDefinition WeaponById(WeaponId id) => _weapons.TryGetValue(id, out var definition)
        ? definition
        : throw new KeyNotFoundException($"Unknown weapon '{id}'.");

    /// <summary>Looks up an armor type by id, throwing if it is not defined.</summary>
    public ArmorDefinition ArmorById(ArmorId id) => _armor.TryGetValue(id, out var definition)
        ? definition
        : throw new KeyNotFoundException($"Unknown armor '{id}'.");

    /// <summary>Looks up a car by id, throwing if it is not defined.</summary>
    public VehicleDefinition VehicleById(VehicleId id) => _vehicles.TryGetValue(id, out var definition)
        ? definition
        : throw new KeyNotFoundException($"Unknown vehicle '{id}'.");

    /// <summary>
    /// Resolves an <see cref="Characters.EquipmentItem"/>'s free-text name to its weapon and/or
    /// armor definition, matching by display name (case-insensitive). Returns an empty
    /// <see cref="GearLookup"/> -- not an exception -- when the name matches no definition,
    /// which is the expected outcome for plain gear such as a flashlight.
    /// </summary>
    public GearLookup Resolve(Characters.EquipmentItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        _weaponsByName.TryGetValue(item.Name, out var weapon);
        _armorByName.TryGetValue(item.Name, out var armor);
        return new GearLookup(weapon, armor);
    }
}
