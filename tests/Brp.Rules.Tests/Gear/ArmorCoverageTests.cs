using Brp.Rules.Combat;
using Brp.Rules.Gear;

namespace Brp.Rules.Tests.Gear;

/// <summary>
/// Confirms <see cref="ArmorCoverage"/> resolves both the printed "Fits Locations" categories
/// ("Head", "Chest", "Arms", "All", "All but head" -- Ch 8: Equipment, pp.207-208) and the
/// "Abdomen"/"Legs" data-authoring convenience (see <see cref="ArmorCoverage"/>'s remarks) against
/// the seven granular <see cref="HitLocation"/> values, and totals armor value across overlapping
/// covering pieces per "Layering Armor" (Ch 8, p.209).
/// </summary>
public class ArmorCoverageTests
{
    private static ArmorDefinition MakeArmor(int meleeAndLowVelocity, int firearms, params string[] hitLocations) =>
        new(
            new ArmorId("test"), "Test Armor", new ArmorValue(meleeAndLowVelocity, firearms),
            new ArmorSkillPenalty(Brp.Core.Skills.SkillCategory.Physical, -10), hitLocations, Note: null, Source: "test");

    [Theory]
    [InlineData("Head", HitLocation.Head, true)]
    [InlineData("Chest", HitLocation.Chest, true)]
    [InlineData("Chest", HitLocation.Abdomen, false)]
    [InlineData("Abdomen", HitLocation.Abdomen, true)]
    [InlineData("Arms", HitLocation.LeftArm, true)]
    [InlineData("Arms", HitLocation.RightArm, true)]
    [InlineData("Arms", HitLocation.LeftLeg, false)]
    [InlineData("Legs", HitLocation.LeftLeg, true)]
    [InlineData("Legs", HitLocation.RightLeg, true)]
    [InlineData("Legs", HitLocation.RightArm, false)]
    [InlineData("All", HitLocation.Head, true)]
    [InlineData("All", HitLocation.Chest, true)]
    [InlineData("All", HitLocation.LeftArm, true)]
    [InlineData("All", HitLocation.RightLeg, true)]
    [InlineData("All but head", HitLocation.Head, false)]
    [InlineData("All but head", HitLocation.Chest, true)]
    [InlineData("All but head", HitLocation.Abdomen, true)]
    [InlineData("All but head", HitLocation.LeftArm, true)]
    [InlineData("All but head", HitLocation.RightLeg, true)]
    public void Covers_maps_each_printed_category_to_the_correct_locations(
        string category, HitLocation location, bool expected)
    {
        var armor = MakeArmor(4, 4, category);

        Assert.Equal(expected, armor.Covers(location));
    }

    [Fact]
    public void Armor_value_at_a_location_is_zero_when_nothing_worn_covers_it()
    {
        var armor = MakeArmor(4, 8, "Chest");

        Assert.Equal(0, ArmorCoverage.ArmorValueAt(HitLocation.Head, isFirearm: false, [armor]));
    }

    [Fact]
    public void Armor_value_at_a_location_selects_melee_or_firearm_column()
    {
        var armor = MakeArmor(4, 8, "Chest");

        Assert.Equal(4, ArmorCoverage.ArmorValueAt(HitLocation.Chest, isFirearm: false, [armor]));
        Assert.Equal(8, ArmorCoverage.ArmorValueAt(HitLocation.Chest, isFirearm: true, [armor]));
    }

    [Fact]
    public void Armor_value_at_a_location_totals_multiple_covering_pieces()
    {
        // Ch 8, "Layering Armor" (p.209): soft armor worn with other armor "add[s] their usual
        // armor value"; overlapping anything else "total[s] the armor value." A Riot Gear (AV12/6)
        // layered with a soft vest (AV4/4) at the chest totals to 16/10, not the heaviest piece alone.
        var lightVest = MakeArmor(4, 4, "Chest");
        var heavyRiotGear = MakeArmor(12, 6, "Head", "Arms", "Chest", "Abdomen", "Legs");

        Assert.Equal(16, ArmorCoverage.ArmorValueAt(HitLocation.Chest, isFirearm: false, [lightVest, heavyRiotGear]));
        Assert.Equal(10, ArmorCoverage.ArmorValueAt(HitLocation.Chest, isFirearm: true, [lightVest, heavyRiotGear]));
    }

    [Fact]
    public void Armor_value_at_a_location_does_not_total_pieces_that_do_not_cover_it()
    {
        var chestOnly = MakeArmor(4, 4, "Chest");
        var armsOnly = MakeArmor(6, 6, "Arms");

        Assert.Equal(4, ArmorCoverage.ArmorValueAt(HitLocation.Chest, isFirearm: false, [chestOnly, armsOnly]));
    }
}
