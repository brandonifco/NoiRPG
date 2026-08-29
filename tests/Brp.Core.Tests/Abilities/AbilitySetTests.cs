using Brp.Core.Abilities;
using Brp.Data;

namespace Brp.Core.Tests.Abilities;

public class AbilitySetTests
{
    public static IEnumerable<object[]> PlausibleConAndSize =>
        from constitution in Enumerable.Range(3, 19)
        from size in Enumerable.Range(8, 14)
        select new object[] { constitution, size };

    public static IEnumerable<object[]> PlausibleIntelligence =>
        Enumerable.Range(8, 14).Select(value => new object[] { value });

    [Theory]
    [InlineData("STR", 2)]
    [InlineData("CON", 22)]
    [InlineData("SIZ", 7)]
    [InlineData("INT", 7)]
    [InlineData("DEX", 22)]
    [InlineData("CHA", 2)]
    public void Characteristic_bounds_are_enforced_from_the_ruleset_data(string id, int value)
    {
        var abilities = Create();

        Assert.Throws<ArgumentOutOfRangeException>(() => abilities.Set(new CharacteristicId(id), value));
    }

    [Theory]
    [InlineData("STR", 2)]
    [InlineData("CON", 22)]
    [InlineData("SIZ", 7)]
    [InlineData("INT", 7)]
    [InlineData("DEX", 22)]
    [InlineData("CHA", 2)]
    public void Initial_characteristic_values_are_checked_against_the_same_data_backed_bounds(string id, int value)
    {
        var ruleset = NoirAbilityRuleset.Load();
        var values = ruleset.Characteristics.Keys.ToDictionary(characteristic => characteristic, _ => 12);
        values[new CharacteristicId(id)] = value;

        Assert.Throws<ArgumentOutOfRangeException>(() => new AbilitySet(ruleset, values));
    }

    [Theory]
    [InlineData("INT", 500)]
    [InlineData("POW", 500)]
    [InlineData("EDU", 500)]
    public void Mental_characteristics_have_no_maximum(string id, int value)
    {
        var abilities = Create();

        abilities.Set(new CharacteristicId(id), value);

        Assert.Equal(value, abilities.ValueOf(new CharacteristicId(id)));
    }

    [Theory]
    [MemberData(nameof(PlausibleConAndSize))]
    public void Hit_points_round_up_for_the_full_physical_characteristic_range(int constitution, int size)
    {
        var abilities = Create(constitution: constitution, size: size);

        Assert.Equal((constitution + size + 1) / 2, abilities.MaximumHitPoints);
    }

    [Theory]
    [MemberData(nameof(PlausibleIntelligence))]
    public void Experience_bonus_rounds_up_for_the_full_plausible_intelligence_range(int intelligence)
    {
        var abilities = Create(intelligence: intelligence);

        Assert.Equal((intelligence + 1) / 2, abilities.ExperienceBonus);
    }

    [Theory]
    [MemberData(nameof(PlausibleConAndSize))]
    public void Major_wound_level_rounds_up_for_each_hit_point_parity(int constitution, int size)
    {
        var abilities = Create(constitution: constitution, size: size);

        Assert.Equal((abilities.MaximumHitPoints + 1) / 2, abilities.MajorWoundLevel);
    }

    [Fact]
    public void Derived_values_update_immediately_for_the_books_poison_example()
    {
        var abilities = Create(constitution: 16, size: 14);
        Assert.Equal(15, abilities.MaximumHitPoints);
        Assert.Equal(8, abilities.MajorWoundLevel);

        abilities.Set(new CharacteristicId("CON"), 10);

        Assert.Equal(12, abilities.MaximumHitPoints);
        Assert.Equal(6, abilities.MajorWoundLevel);
    }

    [Fact]
    public void Current_hit_points_below_a_reduced_maximum_take_no_additional_damage()
    {
        var abilities = Create(constitution: 16, size: 14);
        abilities.SetCurrentHitPoints(10);

        abilities.Set(new CharacteristicId("CON"), 10);

        Assert.Equal(10, abilities.CurrentHitPoints);
    }

    [Fact]
    public void Current_hit_points_are_capped_on_change_and_do_not_resurrect_when_the_maximum_is_restored()
    {
        var abilities = Create(constitution: 16, size: 14);
        abilities.SetCurrentHitPoints(15);
        abilities.Set(new CharacteristicId("CON"), 10);
        abilities.Set(new CharacteristicId("CON"), 16);

        Assert.Equal(12, abilities.CurrentHitPoints);
        Assert.Equal(15, abilities.MaximumHitPoints);
    }

    [Fact]
    public void Current_hit_points_can_remain_negative_when_the_maximum_changes()
    {
        var abilities = Create(constitution: 16, size: 14);
        abilities.SetCurrentHitPoints(-3);
        abilities.Set(new CharacteristicId("CON"), 10);

        Assert.Equal(-3, abilities.CurrentHitPoints);
    }

    [Theory]
    [InlineData("STR", 10, "STR", 5)]
    [InlineData("CON", 10, "CON", 5)]
    [InlineData("INT", 10, "INT", 5)]
    [InlineData("POW", 10, "POW", 5)]
    [InlineData("DEX", 10, "DEX", 5)]
    [InlineData("CHA", 10, "CHA", 5)]
    public void Every_disease_drained_characteristic_changes_its_associated_live_roll(
        string changedId, int changedValue, string rollId, int multiplier)
    {
        var abilities = Create();
        var roll = abilities.Ruleset.CharacteristicRoll(new CharacteristicId(rollId), multiplier);

        abilities.Set(new CharacteristicId(changedId), changedValue);

        Assert.Equal(changedValue * multiplier, roll.ChanceFor(abilities.ValueOf(roll.Characteristic)).Value);
    }

    [Fact]
    public void Strength_drain_updates_the_damage_modifier_at_a_table_boundary()
    {
        var abilities = Create(strength: 13, size: 12);
        Assert.Equal("1D4", abilities.DamageModifier?.Notation);

        abilities.Set(new CharacteristicId("STR"), 12);

        Assert.Null(abilities.DamageModifier);
    }

    [Fact]
    public void Intelligence_drain_updates_experience_bonus()
    {
        var abilities = Create(intelligence: 11);
        Assert.Equal(6, abilities.ExperienceBonus);

        abilities.Set(new CharacteristicId("INT"), 10);

        Assert.Equal(5, abilities.ExperienceBonus);
    }

    [Theory]
    [InlineData("POW")]
    [InlineData("DEX")]
    [InlineData("CHA")]
    public void Disease_drains_without_a_chapter_2_derived_formula_leave_other_derived_values_unchanged(string id)
    {
        var abilities = Create();
        var before = (abilities.MaximumHitPoints, abilities.MajorWoundLevel, abilities.ExperienceBonus, abilities.DamageModifier?.Notation);

        abilities.Set(new CharacteristicId(id), 10);

        Assert.Equal(before, (abilities.MaximumHitPoints, abilities.MajorWoundLevel, abilities.ExperienceBonus, abilities.DamageModifier?.Notation));
    }

    [Fact]
    public void Movement_is_not_a_characteristic_formula()
    {
        var abilities = Create();
        abilities.Set(new CharacteristicId("DEX"), 3);

        Assert.Equal(10, abilities.Movement);
    }

    private static AbilitySet Create(
        int constitution = 12,
        int size = 12,
        int intelligence = 12,
        int strength = 12)
    {
        var ruleset = NoirAbilityRuleset.Load();
        var values = ruleset.Characteristics.Keys.ToDictionary(id => id, _ => 12);
        values[new CharacteristicId("CON")] = constitution;
        values[new CharacteristicId("SIZ")] = size;
        values[new CharacteristicId("INT")] = intelligence;
        values[new CharacteristicId("STR")] = strength;
        return new AbilitySet(ruleset, values);
    }
}
