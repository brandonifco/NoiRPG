using Brp.Rules.Gear;

namespace Brp.Data.Tests;

/// <summary>
/// Guards the hand-picked-subset decision (`orc-scope-filter.md`, Ch 8: "a dozen firearms and
/// three armor types ... not two hundred rows", `docs/decisions/0012-gear-definitions.md`):
/// exactly the chosen weapons and armor load, nothing pre-modern, fantasy, or shielded, and
/// every firearm carries the listed-range value #21's range-band math needs.
/// </summary>
public class NoirGearRulesetScopeTests
{
    private static readonly string[] InScopeWeaponIds =
    [
        "brassKnuckles", "clubHeavy", "clubLight", "knifeButcher", "knifePocket", "knifeSwitchblade",
        "pistolDerringer", "pistolLight", "pistolMedium", "pistolHeavy",
        "revolverLight", "revolverMedium", "revolverHeavy",
        "rifleBoltAction", "rifleSniper",
        "shotgunDoubleBarreled", "shotgunSawedOff",
        "gunSubmachine",
    ];

    private static readonly string[] InScopeArmorIds =
    [
        "bulletproofVestEarly", "bulletproofVestModern", "riotGear",
    ];

    // Names the extractor's over-inclusive first pass carried that the scope filter cuts:
    // pre-modern black-powder firearms, and firearms/armor trimmed back to the hand-picked
    // target count. None of these may appear in the loaded registry.
    private static readonly string[] OutOfScopeWeaponNames =
    [
        "Pistol, Flintlock", "Rifle, Musket", // pre-modern (AGENTS.md invariant 4, orc-scope-filter.md)
        "Rifle, Assault", "Rifle, Elephant", "Rifle, Sporting", // trimmed to the "couple of rifles" target
        "Shotgun, Automatic", "Shotgun, Sporting", // trimmed to the two-shotgun target
        "Chainsaw", // needs a skill specialty ("Melee Weapon (Improvised)") that does not exist in scope
    ];

    private static readonly string[] OutOfScopeArmorNames =
    [
        "Ballistic Cloth", "Flak Jacket", // trimmed to the three-armor-type target
    ];

    [Fact]
    public void Exactly_the_hand_picked_dozen_firearms_and_melee_entries_load()
    {
        var registry = NoirGearRuleset.Load();

        var actual = registry.Weapons.Keys.Select(id => id.Value).ToHashSet();

        Assert.Equal(InScopeWeaponIds.ToHashSet(), actual);
    }

    [Fact]
    public void Exactly_twelve_firearms_are_in_the_hand_picked_subset()
    {
        var registry = NoirGearRuleset.Load();

        var firearmCount = registry.Weapons.Values.Count(weapon => weapon.IsFirearm);

        Assert.Equal(12, firearmCount);
    }

    [Fact]
    public void Exactly_the_hand_picked_three_armor_types_load()
    {
        var registry = NoirGearRuleset.Load();

        var actual = registry.Armor.Keys.Select(id => id.Value).ToHashSet();

        Assert.Equal(InScopeArmorIds.ToHashSet(), actual);
        Assert.Equal(3, registry.Armor.Count);
    }

    [Theory]
    [MemberData(nameof(OutOfScopeWeaponData))]
    public void No_out_of_scope_or_shield_weapon_is_present(string cutName)
    {
        var registry = NoirGearRuleset.Load();

        Assert.DoesNotContain(registry.Weapons.Values, weapon => weapon.Name == cutName);
    }

    public static TheoryData<string> OutOfScopeWeaponData => new(OutOfScopeWeaponNames);

    [Theory]
    [MemberData(nameof(OutOfScopeArmorData))]
    public void No_out_of_scope_armor_type_is_present(string cutName)
    {
        var registry = NoirGearRuleset.Load();

        Assert.DoesNotContain(registry.Armor.Values, armor => armor.Name == cutName);
    }

    public static TheoryData<string> OutOfScopeArmorData => new(OutOfScopeArmorNames);

    [Fact]
    public void No_weapon_uses_the_cut_Shield_skill_or_a_shield_weapon_class()
    {
        // orc-scope-filter.md, Ch 3: "CUT: ... Shield". The plan's "weapon/armor/shield"
        // phrasing predates the scope filter; no shield definitions may exist.
        var registry = NoirGearRuleset.Load();

        Assert.DoesNotContain(registry.Weapons.Values, weapon =>
            weapon.SkillId.Value.Contains("Shield", StringComparison.OrdinalIgnoreCase)
            || weapon.Name.Contains("Shield", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Every_weapon_uses_a_skill_id_defined_in_the_layer_2_skill_ruleset()
    {
        var skills = NoirSkillRuleset.Load();
        var gear = NoirGearRuleset.Load();

        foreach (var weapon in gear.Weapons.Values)
        {
            Assert.True(
                skills.TryGetSkill(weapon.SkillId, out _),
                $"Weapon '{weapon.Name}' references skill '{weapon.SkillId}', which does not exist in skill-ruleset.json.");
        }
    }

    [Fact]
    public void Every_firearm_carries_a_typed_listed_range_value()
    {
        // #21 (missile range bands) multiplies this value by 1/2/4 for point blank / medium /
        // long range (Ch 6/7, "Extended Range"); it must be present and parsed, not just a
        // free-text field, for every firearm.
        var registry = NoirGearRuleset.Load();

        foreach (var weapon in registry.Weapons.Values.Where(w => w.IsFirearm))
        {
            var firearm = weapon.Firearm!;
            Assert.False(string.IsNullOrWhiteSpace(firearm.ListedRange));

            // Either a single parsed range (most firearms) or a banded range whose increments
            // are fully captured in DamageByRange (the two shotguns).
            Assert.True(
                firearm.ListedRangeMeters is not null || weapon.DamageByRange.Count > 0,
                $"Firearm '{weapon.Name}' has no numeric listed range and no range-increment breakdown.");
        }
    }
}
