using Brp.Rules.Gear;

namespace Brp.Data.Tests;

/// <summary>
/// Reproduces the hand-picked SIZ/hit-points/armor-value stats for every breakable item (Ch 8:
/// Equipment, "SIZ of Common Objects" pp.225-226, "Armor Value of Substances" p.224), cell by
/// cell, so a transcription error surfaces as a failing row.
/// </summary>
public class NoirItemHitPointsRulesetTests
{
    public static TheoryData<string, string, int, int, int> Items => new()
    {
        // id, name, siz, hitPoints, armorValue
        { "doorWoodInterior", "Door, Wood Interior", 6, 6, 3 },
        { "doorGlass", "Door, Glass", 8, 8, 1 },
        { "windowGlass", "Window, Glass", 3, 3, 1 },
        { "lockPadlock", "Lock, Padlock", 1, 1, 6 },
    };

    [Theory]
    [MemberData(nameof(Items))]
    public void Every_breakable_item_reproduces_its_hand_picked_stats(
        string id, string name, int siz, int hitPoints, int armorValue)
    {
        var registry = NoirItemHitPointsRuleset.Load();

        var item = registry.ById(new BreakableItemId(id));

        Assert.Equal(name, item.Name);
        Assert.Equal(siz, item.Siz);
        Assert.Equal(hitPoints, item.HitPoints);
        Assert.Equal(armorValue, item.ArmorValue);
        Assert.False(string.IsNullOrWhiteSpace(item.Source));
    }

    [Fact]
    public void Every_item_has_hit_points_equal_to_siz_per_the_printed_guideline_page_225()
    {
        var registry = NoirItemHitPointsRuleset.Load();

        foreach (var item in registry.Items.Values)
        {
            Assert.Equal(item.Siz, item.HitPoints);
        }
    }

    [Fact]
    public void The_table_covers_every_shipped_item_exactly_once()
    {
        var registry = NoirItemHitPointsRuleset.Load();
        var allIds = registry.Items.Keys.Select(id => id.Value).OrderBy(id => id, StringComparer.Ordinal).ToList();
        var coveredIds = Items.Select(row => (string)row[0]!).OrderBy(id => id, StringComparer.Ordinal).ToList();

        Assert.Equal(allIds, coveredIds);
    }
}
