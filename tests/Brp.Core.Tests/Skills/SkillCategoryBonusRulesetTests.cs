using Brp.Core.Abilities;
using Brp.Core.Skills;

namespace Brp.Core.Tests.Skills;

/// <summary>
/// Unit tests for the <see cref="SkillCategoryBonusRuleset"/> policy itself (Ch 2: Characters,
/// "Skill Category Bonuses (Option)", pp.18-19): validation, the "round the magnitude down"
/// rule, negative bonuses, and the live-recompute property that lets an effective rating track a
/// characteristic change (ADR 0006). Built from hand-made specs to show the policy needs no data
/// project. The shipped data's reproduction of the two printed tables lives in
/// <c>Brp.Data.Tests.NoirSkillCategoryBonusRulesetTests</c>.
/// </summary>
public class SkillCategoryBonusRulesetTests
{
    private static SkillCategoryModifierSpec Spec(
        SkillCategory category, string primary, string secondary = "", string negative = "") =>
        new(category,
            new CharacteristicId(primary),
            Ids(secondary),
            Ids(negative));

    private static List<CharacteristicId> Ids(string commaSeparated) => commaSeparated
        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Select(id => new CharacteristicId(id))
        .ToList();

    private static IEnumerable<SkillCategoryModifierSpec> AllSix()
    {
        yield return Spec(SkillCategory.Combat, "DEX", "INT,STR");
        yield return Spec(SkillCategory.Communication, "INT", "POW,CHA");
        yield return Spec(SkillCategory.Manipulation, "DEX", "INT,STR");
        yield return Spec(SkillCategory.Mental, "INT", "POW,EDU");
        yield return Spec(SkillCategory.Perception, "INT", "POW,CON");
        yield return Spec(SkillCategory.Physical, "DEX", "STR,CON", "SIZ");
    }

    private static SkillCategoryBonusRuleset Full() => new(10, 1, 2, AllSix());

    private static AbilitySet Abilities(params (string Id, int Value)[] overrides)
    {
        var ids = new[] { "STR", "CON", "SIZ", "INT", "POW", "DEX", "CHA", "EDU" };
        var characteristics = ids.Select(id => new CharacteristicDefinition(
            new CharacteristicId(id), id, Minimum: 1, Maximum: null, RollName: null));
        var table = new DamageModifierTable(new[] { new DamageModifierBand(-1000, null, null) });
        var ruleset = new AbilityRuleset(
            characteristics, table, 10, 1, 10, 5, 2, 2, 2);
        var values = ids.ToDictionary(id => new CharacteristicId(id), _ => 10);
        foreach (var (id, value) in overrides)
        {
            values[new CharacteristicId(id)] = value;
        }

        return new AbilitySet(ruleset, values);
    }

    [Fact]
    public void A_category_missing_from_the_map_is_rejected_at_construction()
    {
        var withoutPhysical = AllSix().Where(s => s.Category != SkillCategory.Physical);

        Assert.Throws<ArgumentException>(() => new SkillCategoryBonusRuleset(10, 1, 2, withoutPhysical));
    }

    [Fact]
    public void A_duplicate_category_is_rejected_at_construction()
    {
        var withDuplicate = AllSix().Append(Spec(SkillCategory.Combat, "DEX"));

        Assert.Throws<ArgumentException>(() => new SkillCategoryBonusRuleset(10, 1, 2, withDuplicate));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Non_positive_points_per_modifier_are_rejected(int badDivisor)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new SkillCategoryBonusRuleset(10, badDivisor, 2, AllSix()));
        Assert.Throws<ArgumentOutOfRangeException>(() => new SkillCategoryBonusRuleset(10, 1, badDivisor, AllSix()));
    }

    [Fact]
    public void Secondary_magnitude_rounds_down_toward_zero_for_odd_deviations_in_both_directions()
    {
        // Mental secondary = POW. POW 7 is -3 from neutral -> -1 (magnitude 1, not 1.5 up to 2);
        // POW 13 is +3 -> +1. INT/EDU held neutral so only the secondary contributes.
        Assert.Equal(-1, Full().BonusFor(SkillCategory.Mental, Abilities(("POW", 7))));
        Assert.Equal(1, Full().BonusFor(SkillCategory.Mental, Abilities(("POW", 13))));
    }

    [Fact]
    public void A_negative_characteristic_subtracts_an_inverted_primary()
    {
        // Physical negative = SIZ. High SIZ hurts, low SIZ helps (Ch 2 p.18).
        Assert.Equal(-5, Full().BonusFor(SkillCategory.Physical, Abilities(("SIZ", 15))));
        Assert.Equal(5, Full().BonusFor(SkillCategory.Physical, Abilities(("SIZ", 5))));
    }

    [Fact]
    public void The_bonus_is_a_live_read_that_moves_when_a_characteristic_changes()
    {
        var policy = Full();
        var abilities = Abilities(("DEX", 15)); // Combat primary DEX 15 -> +5
        Assert.Equal(5, policy.BonusFor(SkillCategory.Combat, abilities));

        abilities.Set(new CharacteristicId("DEX"), 8); // drained to 8 -> -2

        Assert.Equal(-2, policy.BonusFor(SkillCategory.Combat, abilities));
    }

    [Fact]
    public void An_unknown_category_lookup_throws()
    {
        // Every enum value is configured, so this can only happen for an out-of-range cast.
        Assert.Throws<KeyNotFoundException>(() => Full().BonusFor((SkillCategory)999, Abilities()));
    }
}
