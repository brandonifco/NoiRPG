using Brp.Core.Abilities;
using Brp.Core.Skills;

namespace Brp.Data.Tests;

/// <summary>
/// Confirms the shipped skill-category-bonus data loads and reproduces Ch 2: Characters,
/// "Skill Category Bonuses (Option)" -- the "Skill Category Modifiers" table and the "Skill
/// Bonus Table" (pp.18-19). Both printed tables are reproduced in full (cell by cell) rather
/// than sampled, per AGENTS.md's table-backed-rule convention, and the chapter's worked example
/// is reproduced end to end. Book-verified against <c>tools/skill_bonus.py</c>. See
/// <c>docs/decisions/0006-skill-bonus-system.md</c> and
/// <c>docs/decisions/0022-skill-category-bonus-application.md</c>.
/// </summary>
public class NoirSkillCategoryBonusRulesetTests
{
    private static readonly SkillCategoryBonusRuleset Bonuses = NoirSkillCategoryBonusRuleset.Load();

    [Fact]
    public void Formula_parameters_match_chapter_2_page_18()
    {
        // "Primary ... +/-1% for every point ... Secondary ... +/-1% for every 2 points ...
        // round down." The neutral value the deviations are measured from is 10.
        Assert.Equal(10, Bonuses.NeutralCharacteristicValue);
        Assert.Equal(1, Bonuses.PrimaryPointsPerModifier);
        Assert.Equal(2, Bonuses.SecondaryPointsPerModifier);
    }

    // The printed "Skill Category Modifiers" table (Ch 2 p.18), reproduced row by row. Secondary
    // and negative lists are pipe-joined in declaration order; "" is an empty list.
    [Theory]
    [InlineData(SkillCategory.Combat, "DEX", "INT|STR", "")]
    [InlineData(SkillCategory.Communication, "INT", "POW|CHA", "")]
    [InlineData(SkillCategory.Manipulation, "DEX", "INT|STR", "")]
    [InlineData(SkillCategory.Mental, "INT", "POW|EDU", "")]
    [InlineData(SkillCategory.Perception, "INT", "POW|CON", "")]
    [InlineData(SkillCategory.Physical, "DEX", "STR|CON", "SIZ")]
    public void Skill_category_modifiers_table_maps_every_category_to_its_characteristics(
        SkillCategory category, string primary, string secondary, string negative)
    {
        var spec = Bonuses.Categories[category];

        Assert.Equal(primary, spec.Primary.Value);
        Assert.Equal(secondary, string.Join('|', spec.Secondary.Select(c => c.Value)));
        Assert.Equal(negative, string.Join('|', spec.Negative.Select(c => c.Value)));
    }

    // The printed "Skill Bonus Table" (Ch 2 pp.18-19): for a characteristic at each value 1-21,
    // the bonus contributed as a Primary, a Secondary, and a Negative characteristic. Values are
    // the book's printed cells, exercised through a live AbilitySet:
    //   Primary   via Combat's DEX (its INT/STR secondaries held neutral),
    //   Secondary via Mental's EDU (INT/POW held neutral) -- the same path skill_bonus.py pins,
    //   Negative  via Physical's SIZ (DEX/STR/CON held neutral).
    [Theory]
    [InlineData(1, -9, -4, 9)]
    [InlineData(2, -8, -4, 8)]
    [InlineData(3, -7, -3, 7)]
    [InlineData(4, -6, -3, 6)]
    [InlineData(5, -5, -2, 5)]
    [InlineData(6, -4, -2, 4)]
    [InlineData(7, -3, -1, 3)]
    [InlineData(8, -2, -1, 2)]
    [InlineData(9, -1, 0, 1)]
    [InlineData(10, 0, 0, 0)]
    [InlineData(11, 1, 0, -1)]
    [InlineData(12, 2, 1, -2)]
    [InlineData(13, 3, 1, -3)]
    [InlineData(14, 4, 2, -4)]
    [InlineData(15, 5, 2, -5)]
    [InlineData(16, 6, 3, -6)]
    [InlineData(17, 7, 3, -7)]
    [InlineData(18, 8, 4, -8)]
    [InlineData(19, 9, 4, -9)]
    [InlineData(20, 10, 5, -10)]
    [InlineData(21, 11, 5, -11)]
    public void Skill_bonus_table_reproduces_each_value_as_primary_secondary_and_negative(
        int value, int primary, int secondary, int negative)
    {
        Assert.Equal(primary, Bonuses.BonusFor(SkillCategory.Combat, Neutral(("DEX", value))));
        Assert.Equal(secondary, Bonuses.BonusFor(SkillCategory.Mental, Neutral(("EDU", value))));
        Assert.Equal(negative, Bonuses.BonusFor(SkillCategory.Physical, Neutral(("SIZ", value))));
    }

    // The worked example on Ch 2 p.18: STR 14, CON 13, INT 8, SIZ 12, POW 10, DEX 12, CHA 8.
    // The book leaves EDU "not used in this campaign"; EDU 10 is its neutral equivalent here, so
    // Mental still comes out -2. All seven printed values are within NoiRPG's characteristic
    // bounds, so this runs against the real ability ruleset.
    [Theory]
    [InlineData(SkillCategory.Combat, 3)]         // +2 DEX, +2 STR, -1 INT
    [InlineData(SkillCategory.Communication, -3)] // -2 INT, 0 POW, -1 CHA
    [InlineData(SkillCategory.Manipulation, 3)]   // +2 DEX, -1 INT, +2 STR
    [InlineData(SkillCategory.Mental, -2)]        // -2 INT, 0 POW, 0 EDU
    [InlineData(SkillCategory.Perception, -1)]    // -2 INT, 0 POW, +1 CON
    [InlineData(SkillCategory.Physical, 3)]       // +2 DEX, +2 STR, +1 CON, -2 SIZ
    public void Worked_example_from_chapter_2_page_18_reproduces_every_category_bonus(
        SkillCategory category, int expected)
    {
        var abilities = RealAbilities(
            ("STR", 14), ("CON", 13), ("INT", 8), ("SIZ", 12),
            ("POW", 10), ("DEX", 12), ("CHA", 8), ("EDU", 10));

        Assert.Equal(expected, Bonuses.BonusFor(category, abilities));
    }

    private static AbilitySet RealAbilities(params (string Id, int Value)[] overrides)
    {
        var ruleset = NoirAbilityRuleset.Load();
        var values = ruleset.Characteristics.Keys.ToDictionary(id => id, _ => 10);
        foreach (var (id, value) in overrides)
        {
            values[new CharacteristicId(id)] = value;
        }

        return new AbilitySet(ruleset, values);
    }

    // A permissive ability ruleset (every characteristic 1..infinity) so the full printed Skill
    // Bonus Table can be reproduced, including the value 1-2 rows that NoiRPG's real minimums put
    // out of reach. Only the characteristic bounds differ from the shipped data; the bonus math
    // under test is the shipped policy.
    private static AbilitySet Neutral(params (string Id, int Value)[] overrides)
    {
        var ids = new[] { "STR", "CON", "SIZ", "INT", "POW", "DEX", "CHA", "EDU" };
        var characteristics = ids.Select(id => new CharacteristicDefinition(
            new CharacteristicId(id), id, Minimum: 1, Maximum: null, RollName: null));
        var table = new DamageModifierTable(new[] { new DamageModifierBand(-1000, null, null) });
        var ruleset = new AbilityRuleset(
            characteristics, table, startingMovement: 10,
            minimumCharacteristicRollMultiplier: 1,
            maximumCharacteristicRollMultiplier: 10,
            standardCharacteristicRollMultiplier: 5,
            hitPointDivisor: 2, majorWoundDivisor: 2, experienceBonusDivisor: 2);

        var values = ids.ToDictionary(id => new CharacteristicId(id), _ => 10);
        foreach (var (id, value) in overrides)
        {
            values[new CharacteristicId(id)] = value;
        }

        return new AbilitySet(ruleset, values);
    }
}
