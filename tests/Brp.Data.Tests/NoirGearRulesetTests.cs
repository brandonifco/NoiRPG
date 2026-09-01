using Brp.Core.Dice;
using Brp.Core.Skills;
using Brp.Rules.Gear;

namespace Brp.Data.Tests;

/// <summary>
/// Reproduces the printed stats for every weapon, armor type, and car in the hand-picked modern
/// noir subset (Ch 8: Equipment, Modern Melee Weapons and Modern Missile Weapons tables,
/// p.201-202; Modern Armor table, p.207; Autos, Trucks, Trains &amp; Tanks table, p.219), cell by
/// cell, so a transcription error surfaces as a failing row.
/// </summary>
public class NoirGearRulesetTests
{
    public static TheoryData<string, string, string, string, bool, WeaponClass> MeleeWeapons => new()
    {
        // id, name, skillId, damage, applyDamageBonus, weaponClass
        { "brassKnuckles", "Brass Knuckles", "Brawl", "2", true, WeaponClass.Brawl },
        { "clubHeavy", "Club, Heavy", "Melee Weapon (Club)", "1D8", true, WeaponClass.Club },
        { "clubLight", "Club, Light", "Melee Weapon (Club)", "1D6", true, WeaponClass.Club },
        { "knifeButcher", "Knife, Butcher", "Melee Weapon (Knife)", "1D6", true, WeaponClass.Dagger },
        { "knifePocket", "Knife, Pocket", "Melee Weapon (Knife)", "1D4", true, WeaponClass.Dagger },
        { "knifeSwitchblade", "Knife, Switchblade", "Melee Weapon (Knife)", "1D4", true, WeaponClass.Dagger },
    };

    [Theory]
    [MemberData(nameof(MeleeWeapons))]
    public void Every_melee_weapon_reproduces_its_printed_stats(
        string id, string name, string skillId, string damage, bool applyDamageBonus, WeaponClass weaponClass)
    {
        var registry = NoirGearRuleset.Load();

        var weapon = registry.WeaponById(new WeaponId(id));

        Assert.Equal(name, weapon.Name);
        Assert.Equal(new SkillId(skillId), weapon.SkillId);
        Assert.Equal(DiceExpression.Parse(damage).Notation, weapon.Damage.Notation);
        Assert.Equal(applyDamageBonus, weapon.ApplyDamageBonus);
        Assert.Equal(weaponClass, weapon.WeaponClass);
        Assert.False(weapon.IsFirearm);
        Assert.Empty(weapon.DamageByRange);
    }

    public static TheoryData<string, string, string, string, WeaponClass, int, string, string, string, int> Firearms => new()
    {
        // id, name, skillId, damage, weaponClass, baseRange, malfunctionNumber, ammoCapacity, attacksPerRound, baseChance
        { "pistolDerringer", "Pistol, Derringer", "Firearms (Handgun)", "1D6", WeaponClass.Pistol, 3, "00", "1 or 2", "1", 20 },
        { "pistolLight", "Pistol, Light", "Firearms (Handgun)", "1D6", WeaponClass.Pistol, 10, "00", "8", "3", 20 },
        { "pistolMedium", "Pistol, Medium", "Firearms (Handgun)", "1D8", WeaponClass.Pistol, 20, "98–00", "12", "2", 20 },
        { "pistolHeavy", "Pistol, Heavy", "Firearms (Handgun)", "1D10+2", WeaponClass.Pistol, 15, "00", "8", "1", 20 },
        { "revolverLight", "Revolver, Light", "Firearms (Handgun)", "1D6", WeaponClass.Revolver, 15, "00", "6", "2", 20 },
        { "revolverMedium", "Revolver, Medium", "Firearms (Handgun)", "1D8", WeaponClass.Revolver, 25, "00", "6", "1", 20 },
        { "revolverHeavy", "Revolver, Heavy", "Firearms (Handgun)", "1D10+2", WeaponClass.Revolver, 20, "00", "6", "1", 20 },
        { "rifleBoltAction", "Rifle, Bolt-action", "Firearms (Rifle)", "2D6+4", WeaponClass.Rifle, 110, "00", "5", "0.5", 25 },
        { "gunSubmachine", "Gun, Submachine", "Firearms (Handgun)", "1D8", WeaponClass.SubmachineGun, 40, "98–00", "32", "2 or burst", 15 },
    };

    [Theory]
    [MemberData(nameof(Firearms))]
    public void Every_non_shotgun_non_sniper_firearm_reproduces_its_printed_stats(
        string id, string name, string skillId, string damage, WeaponClass weaponClass,
        int baseRange, string malfunctionNumber, string ammoCapacity, string attacksPerRound, int baseChance)
    {
        var registry = NoirGearRuleset.Load();

        var weapon = registry.WeaponById(new WeaponId(id));

        Assert.Equal(name, weapon.Name);
        Assert.Equal(new SkillId(skillId), weapon.SkillId);
        Assert.Equal(DiceExpression.Parse(damage).Notation, weapon.Damage.Notation);
        Assert.False(weapon.ApplyDamageBonus);
        Assert.Equal(weaponClass, weapon.WeaponClass);
        Assert.True(weapon.IsFirearm);

        var firearm = weapon.Firearm!;
        Assert.Equal(baseRange, firearm.ListedRangeMeters);
        Assert.Equal(baseRange.ToString(System.Globalization.CultureInfo.InvariantCulture), firearm.ListedRange);
        Assert.Equal(malfunctionNumber, firearm.MalfunctionNumber);
        Assert.Equal(ammoCapacity, firearm.AmmoCapacity);
        Assert.Equal(attacksPerRound, firearm.AttacksPerRound);
        Assert.Equal(baseChance, firearm.BaseChance);
    }

    [Fact]
    public void Sniper_rifle_reproduces_its_printed_stats_and_bipod_scope_notes()
    {
        // Ch 8, Modern Missile Weapons table (p.201-202), notes 4 and 5.
        var registry = NoirGearRuleset.Load();

        var sniper = registry.WeaponById(new WeaponId("rifleSniper"));

        Assert.Equal("Rifle, Sniper", sniper.Name);
        Assert.Equal(new SkillId("Firearms (Rifle)"), sniper.SkillId);
        Assert.Equal(DiceExpression.Parse("2D10+4").Notation, sniper.Damage.Notation);
        Assert.False(sniper.ApplyDamageBonus);
        Assert.Equal(WeaponClass.Rifle, sniper.WeaponClass);

        var firearm = sniper.Firearm!;
        Assert.Equal(250, firearm.ListedRangeMeters);
        Assert.Equal(125, firearm.ListedRangeWithoutScope);
        Assert.Equal("98–00", firearm.MalfunctionNumber);
        Assert.Equal("11", firearm.AmmoCapacity);
        Assert.Equal("1", firearm.AttacksPerRound);
        Assert.Equal(20, firearm.BaseChance);
        Assert.Equal(20, firearm.BaseChanceWithBipod);
        Assert.Equal(10, firearm.BaseChanceWithoutBipod);
    }

    [Fact]
    public void Double_barreled_shotgun_reproduces_its_damage_by_range()
    {
        // Ch 8, Modern Missile Weapons table (p.201), note 6.
        var registry = NoirGearRuleset.Load();

        var shotgun = registry.WeaponById(new WeaponId("shotgunDoubleBarreled"));

        Assert.Equal("Shotgun, Double-barreled", shotgun.Name);
        Assert.Equal(new SkillId("Firearms (Shotgun)"), shotgun.SkillId);
        Assert.Equal(WeaponClass.Shotgun, shotgun.WeaponClass);
        Assert.False(shotgun.ApplyDamageBonus);

        Assert.Collection(
            shotgun.DamageByRange,
            first => AssertIncrement(first, 10, "4D6"),
            second => AssertIncrement(second, 20, "2D6"),
            third => AssertIncrement(third, 50, "1D6"));

        var firearm = shotgun.Firearm!;
        Assert.Equal("10/20/50", firearm.ListedRange);
        Assert.Null(firearm.ListedRangeMeters);
        Assert.Equal("2", firearm.AmmoCapacity);
        Assert.Equal(30, firearm.BaseChance);
    }

    [Fact]
    public void Sawn_off_shotgun_reproduces_its_two_step_damage_by_range()
    {
        // Ch 8, Modern Missile Weapons table (p.201), note 7: "not effective beyond 20 yards".
        var registry = NoirGearRuleset.Load();

        var shotgun = registry.WeaponById(new WeaponId("shotgunSawedOff"));

        Assert.Equal("Shotgun, Sawn-off", shotgun.Name);
        Assert.Collection(
            shotgun.DamageByRange,
            first => AssertIncrement(first, 5, "4D6"),
            second => AssertIncrement(second, 20, "1D6"));

        var firearm = shotgun.Firearm!;
        Assert.Equal("5/20/—", firearm.ListedRange);
        Assert.Equal("1 or 2", firearm.AmmoCapacity);
    }

    public static TheoryData<string, SpecialDamageType> SpecialDamageTypesByWeapon => new()
    {
        // Ch 6, p.150: "Firearms, arrows, and other pointed weapons inflict impaling damage."
        { "knifeButcher", SpecialDamageType.Impaling },
        { "knifePocket", SpecialDamageType.Impaling },
        { "knifeSwitchblade", SpecialDamageType.Impaling },
        { "pistolDerringer", SpecialDamageType.Impaling },
        { "pistolLight", SpecialDamageType.Impaling },
        { "pistolMedium", SpecialDamageType.Impaling },
        { "pistolHeavy", SpecialDamageType.Impaling },
        { "revolverLight", SpecialDamageType.Impaling },
        { "revolverMedium", SpecialDamageType.Impaling },
        { "revolverHeavy", SpecialDamageType.Impaling },
        { "rifleBoltAction", SpecialDamageType.Impaling },
        { "rifleSniper", SpecialDamageType.Impaling },
        { "shotgunDoubleBarreled", SpecialDamageType.Impaling },
        { "shotgunSawedOff", SpecialDamageType.Impaling },
        { "gunSubmachine", SpecialDamageType.Impaling },
        // Ch 6, p.149: "Clubs, unarmed strikes, and other blunt weapons can cause crushing damage."
        { "brassKnuckles", SpecialDamageType.Crushing },
        { "clubHeavy", SpecialDamageType.Crushing },
        { "clubLight", SpecialDamageType.Crushing },
    };

    [Theory]
    [MemberData(nameof(SpecialDamageTypesByWeapon))]
    public void Every_weapon_has_the_printed_special_damage_type(string id, SpecialDamageType expected)
    {
        var registry = NoirGearRuleset.Load();

        var weapon = registry.WeaponById(new WeaponId(id));

        Assert.Equal(expected, weapon.SpecialDamageType);
    }

    [Fact]
    public void The_special_damage_type_table_covers_every_shipped_weapon_exactly_once()
    {
        var registry = NoirGearRuleset.Load();
        var allIds = registry.Weapons.Keys.Select(id => id.Value).OrderBy(id => id, StringComparer.Ordinal).ToList();
        var coveredIds = SpecialDamageTypesByWeapon.Select(row => (string)row[0]!).OrderBy(id => id, StringComparer.Ordinal).ToList();

        Assert.Equal(allIds, coveredIds);
    }

    private static void AssertIncrement(RangeIncrementDamage increment, int range, string damage)
    {
        Assert.Equal(range, increment.Range);
        Assert.Equal(DiceExpression.Parse(damage).Notation, increment.Damage.Notation);
    }

    private static readonly string[] ChestOnly = ["Chest"];
    private static readonly string[] AllLocations = ["Head", "Arms", "Chest", "Abdomen", "Legs"];

    public static TheoryData<string, string, int, int, string, int, string[]> ArmorTypes => new()
    {
        // id, name, meleeAv, firearmsAv, skillPenaltyCategory, skillPenaltyValue, hitLocations
        { "bulletproofVestEarly", "Bulletproof Vest, Early", 4, 4, "Physical", -25, ChestOnly },
        { "bulletproofVestModern", "Bulletproof Vest, Modern", 4, 8, "Physical", -5, ChestOnly },
        { "riotGear", "Riot Gear", 12, 6, "Physical", -10, AllLocations },
    };

    [Theory]
    [MemberData(nameof(ArmorTypes))]
    public void Every_armor_type_reproduces_its_printed_stats(
        string id, string name, int meleeAv, int firearmsAv, string skillPenaltyCategory, int skillPenaltyValue, string[] hitLocations)
    {
        var registry = NoirGearRuleset.Load();

        var armor = registry.ArmorById(new ArmorId(id));

        Assert.Equal(name, armor.Name);
        Assert.Equal(meleeAv, armor.ArmorValue.MeleeAndLowVelocity);
        Assert.Equal(firearmsAv, armor.ArmorValue.Firearms);
        Assert.Equal(Enum.Parse<SkillCategory>(skillPenaltyCategory), armor.SkillPenalty.Category);
        Assert.Equal(skillPenaltyValue, armor.SkillPenalty.PercentPenalty);
        Assert.Equal(hitLocations, armor.HitLocations);
    }

    [Fact]
    public void Riot_gear_records_its_helmet_note()
    {
        var registry = NoirGearRuleset.Load();

        var riotGear = registry.ArmorById(new ArmorId("riotGear"));

        Assert.Equal("Includes helmet", riotGear.Note);
    }

    /// <summary>
    /// One row of the Autos, Trucks, Trains &amp; Tanks table (p.219), restricted to its three
    /// automobile entries. Grouped into a record because the table has more columns than
    /// <see cref="TheoryData{T1}"/>'s generic arity supports individually.
    /// </summary>
    public sealed record VehicleRow(
        string Id, string Name, int RatedSpeed, int Handling, int Acceleration, int MetersPerRound,
        int MeleeArmor, int FirearmsArmor, int Siz, int HitPoints, int Crew, string Passengers,
        int Cargo, string ValueTier);

    public static TheoryData<VehicleRow> Vehicles => new()
    {
        new VehicleRow("automobileVintage", "Automobile, Vintage", 6, -5, 1, 67, 10, 1, 60, 35, 1, "3", 12, "Average"),
        new VehicleRow("automobileModernSedan", "Automobile, Modern Sedan", 12, 0, 7, 134, 14, 2, 50, 40, 1, "3-4", 24, "Average"),
        new VehicleRow("automobileModernSportscar", "Automobile, Modern Sportscar", 15, 5, 8, 200, 10, 2, 45, 45, 1, "1", 8, "Expensive"),
    };

    [Theory]
    [MemberData(nameof(Vehicles))]
    public void Every_vehicle_reproduces_its_printed_stats(VehicleRow row)
    {
        var registry = NoirGearRuleset.Load();

        var vehicle = registry.VehicleById(new VehicleId(row.Id));

        Assert.Equal(row.Name, vehicle.Name);
        Assert.Equal(new SkillId("Drive"), vehicle.SkillId);
        Assert.Equal(row.RatedSpeed, vehicle.RatedSpeed);
        Assert.Equal(row.Handling, vehicle.Handling);
        Assert.Equal(row.Acceleration, vehicle.Acceleration);
        Assert.Equal(row.MetersPerRound, vehicle.MetersPerRound);
        Assert.Equal(row.MeleeArmor, vehicle.Armor.MeleeAndLowVelocity);
        Assert.Equal(row.FirearmsArmor, vehicle.Armor.Firearms);
        Assert.Equal(row.Siz, vehicle.Siz);
        Assert.Equal(row.HitPoints, vehicle.HitPoints);
        Assert.Equal(row.Crew, vehicle.Crew);
        Assert.Equal(row.Passengers, vehicle.Passengers);
        Assert.Equal(row.Cargo, vehicle.Cargo);
        Assert.Equal(row.ValueTier, vehicle.ValueTier);
    }
}
