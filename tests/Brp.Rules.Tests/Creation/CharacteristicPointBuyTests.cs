using Brp.Core.Abilities;
using Brp.Data;
using Brp.Rules.Creation;

namespace Brp.Rules.Tests.Creation;

public class CharacteristicPointBuyTests
{
    private static readonly CharacterCreationRuleset Ruleset = NoirCharacterCreationRuleset.Load();
    private static readonly AbilityRuleset AbilityRuleset = NoirAbilityRuleset.Load();

    private static Dictionary<CharacteristicId, int> ZeroDeltas() => Ruleset.CharacteristicCosts.Keys
        .ToDictionary(id => id, _ => 0);

    [Fact]
    public void Ruleset_matches_ch2_point_based_character_creation()
    {
        // Ch 2: Characters, "Point-Based Character Creation (option)" (pp.9-10).
        Assert.Equal(24, Ruleset.CharacteristicPointPool);
        Assert.Equal(10, Ruleset.StartingCharacteristicValue);
        Assert.Equal(21, Ruleset.CharacteristicCreationMaximum);
        Assert.Equal(1, Ruleset.CharacteristicCosts[new CharacteristicId("STR")]);
        Assert.Equal(1, Ruleset.CharacteristicCosts[new CharacteristicId("CON")]);
        Assert.Equal(1, Ruleset.CharacteristicCosts[new CharacteristicId("SIZ")]);
        Assert.Equal(1, Ruleset.CharacteristicCosts[new CharacteristicId("CHA")]);
        Assert.Equal(3, Ruleset.CharacteristicCosts[new CharacteristicId("DEX")]);
        Assert.Equal(3, Ruleset.CharacteristicCosts[new CharacteristicId("INT")]);
        Assert.Equal(3, Ruleset.CharacteristicCosts[new CharacteristicId("POW")]);
    }

    [Fact]
    public void All_characteristics_default_to_ten_when_no_points_are_spent()
    {
        var result = CharacteristicPointBuy.Allocate(Ruleset, AbilityRuleset, ZeroDeltas());

        Assert.All(result.Values, v => Assert.Equal(10, v));
    }

    [Fact]
    public void Raising_a_one_point_characteristic_costs_one_pool_point_per_point()
    {
        var deltas = ZeroDeltas();
        deltas[new CharacteristicId("STR")] = 11; // 10 -> 21, costs 11
        deltas[new CharacteristicId("CON")] = -7; // 10 -> 3, refunds 7

        var result = CharacteristicPointBuy.Allocate(Ruleset, AbilityRuleset, deltas);

        Assert.Equal(21, result[new CharacteristicId("STR")]);
        Assert.Equal(3, result[new CharacteristicId("CON")]);
    }

    [Fact]
    public void Raising_a_three_point_characteristic_costs_three_pool_points_per_point()
    {
        var deltas = ZeroDeltas();
        deltas[new CharacteristicId("DEX")] = 8; // 10 -> 18, costs 24 (the whole pool)

        var result = CharacteristicPointBuy.Allocate(Ruleset, AbilityRuleset, deltas);

        Assert.Equal(18, result[new CharacteristicId("DEX")]);
    }

    [Fact]
    public void Spend_exceeding_the_point_pool_is_rejected()
    {
        var deltas = ZeroDeltas();
        deltas[new CharacteristicId("DEX")] = 9; // costs 27, exceeds the 24-point pool

        Assert.Throws<ArgumentException>(() => CharacteristicPointBuy.Allocate(Ruleset, AbilityRuleset, deltas));
    }

    [Fact]
    public void No_initial_characteristic_may_be_raised_above_the_creation_maximum_of_21()
    {
        // Ch 2 p.9: "No initial characteristic can be raised to higher than 21" -- this
        // creation-time ceiling applies even to INT and POW, whose ongoing-play maximum
        // (AbilityRuleset.Maximum) is unbounded.
        var deltas = ZeroDeltas();
        deltas[new CharacteristicId("INT")] = 12; // 10 -> 22, above the creation maximum

        Assert.Throws<ArgumentOutOfRangeException>(() => CharacteristicPointBuy.Allocate(Ruleset, AbilityRuleset, deltas));
    }

    [Fact]
    public void Siz_and_int_cannot_be_lowered_below_their_printed_floor_of_eight()
    {
        var deltas = ZeroDeltas();
        deltas[new CharacteristicId("SIZ")] = -3; // 10 -> 7, below SIZ's floor of 8

        Assert.Throws<ArgumentOutOfRangeException>(() => CharacteristicPointBuy.Allocate(Ruleset, AbilityRuleset, deltas));
    }

    [Fact]
    public void Other_characteristics_cannot_be_lowered_below_the_general_floor_of_three()
    {
        var deltas = ZeroDeltas();
        deltas[new CharacteristicId("CHA")] = -8; // 10 -> 2, below the general floor of 3

        Assert.Throws<ArgumentOutOfRangeException>(() => CharacteristicPointBuy.Allocate(Ruleset, AbilityRuleset, deltas));
    }

    [Fact]
    public void Shift_moves_points_between_characteristics_without_spending_the_pool()
    {
        var allocated = CharacteristicPointBuy.Allocate(Ruleset, AbilityRuleset, ZeroDeltas());
        var shift = new Dictionary<CharacteristicId, int>
        {
            [new CharacteristicId("STR")] = 3,
            [new CharacteristicId("CHA")] = -3,
        };

        var shifted = CharacteristicPointBuy.ApplyShift(Ruleset, AbilityRuleset, allocated, shift);

        Assert.Equal(13, shifted[new CharacteristicId("STR")]);
        Assert.Equal(7, shifted[new CharacteristicId("CHA")]);
    }

    [Fact]
    public void Shift_beyond_the_three_point_allowance_is_rejected()
    {
        var allocated = CharacteristicPointBuy.Allocate(Ruleset, AbilityRuleset, ZeroDeltas());
        var shift = new Dictionary<CharacteristicId, int>
        {
            [new CharacteristicId("STR")] = 4,
            [new CharacteristicId("CHA")] = -4,
        };

        Assert.Throws<ArgumentException>(() => CharacteristicPointBuy.ApplyShift(Ruleset, AbilityRuleset, allocated, shift));
    }

    [Fact]
    public void A_non_zero_sum_shift_is_rejected()
    {
        var allocated = CharacteristicPointBuy.Allocate(Ruleset, AbilityRuleset, ZeroDeltas());
        var shift = new Dictionary<CharacteristicId, int>
        {
            [new CharacteristicId("STR")] = 2,
            [new CharacteristicId("CHA")] = -1,
        };

        Assert.Throws<ArgumentException>(() => CharacteristicPointBuy.ApplyShift(Ruleset, AbilityRuleset, allocated, shift));
    }
}
