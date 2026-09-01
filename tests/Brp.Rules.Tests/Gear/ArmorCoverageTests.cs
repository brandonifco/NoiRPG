using Brp.Rules.Combat;
using Brp.Rules.Gear;

namespace Brp.Rules.Tests.Gear;

/// <summary>
/// Confirms <see cref="ArmorCoverage"/> resolves the armor table's printed coverage categories
/// ("Head", "Chest", "Abdomen", "Arms", "Legs" -- Ch 8: Equipment, Modern Armor table, "Fits
/// Locations" column, p.207) against the seven granular <see cref="HitLocation"/> values, per
/// "Armor by Hit Location (Option)" (Ch 8, p.209).
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
    public void Armor_value_at_a_location_uses_the_heaviest_of_multiple_covering_pieces()
    {
        // Ch 8, p.209: "using the heaviest if these differ."
        var lightVest = MakeArmor(4, 4, "Chest");
        var heavyRiotGear = MakeArmor(12, 6, "Head", "Arms", "Chest", "Abdomen", "Legs");

        Assert.Equal(12, ArmorCoverage.ArmorValueAt(HitLocation.Chest, isFirearm: false, [lightVest, heavyRiotGear]));
        Assert.Equal(6, ArmorCoverage.ArmorValueAt(HitLocation.Chest, isFirearm: true, [lightVest, heavyRiotGear]));
    }
}
