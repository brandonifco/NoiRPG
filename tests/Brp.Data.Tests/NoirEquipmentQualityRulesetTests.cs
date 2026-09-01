namespace Brp.Data.Tests;

/// <summary>
/// Confirms the shipped equipment-quality percentages load and match the book: Ch 8: Equipment,
/// "Equipment Quality Modifiers" (p.185). See
/// <c>docs/decisions/0031-equipment-quality-and-skills-and-equipment.md</c>.
/// </summary>
public class NoirEquipmentQualityRulesetTests
{
    [Fact]
    public void Deltas_match_the_books_equipment_quality_table_page_185()
    {
        var ruleset = NoirEquipmentQualityRuleset.Load();

        Assert.Equal(-20, ruleset.InferiorDelta);
        Assert.Equal(20, ruleset.SuperiorDelta);
    }
}
