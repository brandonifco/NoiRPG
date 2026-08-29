using Brp.Data;

namespace Brp.Data.Tests;

public class NoirAbilityRulesetTests
{
    public static TheoryData<int, int, string?> PrintedDamageModifierTable => new()
    {
        { 2, 12, "-1D6" }, { 13, 16, "-1D4" }, { 17, 24, null }, { 25, 32, "1D4" },
        { 33, 40, "1D6" }, { 41, 56, "2D6" }, { 57, 72, "3D6" }, { 73, 88, "4D6" },
        { 89, 104, "5D6" }, { 105, 120, "6D6" }, { 121, 136, "7D6" }, { 137, 152, "8D6" },
        { 153, 168, "9D6" },
    };

    [Fact]
    public void Characteristics_are_loaded_from_data_with_their_printed_bounds_and_roll_names()
    {
        var ruleset = NoirAbilityRuleset.Load();

        Assert.Collection(
            ruleset.Characteristics.Values.OrderBy(c => c.Id.Value),
            c => Assert.Equal(("CHA", "Charisma", 3, 21, "Charm"), (c.Id.Value, c.DisplayName, c.Minimum, c.Maximum, c.RollName)),
            c => Assert.Equal(("CON", "Constitution", 3, 21, "Stamina"), (c.Id.Value, c.DisplayName, c.Minimum, c.Maximum, c.RollName)),
            c => Assert.Equal(("DEX", "Dexterity", 3, 21, "Agility"), (c.Id.Value, c.DisplayName, c.Minimum, c.Maximum, c.RollName)),
            c => Assert.Equal(("EDU", "Education", 3, (int?)null, "Know"), (c.Id.Value, c.DisplayName, c.Minimum, c.Maximum, c.RollName)),
            c => Assert.Equal(("INT", "Intelligence", 8, (int?)null, "Idea"), (c.Id.Value, c.DisplayName, c.Minimum, c.Maximum, c.RollName)),
            c => Assert.Equal(("POW", "Power", 3, (int?)null, "Luck"), (c.Id.Value, c.DisplayName, c.Minimum, c.Maximum, c.RollName)),
            c => Assert.Equal(("SIZ", "Size", 8, 21, (string?)null), (c.Id.Value, c.DisplayName, c.Minimum, c.Maximum, c.RollName)),
            c => Assert.Equal(("STR", "Strength", 3, 21, "Effort"), (c.Id.Value, c.DisplayName, c.Minimum, c.Maximum, c.RollName)));
    }

    [Theory]
    [MemberData(nameof(PrintedDamageModifierTable))]
    public void Damage_modifier_table_reproduces_each_printed_row(int minimum, int maximum, string? expectedNotation)
    {
        // Ch 2: Characters, "Damage Modifier Table" (p.13). Each printed row is a test case;
        // checking every cell in its printed interval catches middle-band holes as well as ends.
        var table = NoirAbilityRuleset.Load().DamageModifierTable;

        foreach (var total in Enumerable.Range(minimum, maximum - minimum + 1))
        {
            Assert.Equal(expectedNotation, table.ForTotal(total)?.Notation);
        }
    }

    [Theory]
    [InlineData(12, "-1D6")]
    [InlineData(13, "-1D4")]
    [InlineData(16, "-1D4")]
    [InlineData(17, null)]
    [InlineData(24, null)]
    [InlineData(25, "1D4")]
    [InlineData(32, "1D4")]
    [InlineData(33, "1D6")]
    [InlineData(40, "1D6")]
    [InlineData(41, "2D6")]
    [InlineData(56, "2D6")]
    [InlineData(57, "3D6")]
    [InlineData(72, "3D6")]
    [InlineData(73, "4D6")]
    [InlineData(88, "4D6")]
    [InlineData(89, "5D6")]
    [InlineData(104, "5D6")]
    [InlineData(105, "6D6")]
    [InlineData(120, "6D6")]
    [InlineData(121, "7D6")]
    [InlineData(136, "7D6")]
    [InlineData(137, "8D6")]
    [InlineData(152, "8D6")]
    [InlineData(153, "9D6")]
    [InlineData(168, "9D6")]
    [InlineData(169, "10D6")]
    public void Damage_modifier_table_asserts_both_sides_of_every_band_edge(int total, string? expectedNotation)
    {
        Assert.Equal(expectedNotation, NoirAbilityRuleset.Load().DamageModifierTable.ForTotal(total)?.Notation);
    }

    [Theory]
    [InlineData(184, "10D6")]
    [InlineData(185, "11D6")]
    [InlineData(200, "11D6")]
    [InlineData(201, "12D6")]
    [InlineData(216, "12D6")]
    [InlineData(217, "13D6")]
    [InlineData(232, "13D6")]
    [InlineData(233, "14D6")]
    public void Damage_modifier_continuation_is_expressed_by_the_data_table(int total, string expectedNotation)
    {
        Assert.Equal(expectedNotation, NoirAbilityRuleset.Load().DamageModifierTable.ForTotal(total)?.Notation);
    }

    [Fact]
    public void Movement_is_a_flat_data_value()
    {
        Assert.Equal(10, NoirAbilityRuleset.Load().StartingMovement);
    }
}
