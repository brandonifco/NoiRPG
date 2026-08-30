using Brp.Core.Abilities;
using Brp.Core.Primitives;
using Brp.Core.Skills;
using Brp.Data;

namespace Brp.Core.Tests.Skills;

public class SkillDefinitionTests
{
    [Fact]
    public void A_plain_skill_is_not_a_specialty_and_its_book_equivalent_defaults_to_its_own_name()
    {
        var spot = new SkillDefinition(new SkillId("Spot"), "Spot", SkillCategory.Perception, new ConstantBaseChance(Percent.Of(25)));

        Assert.False(spot.IsSpecialty);
        Assert.Null(spot.Parent);
        Assert.Equal("Spot", spot.BookEquivalent);
    }

    [Fact]
    public void BaseChanceFor_evaluates_the_skills_expression_against_the_given_abilities()
    {
        var dodge = new SkillDefinition(
            new SkillId("Dodge"), "Dodge", SkillCategory.Physical,
            new CharacteristicFormulaBaseChance(new CharacteristicTerm(new CharacteristicId("DEX"), 2)));

        Assert.Equal(Percent.Of(24), dodge.BaseChanceFor(Create(dexterity: 12)));
    }

    private static AbilitySet Create(int dexterity)
    {
        var ruleset = NoirAbilityRuleset.Load();
        var values = ruleset.Characteristics.Keys.ToDictionary(id => id, _ => 12);
        values[new CharacteristicId("DEX")] = dexterity;
        return new AbilitySet(ruleset, values);
    }
}
