using Brp.Core.Abilities;
using Brp.Core.Modifiers;
using Brp.Core.Resolution;
using Brp.Core.Tests.Dice;
using Brp.Data;

namespace Brp.Core.Tests.Abilities;

public class AbilityResolverTests
{
    [Theory]
    [InlineData(1, SuccessLevel.Critical)]
    [InlineData(5, SuccessLevel.Special)]
    [InlineData(20, SuccessLevel.Success)]
    [InlineData(90, SuccessLevel.Failure)]
    [InlineData(100, SuccessLevel.Fumble)]
    public void Characteristic_rolls_use_all_five_action_roll_grades(int result, SuccessLevel expected)
    {
        var abilities = Create(power: 10);
        var roll = abilities.Ruleset.CharacteristicRoll(new CharacteristicId("POW"), 5);

        var outcome = AbilityResolver.Resolve(abilities, roll, [], new FixedEntropySource(result));

        Assert.Equal(expected, outcome?.Level);
    }

    [Fact]
    public void Characteristic_rolls_fail_at_96_and_do_not_take_the_resistance_carve_out()
    {
        var abilities = Create(power: 21);
        var roll = abilities.Ruleset.CharacteristicRoll(new CharacteristicId("POW"), 5);

        var outcome = AbilityResolver.Resolve(abilities, roll, [], new FixedEntropySource(96));

        Assert.Equal(SuccessLevel.Failure, outcome?.Level);
    }

    [Fact]
    public void Difficulty_modifier_makes_easy_rolls_double_to_x10()
    {
        var abilities = Create(power: 10);
        var roll = abilities.Ruleset.StandardCharacteristicRoll(new CharacteristicId("POW"));

        var outcome = AbilityResolver.Resolve(
            abilities,
            roll,
            [DifficultyModifier.Easy("easy")],
            new FixedEntropySource(100));

        Assert.Equal(100, outcome?.EffectiveChance.Value);
    }

    [Fact]
    public void Difficulty_modifier_halves_difficult_characteristic_rolls_rounding_up()
    {
        var abilities = Create(power: 3);
        var roll = abilities.Ruleset.CharacteristicRoll(new CharacteristicId("POW"), 1);

        var outcome = AbilityResolver.Resolve(
            abilities,
            roll,
            [DifficultyModifier.Difficult("difficult")],
            new FixedEntropySource(2));

        Assert.Equal(2, outcome?.EffectiveChance.Value);
        Assert.Equal(SuccessLevel.Success, outcome?.Level);
    }

    [Fact]
    public void Characteristic_rolls_do_not_inherit_the_skill_only_5_percent_floor()
    {
        // Ch 5, "Skill Rolls" (p.128) says the 01-05 floor is for skills. POW 3 x2 begins
        // above the floor's 5% eligibility threshold, then Difficult reduces it to 3%; roll
        // 4 must fail instead of being rescued by that skill-only floor.
        var abilities = Create(power: 3);
        var roll = abilities.Ruleset.CharacteristicRoll(new CharacteristicId("POW"), 2);

        var outcome = AbilityResolver.Resolve(
            abilities,
            roll,
            [DifficultyModifier.Difficult("difficult")],
            new FixedEntropySource(4));

        Assert.Equal(SuccessLevel.Failure, outcome?.Level);
    }

    [Theory]
    [InlineData(1, 3)]
    [InlineData(2, 6)]
    [InlineData(3, 9)]
    [InlineData(4, 12)]
    [InlineData(5, 15)]
    public void Disease_recovery_multipliers_are_representable_by_the_same_roll_type(int multiplier, int expectedChance)
    {
        var abilities = Create(constitution: 3);
        var roll = abilities.Ruleset.CharacteristicRoll(new CharacteristicId("CON"), multiplier);

        Assert.Equal(expectedChance, roll.ChanceFor(abilities.ValueOf(roll.Characteristic)).Value);
    }

    [Fact]
    public void Standard_rolls_have_the_books_names_and_siz_has_no_roll()
    {
        var ruleset = NoirAbilityRuleset.Load();

        Assert.Equal("Charm", ruleset.Characteristics[new CharacteristicId("CHA")].RollName);
        Assert.Equal(5, ruleset.StandardCharacteristicRoll(new CharacteristicId("CHA")).Multiplier);
        Assert.Throws<InvalidOperationException>(
            () => ruleset.StandardCharacteristicRoll(new CharacteristicId("SIZ")));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(11)]
    public void Unsupported_characteristic_roll_multipliers_are_rejected(int multiplier)
    {
        var ruleset = NoirAbilityRuleset.Load();

        Assert.Throws<ArgumentOutOfRangeException>(
            () => ruleset.CharacteristicRoll(new CharacteristicId("CON"), multiplier));
    }

    private static AbilitySet Create(int constitution = 12, int power = 12)
    {
        var ruleset = NoirAbilityRuleset.Load();
        var values = ruleset.Characteristics.Keys.ToDictionary(id => id, _ => 12);
        values[new CharacteristicId("CON")] = constitution;
        values[new CharacteristicId("POW")] = power;
        return new AbilitySet(ruleset, values);
    }
}
