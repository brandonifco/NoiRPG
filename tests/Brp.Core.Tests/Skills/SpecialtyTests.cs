using Brp.Core.Primitives;
using Brp.Core.Skills;

namespace Brp.Core.Tests.Skills;

/// <summary>
/// Ch 3: Skills, "Knowledge Specialties" (p.43): two specialties of one parent are two
/// independent skills. Acceptance criterion for #35: "A Specialty is a distinct resolvable
/// skill instance sharing its parent definition; two specialties of one parent are two
/// independent skills."
/// </summary>
public class SpecialtyTests
{
    [Fact]
    public void A_specialty_shares_its_parents_category_and_records_it_as_parent()
    {
        var knowledge = new SkillDefinition(new SkillId("Knowledge"), "Knowledge", SkillCategory.Mental, new ConstantBaseChance(Percent.Of(5)));
        var law = Specialty.Create(knowledge, new SkillId("Law"), "Law", new ConstantBaseChance(Percent.Of(5)), bookEquivalent: "Knowledge (Law)");

        Assert.True(law.IsSpecialty);
        Assert.Same(knowledge, law.Parent);
        Assert.Equal(knowledge.Category, law.Category);
        Assert.Equal("Knowledge (Law)", law.BookEquivalent);
    }

    [Fact]
    public void Two_specialties_of_one_parent_are_two_independent_resolvable_skills()
    {
        var knowledge = new SkillDefinition(new SkillId("Knowledge"), "Knowledge", SkillCategory.Mental, new ConstantBaseChance(Percent.Of(5)));
        var law = Specialty.Create(knowledge, new SkillId("Law"), "Law", new ConstantBaseChance(Percent.Of(5)));
        var streetwise = Specialty.Create(knowledge, new SkillId("Streetwise"), "Streetwise", new ConstantBaseChance(Percent.Of(5)));

        Assert.NotEqual(law.Id, streetwise.Id);
        Assert.Same(law.Parent, streetwise.Parent);

        var registry = new SkillRegistry(new[] { law, streetwise });

        Assert.Same(law, registry[new SkillId("Law")]);
        Assert.Same(streetwise, registry[new SkillId("Streetwise")]);
    }

    [Fact]
    public void A_specialty_can_carry_its_own_base_chance_independent_of_its_siblings()
    {
        // Ch 3: Skills, "Knowledge (various)" (p.42): the gamemaster picks 05% for common
        // specialties or 00% for ones requiring dedicated study -- two specialties of the
        // same parent are not required to share a base chance.
        var knowledge = new SkillDefinition(new SkillId("Knowledge"), "Knowledge", SkillCategory.Mental, new ConstantBaseChance(Percent.Of(5)));
        var common = Specialty.Create(knowledge, new SkillId("Region"), "Region", new ConstantBaseChance(Percent.Of(5)));
        var obscure = Specialty.Create(knowledge, new SkillId("Occult"), "Occult", new ConstantBaseChance(Percent.Of(0)));

        Assert.Equal(Percent.Of(5), common.BaseChance.Evaluate(AbilityHelper.Default()));
        Assert.Equal(Percent.Of(0), obscure.BaseChance.Evaluate(AbilityHelper.Default()));
    }
}
