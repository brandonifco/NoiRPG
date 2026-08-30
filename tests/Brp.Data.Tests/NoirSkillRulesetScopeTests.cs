using Brp.Core.Skills;

namespace Brp.Data.Tests;

/// <summary>
/// Reproduces `orc-scope-filter.md`, "Chapter 3: Skills — the filter", IN list in full, so
/// a transcription error (a skill dropped or smuggled in) surfaces as a failing row rather
/// than a spot check. Framework-renamed skills are asserted under their canonical id; every
/// other skill is asserted under its own book name.
/// </summary>
public class NoirSkillRulesetScopeTests
{
    private static readonly string[] InScopeSkillIdValues =
    {
        "Appraise", "Photography", "Bargain", "Command", "Disguise", "Drive", "Etiquette",
        "Fast Talk", "Locksmith", "First Aid", "Gaming", "Insight",
        "Law", "Streetwise", "Accounting", "Knowledge (Group)", "Knowledge (Region)", "Knowledge (Politics)",
        "Language", "Listen", "Medicine", "Navigate", "Perform", "Persuade", "Psychotherapy",
        "Repair (Electrical)", "Repair (Electronic)", "Repair (Mechanical)", "Research",
        "Science (Chemistry)", "Science (Forensics)", "Sense", "Sleight of Hand", "Spot",
        "Status", "Shadow", "Stealth", "Strategy", "Teach",
        "Technical Skill (Computer Use)", "Technical Skill (Electronics)", "Technical Skill (Security Systems)",
        "Track", "Hide", "Climb", "Jump", "Swim", "Throw",
        "Brawl", "Grapple", "Dodge",
        "Firearms (Handgun)", "Firearms (Shotgun)", "Firearms (Rifle)",
        "Melee Weapon (Knife)", "Melee Weapon (Club)", "Martial Arts",
    };

    public static TheoryData<string> InScopeSkillIds => new(InScopeSkillIdValues);

    [Theory]
    [MemberData(nameof(InScopeSkillIds))]
    public void Every_in_scope_skill_is_present(string id)
    {
        var registry = NoirSkillRuleset.Load();

        Assert.True(registry.TryGetSkill(new SkillId(id), out _), $"'{id}' is IN scope per orc-scope-filter.md Ch 3 but missing from the registry.");
    }

    [Fact]
    public void Nothing_beyond_the_in_scope_list_and_the_house_rule_Intimidate_entry_is_loaded()
    {
        // Intimidate is the one documented exception: no book equivalent, added per
        // docs/decisions/0006-skill-bonus-system.md as part of the framework's 18.
        var registry = NoirSkillRuleset.Load();
        var expected = InScopeSkillIdValues.Append("Intimidate").ToHashSet();

        var actual = registry.Skills.Keys.Select(id => id.Value).ToHashSet();

        Assert.Equal(expected, actual);
    }
}
