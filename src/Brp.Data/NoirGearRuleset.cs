using System.Text.Json;
using Brp.Core.Dice;
using Brp.Core.Skills;
using Brp.Rules.Gear;

namespace Brp.Data;

/// <summary>
/// Loads NoiRPG's Layer 4 weapon, armor, and car lists from embedded JSON. The source is Ch 8:
/// Equipment, the Modern Melee Weapons, Modern Missile Weapons, and Modern Armor tables
/// (pp.201-202, 207) plus the Primitive Melee Weapons table (p.196) for the two Club entries, and
/// the Autos, Trucks, Trains &amp; Tanks table (p.219) for the three in-scope cars -- see each
/// entry's own <c>source</c> field in the ruleset JSON for the exact citation. Hand-picked to the
/// modern noir subset per `orc-scope-filter.md`, Ch 8, and recorded in
/// <c>docs/decisions/0012-gear-definitions.md</c>.
/// </summary>
public static class NoirGearRuleset
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>Loads a new, immutable gear registry from the shipped data.</summary>
    public static GearRegistry Load()
    {
        var weapons = LoadWeapons();
        var armor = LoadArmor();
        var vehicles = LoadVehicles();
        return new GearRegistry(weapons, armor, vehicles);
    }

    private static List<WeaponDefinition> LoadWeapons()
    {
        var assembly = typeof(NoirGearRuleset).Assembly;
        using var stream = assembly.GetManifestResourceStream("Brp.Data.weapon-ruleset.json")
            ?? throw new InvalidOperationException("The weapon ruleset data resource is missing.");
        var data = JsonSerializer.Deserialize<WeaponRulesetData>(stream, SerializerOptions)
            ?? throw new InvalidOperationException("The weapon ruleset data is empty.");

        return data.Weapons.Select(ToWeaponDefinition).ToList();
    }

    private static List<ArmorDefinition> LoadArmor()
    {
        var assembly = typeof(NoirGearRuleset).Assembly;
        using var stream = assembly.GetManifestResourceStream("Brp.Data.armor-ruleset.json")
            ?? throw new InvalidOperationException("The armor ruleset data resource is missing.");
        var data = JsonSerializer.Deserialize<ArmorRulesetData>(stream, SerializerOptions)
            ?? throw new InvalidOperationException("The armor ruleset data is empty.");

        return data.Armor.Select(ToArmorDefinition).ToList();
    }

    private static List<VehicleDefinition> LoadVehicles()
    {
        var assembly = typeof(NoirGearRuleset).Assembly;
        using var stream = assembly.GetManifestResourceStream("Brp.Data.vehicle-ruleset.json")
            ?? throw new InvalidOperationException("The vehicle ruleset data resource is missing.");
        var data = JsonSerializer.Deserialize<VehicleRulesetData>(stream, SerializerOptions)
            ?? throw new InvalidOperationException("The vehicle ruleset data is empty.");

        return data.Vehicles.Select(ToVehicleDefinition).ToList();
    }

    private static WeaponDefinition ToWeaponDefinition(WeaponEntryData entry)
    {
        List<RangeIncrementDamage> damageByRange = entry.DamageByRange is null
            ? []
            : ToRangeIncrements(entry);

        FirearmProfile? firearm = entry.BaseRange is not { } baseRange
            ? null
            : new FirearmProfile(
                ListedRange: RawValue(baseRange),
                ListedRangeMeters: SingleNumber(baseRange),
                MalfunctionNumber: entry.MalfunctionNumber
                    ?? throw new InvalidOperationException($"Firearm '{entry.Id}' has a range but no malfunction number."),
                AmmoCapacity: RawValue(entry.AmmoCapacity
                    ?? throw new InvalidOperationException($"Firearm '{entry.Id}' has a range but no ammo capacity.")),
                AttacksPerRound: RawValue(entry.AttacksPerRound
                    ?? throw new InvalidOperationException($"Firearm '{entry.Id}' has a range but no attacks-per-round.")),
                BaseChance: entry.BaseChance
                    ?? throw new InvalidOperationException($"Firearm '{entry.Id}' has a range but no base chance."),
                ListedRangeWithoutScope: entry.BaseRangeWithoutScope,
                BaseChanceWithBipod: entry.BaseChanceWithBipod,
                BaseChanceWithoutBipod: entry.BaseChanceWithoutBipod);

        // For weapons whose damage falls off by range (shotguns), the "damage" field is the
        // book's own combined display string (e.g. "4D6/2D6/1D6"), not a rollable expression;
        // the closest-range increment is the representative single value.
        var damage = damageByRange.Count > 0 ? damageByRange[0].Damage : DiceExpression.Parse(entry.Damage);

        return new WeaponDefinition(
            Id: new WeaponId(entry.Id),
            Name: entry.Name,
            SkillId: new SkillId(entry.SkillId),
            WeaponClass: ToWeaponClass(entry.WeaponClass),
            Damage: damage,
            ApplyDamageBonus: entry.ApplyDamageBonus,
            DamageByRange: damageByRange,
            Firearm: firearm,
            SpecialDamageType: ToSpecialDamageType(entry.SpecialDamageType),
            Source: entry.Source);
    }

    private static List<RangeIncrementDamage> ToRangeIncrements(WeaponEntryData entry)
    {
        var ranges = ParseRangeList(entry.BaseRange
            ?? throw new InvalidOperationException($"Weapon '{entry.Id}' has damageByRange but no baseRange."));
        var damage = entry.DamageByRange!;
        var increments = new List<RangeIncrementDamage>();
        AddIfPresent(increments, ranges, 0, damage.Close);
        AddIfPresent(increments, ranges, 1, damage.Medium);
        AddIfPresent(increments, ranges, 2, damage.Long);
        return increments;
    }

    private static void AddIfPresent(List<RangeIncrementDamage> increments, List<int?> ranges, int index, string? damage)
    {
        if (damage is null)
        {
            return;
        }

        if (index >= ranges.Count || ranges[index] is not { } range)
        {
            throw new InvalidOperationException($"No numeric range for damage-by-range increment {index}.");
        }

        increments.Add(new RangeIncrementDamage(range, DiceExpression.Parse(damage)));
    }

    /// <summary>
    /// Parses a JSON range value into its "/"-separated numeric increments. A plain number
    /// yields a single-element list; a non-numeric increment (the sawed-off shotgun's trailing
    /// "—", meaning "not effective beyond 20 yards") is recorded as <see langword="null"/>.
    /// </summary>
    private static List<int?> ParseRangeList(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Number)
        {
            return [element.GetInt32()];
        }

        return element.GetString()!
            .Split('/')
            .Select(part => int.TryParse(part, out var value) ? value : (int?)null)
            .ToList();
    }

    private static string RawValue(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.Number => element.GetRawText(),
        JsonValueKind.String => element.GetString()!,
        _ => throw new InvalidOperationException($"Unsupported JSON value kind '{element.ValueKind}'."),
    };

    private static int? SingleNumber(JsonElement element) =>
        element.ValueKind == JsonValueKind.Number ? element.GetInt32() : null;

    private static SpecialDamageType ToSpecialDamageType(string value) => value switch
    {
        "bleeding" => SpecialDamageType.Bleeding,
        "crushing" => SpecialDamageType.Crushing,
        "entangling" => SpecialDamageType.Entangling,
        "impaling" => SpecialDamageType.Impaling,
        "knockback" => SpecialDamageType.Knockback,
        _ => throw new InvalidOperationException($"Unknown special damage type '{value}'."),
    };

    private static WeaponClass ToWeaponClass(string value) => value switch
    {
        "Brawl" => WeaponClass.Brawl,
        "Club" => WeaponClass.Club,
        "Dagger" => WeaponClass.Dagger,
        "Pistol" => WeaponClass.Pistol,
        "Revolver" => WeaponClass.Revolver,
        "Rifle" => WeaponClass.Rifle,
        "Shotgun" => WeaponClass.Shotgun,
        "Submachine Gun" => WeaponClass.SubmachineGun,
        _ => throw new InvalidOperationException($"Unknown weapon class '{value}'."),
    };

    private static ArmorDefinition ToArmorDefinition(ArmorEntryData entry)
    {
        var armorValue = entry.ArmorValue.ValueKind == JsonValueKind.Number
            ? ArmorValue.Flat(entry.ArmorValue.GetInt32())
            : ToArmorValue(entry.ArmorValue);

        return new ArmorDefinition(
            Id: new ArmorId(entry.Id),
            Name: entry.Name,
            ArmorValue: armorValue,
            SkillPenalty: new ArmorSkillPenalty(
                Enum.Parse<SkillCategory>(entry.SkillPenalty.Category, ignoreCase: true),
                entry.SkillPenalty.Value),
            HitLocations: entry.HitLocations,
            Note: entry.Note,
            Source: entry.Source);
    }

    private static ArmorValue ToArmorValue(JsonElement element)
    {
        var data = element.Deserialize<ArmorValueData>(SerializerOptions)
            ?? throw new InvalidOperationException("An armor value object requires melee and firearms values.");
        return new ArmorValue(data.Melee, data.Firearms);
    }

    private static VehicleDefinition ToVehicleDefinition(VehicleEntryData entry) => new(
        Id: new VehicleId(entry.Id),
        Name: entry.Name,
        SkillId: new SkillId(entry.SkillId),
        RatedSpeed: entry.RatedSpeed,
        Handling: entry.Handling,
        Acceleration: entry.Acceleration,
        MetersPerRound: entry.MetersPerRound,
        Armor: new ArmorValue(entry.Armor.Melee, entry.Armor.Firearms),
        Siz: entry.Siz,
        HitPoints: entry.HitPoints,
        Crew: entry.Crew,
        Passengers: entry.Passengers,
        Cargo: entry.Cargo,
        ValueTier: entry.ValueTier,
        Source: entry.Source);

    private sealed class WeaponRulesetData
    {
        public required List<WeaponEntryData> Weapons { get; init; }
    }

    private sealed class WeaponEntryData
    {
        public required string Id { get; init; }

        public required string Name { get; init; }

        public required string SkillId { get; init; }

        public required string Damage { get; init; }

        public required bool ApplyDamageBonus { get; init; }

        public required string WeaponClass { get; init; }

        public JsonElement? BaseRange { get; init; }

        public string? MalfunctionNumber { get; init; }

        public JsonElement? AmmoCapacity { get; init; }

        public JsonElement? AttacksPerRound { get; init; }

        public int? BaseChance { get; init; }

        public int? BaseRangeWithoutScope { get; init; }

        public int? BaseChanceWithBipod { get; init; }

        public int? BaseChanceWithoutBipod { get; init; }

        public DamageByRangeData? DamageByRange { get; init; }

        public required string SpecialDamageType { get; init; }

        public required string Source { get; init; }
    }

    private sealed class DamageByRangeData
    {
        public string? Close { get; init; }

        public string? Medium { get; init; }

        public string? Long { get; init; }
    }

    private sealed class ArmorRulesetData
    {
        public required List<ArmorEntryData> Armor { get; init; }
    }

    private sealed class ArmorEntryData
    {
        public required string Id { get; init; }

        public required string Name { get; init; }

        public required JsonElement ArmorValue { get; init; }

        public required SkillPenaltyData SkillPenalty { get; init; }

        public required List<string> HitLocations { get; init; }

        public string? Note { get; init; }

        public required string Source { get; init; }
    }

    private sealed class SkillPenaltyData
    {
        public required string Category { get; init; }

        public required int Value { get; init; }
    }

    private sealed class ArmorValueData
    {
        public required int Melee { get; init; }

        public required int Firearms { get; init; }
    }

    private sealed class VehicleRulesetData
    {
        public required List<VehicleEntryData> Vehicles { get; init; }
    }

    private sealed class VehicleEntryData
    {
        public required string Id { get; init; }

        public required string Name { get; init; }

        public required string SkillId { get; init; }

        public required int RatedSpeed { get; init; }

        public required int Handling { get; init; }

        public required int Acceleration { get; init; }

        public required int MetersPerRound { get; init; }

        public required ArmorValueData Armor { get; init; }

        public required int Siz { get; init; }

        public required int HitPoints { get; init; }

        public required int Crew { get; init; }

        public required string Passengers { get; init; }

        public required int Cargo { get; init; }

        public required string ValueTier { get; init; }

        public required string Source { get; init; }
    }
}
