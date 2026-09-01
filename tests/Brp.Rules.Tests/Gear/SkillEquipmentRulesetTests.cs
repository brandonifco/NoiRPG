using Brp.Core.Skills;
using Brp.Rules.Gear;

namespace Brp.Rules.Tests.Gear;

/// <summary>
/// Ch 8: Equipment, "Skills and Equipment" (pp.185-186): the mapping primitive itself, apart
/// from the shipped data (covered separately by <c>Brp.Data.Tests.NoirSkillEquipmentRulesetTests</c>).
/// </summary>
public class SkillEquipmentRulesetTests
{
    [Fact]
    public void A_listed_skill_resolves_to_its_link()
    {
        var ruleset = new SkillEquipmentRuleset(
            [new SkillEquipmentLink(new SkillId("Locksmith"), "None, or precision tools.")]);

        Assert.True(ruleset.UsesEquipment(new SkillId("Locksmith")));
        Assert.Equal("None, or precision tools.", ruleset.TryGetLink(new SkillId("Locksmith"))!.PotentialEquipment);
    }

    [Fact]
    public void An_unlisted_skill_is_the_expected_no_equipment_outcome_not_an_error()
    {
        // p.185-186: "If the skill is not listed, it does not require any equipment, or it is
        // obvious (such as weapon skills)."
        var ruleset = new SkillEquipmentRuleset(
            [new SkillEquipmentLink(new SkillId("Locksmith"), "None, or precision tools.")]);

        Assert.False(ruleset.UsesEquipment(new SkillId("Brawl")));
        Assert.Null(ruleset.TryGetLink(new SkillId("Brawl")));
    }

    [Fact]
    public void Lookup_is_case_insensitive_matching_the_skill_ids_convention()
    {
        var ruleset = new SkillEquipmentRuleset(
            [new SkillEquipmentLink(new SkillId("Locksmith"), "None, or precision tools.")]);

        Assert.True(ruleset.UsesEquipment(new SkillId("locksmith")));
    }

    [Fact]
    public void Construction_requires_at_least_one_link()
    {
        Assert.Throws<ArgumentException>(() => new SkillEquipmentRuleset([]));
    }
}
