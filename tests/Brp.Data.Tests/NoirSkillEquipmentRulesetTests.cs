using Brp.Core.Skills;

namespace Brp.Data.Tests;

/// <summary>
/// Confirms the shipped Skills-and-Equipment mapping loads, matches the hand-picked noir subset
/// described in <c>skill-equipment-ruleset.json</c>'s own <c>source</c> field, and stays a subset
/// of the engine's actual skill list (<c>skill-ruleset.json</c>). Ch 8: Equipment, "Skills and
/// Equipment" (pp.185-186); <c>orc-scope-filter.md</c>, Ch 8.
/// </summary>
public class NoirSkillEquipmentRulesetTests
{
    // Every skill id this ruleset is expected to carry -- the book's 16 in-scope rows, with the
    // five rows that name a specialty-only parent (Art, Knowledge, Repair, Science, Technical
    // Skill) expanded to their resolvable specialty ids (see the ADR's "Specialty expansion").
    private static readonly string[] ExpectedSkillIds =
    [
        "Appraise", "Photography", "Climb", "Disguise", "Locksmith", "First Aid", "Gaming",
        "Streetwise", "Law", "Accounting", "Knowledge (Group)", "Knowledge (Region)", "Knowledge (Politics)",
        "Language", "Medicine", "Navigate",
        "Repair (Electrical)", "Repair (Electronic)", "Repair (Mechanical)",
        "Research", "Science (Chemistry)", "Science (Forensics)", "Teach",
        "Technical Skill (Computer Use)", "Technical Skill (Electronics)", "Technical Skill (Security Systems)",
    ];

    [Fact]
    public void Loads_exactly_the_hand_picked_noir_subset()
    {
        var ruleset = NoirSkillEquipmentRuleset.Load();

        Assert.Equal(ExpectedSkillIds.Length, ruleset.Links.Count);
        foreach (var skillId in ExpectedSkillIds)
        {
            Assert.True(ruleset.UsesEquipment(new SkillId(skillId)), $"Expected a link for '{skillId}'.");
        }
    }

    [Fact]
    public void Every_linked_skill_id_exists_in_the_engines_skill_ruleset()
    {
        var skillEquipment = NoirSkillEquipmentRuleset.Load();
        var skillRegistry = NoirSkillRuleset.Load();

        foreach (var link in skillEquipment.Links.Values)
        {
            Assert.True(
                skillRegistry.TryGetSkill(link.SkillId, out _),
                $"'{link.SkillId}' is not a defined skill in skill-ruleset.json.");
        }
    }

    [Fact]
    public void Weapon_and_combat_skills_are_not_listed_matching_the_books_own_carve_out()
    {
        // p.185-186: "it does not require any equipment, or it is obvious (such as weapon
        // skills)."
        var ruleset = NoirSkillEquipmentRuleset.Load();

        Assert.False(ruleset.UsesEquipment(new SkillId("Firearms")));
        Assert.False(ruleset.UsesEquipment(new SkillId("Brawl")));
        Assert.False(ruleset.UsesEquipment(new SkillId("Melee Weapon")));
    }
}
