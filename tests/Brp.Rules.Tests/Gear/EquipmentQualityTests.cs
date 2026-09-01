using Brp.Core.Modifiers;
using Brp.Core.Primitives;
using Brp.Core.Skills;
using Brp.Rules.Gear;

namespace Brp.Rules.Tests.Gear;

/// <summary>
/// Ch 8: Equipment, "Equipment Quality Modifiers" (p.185): reproduces the printed three-row
/// table in full (Inferior/Average/Superior), and confirms the modifier flows through the
/// existing <see cref="ModifierPipeline"/> as a situational source (ADR 0007), not a parallel
/// path. See <c>docs/decisions/0031-equipment-quality-and-skills-and-equipment.md</c>.
/// </summary>
public class EquipmentQualityTests
{
    private static readonly EquipmentQualityRuleset Ruleset = new(inferiorDelta: -20, superiorDelta: 20);
    private static readonly SkillEquipmentRuleset SkillEquipment =
        new([new SkillEquipmentLink(new SkillId("Locksmith"), "None, or precision tools.")]);

    // Ch 8, p.185: "Quality | Modifier | Value and Ability -- Inferior -20% ... Average None ...
    // Superior +20%." Every printed row, not a spot check.
    [Theory]
    [InlineData(EquipmentQualityTier.Inferior, -20)]
    [InlineData(EquipmentQualityTier.Average, 0)]
    [InlineData(EquipmentQualityTier.Superior, 20)]
    public void Reproduces_the_books_equipment_quality_table_page_185(EquipmentQualityTier tier, int expectedDelta)
    {
        var modifier = EquipmentQuality.Modifier(tier, "lock picks", Ruleset);

        Assert.Equal(expectedDelta, modifier.Delta);
    }

    [Fact]
    public void Average_quality_produces_a_zero_delta_modifier_not_a_null()
    {
        var modifier = EquipmentQuality.Modifier(EquipmentQualityTier.Average, "lock picks", Ruleset);

        Assert.NotNull(modifier);
        Assert.Equal(0, modifier.Delta);
    }

    [Fact]
    public void Quality_modifier_is_situational_per_the_books_own_cross_reference()
    {
        // "The quality of equipment can provide a modifier to a skill roll, as described in
        // Situational Modifiers" -- the book names the stage itself.
        var modifier = EquipmentQuality.Modifier(EquipmentQualityTier.Superior, "lock picks", Ruleset);

        Assert.Equal(AdditiveKind.Situational, modifier.Kind);
    }

    [Fact]
    public void Situational_modifier_is_applied_after_the_difficulty_multiplier()
    {
        // p.185: "Remember that situational modifiers to a skill rating are applied after an
        // Easy modifier doubles it or Difficult divides it in half." Worked example: 65%,
        // Difficult (halve, round up per ADR 0007), superior lock picks (+20%).
        var quality = EquipmentQuality.Modifier(EquipmentQualityTier.Superior, "lock picks", Ruleset);
        var chain = ModifierPipeline.Evaluate(
            Percent.Of(65), [quality, DifficultyModifier.Difficult("test")]);

        // 65 / 2 = 32.5 -> 33 (round up), then +20 -> 53. Not (65 + 20) / 2 = 43.
        Assert.Equal(53, chain.EffectiveChance!.Value.Value);
    }

    [Fact]
    public void Inferior_lockpicking_example_matches_the_books_worked_example_page_185()
    {
        // "Inferior tools (rusty, broken, improvised) modified the skill by -20%."
        var modifier = EquipmentQuality.Modifier(EquipmentQualityTier.Inferior, "lock picks", Ruleset);
        var chain = ModifierPipeline.Evaluate(Percent.Of(50), [modifier]);

        Assert.Equal(30, chain.EffectiveChance!.Value.Value);
    }

    [Fact]
    public void ModifierForSkill_delegates_to_Modifier_when_the_skill_is_in_the_mapping()
    {
        var modifier = EquipmentQuality.ModifierForSkill(
            new SkillId("Locksmith"), EquipmentQualityTier.Superior, "lock picks", Ruleset, SkillEquipment);

        Assert.Equal(20, modifier.Delta);
    }

    [Fact]
    public void ModifierForSkill_throws_when_the_skill_is_not_listed_in_the_skills_and_equipment_mapping()
    {
        // p.185-186: an unlisted skill "does not require any equipment, or it is obvious (such
        // as weapon skills)" -- asking for a quality modifier there is a caller mistake.
        Assert.Throws<ArgumentException>(() =>
            EquipmentQuality.ModifierForSkill(
                new SkillId("Brawl"), EquipmentQualityTier.Superior, "brass knuckles", Ruleset, SkillEquipment));
    }
}
