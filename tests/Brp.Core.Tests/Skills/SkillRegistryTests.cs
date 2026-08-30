using Brp.Core.Primitives;
using Brp.Core.Skills;

namespace Brp.Core.Tests.Skills;

public class SkillRegistryTests
{
    [Fact]
    public void Skills_are_looked_up_by_their_canonical_id()
    {
        var spot = new SkillDefinition(new SkillId("Spot"), "Spot", SkillCategory.Perception, new ConstantBaseChance(Percent.Of(25)));
        var registry = new SkillRegistry(new[] { spot });

        Assert.Same(spot, registry[new SkillId("Spot")]);
        Assert.True(registry.TryGetSkill(new SkillId("Spot"), out var found));
        Assert.Same(spot, found);
    }

    [Fact]
    public void An_unknown_skill_id_throws_on_indexer_access()
    {
        var registry = new SkillRegistry(new[]
        {
            new SkillDefinition(new SkillId("Spot"), "Spot", SkillCategory.Perception, new ConstantBaseChance(Percent.Of(25))),
        });

        Assert.Throws<KeyNotFoundException>(() => registry[new SkillId("Locksmith")]);
    }

    [Fact]
    public void An_unknown_skill_id_is_reported_by_TryGetSkill_without_throwing()
    {
        var registry = new SkillRegistry(new[]
        {
            new SkillDefinition(new SkillId("Spot"), "Spot", SkillCategory.Perception, new ConstantBaseChance(Percent.Of(25))),
        });

        Assert.False(registry.TryGetSkill(new SkillId("Locksmith"), out var found));
        Assert.Null(found);
    }

    [Fact]
    public void An_empty_skill_list_is_rejected()
    {
        Assert.Throws<ArgumentException>(() => new SkillRegistry(Array.Empty<SkillDefinition>()));
    }
}
