using Brp.Data;
using Brp.Rules.Characters;
using Brp.Rules.Gear;

namespace Brp.Rules.Tests.Gear;

public class GearRegistryTests
{
    [Fact]
    public void An_equipment_item_whose_name_matches_a_weapon_resolves_to_its_definition()
    {
        var registry = NoirGearRuleset.Load();
        var item = new EquipmentItem("Revolver, Light");

        var lookup = registry.Resolve(item);

        Assert.True(lookup.HasDefinition);
        Assert.NotNull(lookup.Weapon);
        Assert.Equal("Revolver, Light", lookup.Weapon!.Name);
        Assert.Null(lookup.Armor);
    }

    [Fact]
    public void An_equipment_item_whose_name_matches_an_armor_type_resolves_to_its_definition()
    {
        var registry = NoirGearRuleset.Load();
        var item = new EquipmentItem("Bulletproof Vest, Modern");

        var lookup = registry.Resolve(item);

        Assert.True(lookup.HasDefinition);
        Assert.NotNull(lookup.Armor);
        Assert.Equal("Bulletproof Vest, Modern", lookup.Armor!.Name);
        Assert.Null(lookup.Weapon);
    }

    [Fact]
    public void Matching_is_case_insensitive()
    {
        var registry = NoirGearRuleset.Load();
        var item = new EquipmentItem("revolver, light");

        var lookup = registry.Resolve(item);

        Assert.NotNull(lookup.Weapon);
    }

    [Fact]
    public void Plain_gear_with_no_definition_resolves_cleanly_with_no_exception()
    {
        var registry = NoirGearRuleset.Load();
        var flashlight = new EquipmentItem("flashlight");

        var lookup = registry.Resolve(flashlight);

        Assert.False(lookup.HasDefinition);
        Assert.Null(lookup.Weapon);
        Assert.Null(lookup.Armor);
    }

    [Fact]
    public void A_car_can_be_looked_up_by_id()
    {
        var registry = NoirGearRuleset.Load();

        var sedan = registry.VehicleById(new VehicleId("automobileModernSedan"));

        Assert.Equal("Automobile, Modern Sedan", sedan.Name);
    }

    [Fact]
    public void Looking_up_an_unknown_vehicle_id_throws()
    {
        var registry = NoirGearRuleset.Load();

        Assert.Throws<KeyNotFoundException>(() => registry.VehicleById(new VehicleId("hovercraft")));
    }

    [Fact]
    public void A_character_carrying_an_equipment_item_can_have_it_resolved_against_the_gear_registry()
    {
        // The light tie the issue asks for: EquipmentItem stays name-only on Character, but a
        // caller holding both a Character and the GearRegistry can still look up its stats.
        var registry = NoirGearRuleset.Load();
        var equipment = new EquipmentList();
        equipment.Add(new EquipmentItem("Knife, Switchblade"));

        var lookup = registry.Resolve(equipment.Items.Single());

        Assert.Equal("Knife, Switchblade", lookup.Weapon!.Name);
    }
}
