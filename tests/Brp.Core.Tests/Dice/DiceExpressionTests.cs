using Brp.Core.Dice;
using Brp.Core.Randomness;

namespace Brp.Core.Tests.Dice;

public class DiceExpressionTests
{
    // Every notation form the in-scope rules use (per Issue #9), with its valid Total range
    // when rolled with no damage bonus in context.
    public static TheoryData<string, int, int> ValidNotationRanges => new()
    {
        { "3D6", 3, 18 },
        { "2D6+6", 8, 18 },
        { "1D8", 1, 8 },
        { "1D3", 1, 3 },
        { "2D8", 2, 16 },
        { "1D10", 1, 10 },
        { "1D100", 1, 100 },
        { "1D8+2", 3, 10 },
        { "1D6+1", 2, 7 },
        { "1D10+3", 4, 13 },
        { "2D6+2", 4, 14 },
        { "1D3+1", 2, 4 },
        { "1D6-2", 0, 4 }, // floors at zero, so the low end is 0 rather than -1
        { "1D10+1D4", 2, 14 },
        { "D6", 1, 6 }, // omitted count means 1
        { "d6", 1, 6 }, // case-insensitive
        { "3d6", 3, 18 }, // case-insensitive with a count
        { " 3 D 6 + 2 ", 5, 20 }, // whitespace-tolerant
        { "+1D6", 1, 6 }, // leading sign allowed
    };

    [Theory]
    [MemberData(nameof(ValidNotationRanges))]
    public void Notation_forms_parse_and_evaluate_in_range(string notation, int min, int max)
    {
        var expression = DiceExpression.Parse(notation);
        IEntropySource entropy = new Xoshiro256StarStar(seed: 1);

        for (var i = 0; i < 200; i++)
        {
            var roll = expression.Roll(entropy);
            Assert.InRange(roll.Total, min, max);
        }
    }

    [Fact]
    public void Db_with_no_damage_bonus_in_context_evaluates_to_zero()
    {
        var expression = DiceExpression.Parse("1D8+2+db");
        IEntropySource entropy = new Xoshiro256StarStar(seed: 7);

        for (var i = 0; i < 200; i++)
        {
            var roll = expression.Roll(entropy);
            Assert.InRange(roll.Total, 3, 10);

            var dbTerm = roll.Terms[^1];
            Assert.Equal(0, dbTerm.Value);
            Assert.Empty(dbTerm.Faces);
        }
    }

    [Fact]
    public void Db_with_damage_bonus_in_context_is_added()
    {
        var expression = DiceExpression.Parse("1D8+2+db");
        var damageBonus = DiceExpression.Parse("1D4");
        var context = new DiceContext(damageBonus);
        IEntropySource entropy = new Xoshiro256StarStar(seed: 7);

        for (var i = 0; i < 200; i++)
        {
            var roll = expression.Roll(entropy, context);
            Assert.InRange(roll.Total, 4, 14);
        }
    }

    [Theory]
    [InlineData("½db")]
    [InlineData("db/2")]
    [InlineData("DB/2")]
    public void Half_damage_bonus_forms_round_a_positive_modifier_up(string notation)
    {
        var expression = DiceExpression.Parse(notation);

        // A constant-only damage bonus needs no entropy to evaluate, keeping the halving
        // arithmetic isolated from die rolls.
        var context = new DiceContext(DiceExpression.Parse("5"));
        IEntropySource entropy = new Xoshiro256StarStar(seed: 3);

        Assert.Equal(3, expression.Roll(entropy, context).Total); // ceil(5/2) = 3
    }

    [Fact]
    public void Half_damage_bonus_of_a_negative_modifier_rounds_up_in_magnitude_before_flooring()
    {
        var expression = DiceExpression.Parse("db/2");
        var context = new DiceContext(DiceExpression.Parse("-3"));
        IEntropySource entropy = new Xoshiro256StarStar(seed: 3);

        var roll = expression.Roll(entropy, context);

        // -3 halved, rounding the magnitude up, is -2 (not -1). The Total then floors at
        // zero, but RawTotal must show the true -2 so the two behaviors are distinguishable.
        Assert.Equal(-2, roll.RawTotal);
        Assert.Equal(0, roll.Total);
        Assert.True(roll.WasFloored);
    }

    [Fact]
    public void Db_and_half_db_forms_agree_given_the_same_damage_bonus_and_entropy()
    {
        var dbHalf1 = DiceExpression.Parse("½db");
        var dbHalf2 = DiceExpression.Parse("db/2");
        var damageBonus = DiceExpression.Parse("2D4");
        var context = new DiceContext(damageBonus);

        for (var seed = 0; seed < 50; seed++)
        {
            IEntropySource entropyA = new Xoshiro256StarStar((ulong)seed);
            IEntropySource entropyB = new Xoshiro256StarStar((ulong)seed);

            var rollA = dbHalf1.Roll(entropyA, context);
            var rollB = dbHalf2.Roll(entropyB, context);

            Assert.Equal(rollA.Total, rollB.Total);
            Assert.Equal(rollA.RawTotal, rollB.RawTotal);
        }
    }

    [Theory]
    [MemberData(nameof(ValidNotationRanges))]
    public void Notation_round_trips_through_parse_and_render(string notation, int min, int max)
    {
        Assert.True(min <= max); // sanity check on the shared theory data, not the behavior under test

        var once = DiceExpression.Parse(notation).Notation;
        var twice = DiceExpression.Parse(once).Notation;

        Assert.Equal(once, twice);
    }

    [Theory]
    [InlineData("d6", "1D6")]
    [InlineData("D6", "1D6")]
    [InlineData("3d6", "3D6")]
    [InlineData(" 1d8 + 2 ", "1D8+2")]
    [InlineData("+1D6", "1D6")]
    [InlineData("-1D6", "-1D6")]
    [InlineData("1D6-2", "1D6-2")]
    [InlineData("½db", "DB/2")]
    [InlineData("db/2", "DB/2")]
    [InlineData("DB", "DB")]
    [InlineData("db", "DB")]
    [InlineData("1D8+2+db", "1D8+2+DB")]
    public void Notation_normalizes_as_expected(string notation, string expectedNotation)
    {
        Assert.Equal(expectedNotation, DiceExpression.Parse(notation).Notation);
    }

    [Fact]
    public void Negative_modifier_driving_below_zero_floors_the_total_but_keeps_a_negative_RawTotal()
    {
        var expression = DiceExpression.Parse("1D6-2");
        IEntropySource entropy = new FixedEntropySource(1); // 1 - 2 = -1

        var roll = expression.Roll(entropy);

        Assert.Equal(0, roll.Total);
        Assert.Equal(-1, roll.RawTotal);
        Assert.True(roll.WasFloored);
    }

    [Fact]
    public void A_result_that_does_not_go_negative_is_not_marked_as_floored()
    {
        var expression = DiceExpression.Parse("1D6-2");
        IEntropySource entropy = new FixedEntropySource(6); // 6 - 2 = 4

        var roll = expression.Roll(entropy);

        Assert.Equal(4, roll.Total);
        Assert.Equal(4, roll.RawTotal);
        Assert.False(roll.WasFloored);
    }

    [Fact]
    public void Individual_faces_are_recoverable_and_consistent_with_the_term_value()
    {
        var expression = DiceExpression.Parse("3D6");
        IEntropySource entropy = new FixedEntropySource(2, 3, 4);

        var roll = expression.Roll(entropy);
        var term = Assert.Single(roll.Terms);

        int[] expectedFaces = [2, 3, 4];
        Assert.Equal(expectedFaces, term.Faces);
        Assert.Equal(9, term.Value);
        Assert.Equal(9, roll.Total);
        Assert.Equal(9, roll.RawTotal);
    }

    [Fact]
    public void A_negative_dice_term_keeps_positive_faces_but_a_negative_value()
    {
        var expression = DiceExpression.Parse("-1D6");
        IEntropySource entropy = new FixedEntropySource(5);

        var roll = expression.Roll(entropy);
        var term = Assert.Single(roll.Terms);

        int[] expectedFaces = [5];
        Assert.Equal(expectedFaces, term.Faces);
        Assert.Equal(-5, term.Value);
        Assert.Equal(-5, roll.RawTotal);
        Assert.Equal(0, roll.Total);
    }

    [Fact]
    public void Db_term_propagates_the_underlying_dice_faces_in_roll_order()
    {
        var expression = DiceExpression.Parse("1D8+2+db");
        var damageBonus = DiceExpression.Parse("1D4");
        var context = new DiceContext(damageBonus);
        IEntropySource entropy = new FixedEntropySource(5, 3); // 1D8 -> 5, then the db's 1D4 -> 3

        var roll = expression.Roll(entropy, context);

        int[] diceFaces = [5];
        int[] damageBonusFaces = [3];
        Assert.Equal(diceFaces, roll.Terms[0].Faces);
        Assert.Empty(roll.Terms[1].Faces);
        Assert.Equal(damageBonusFaces, roll.Terms[2].Faces);
        Assert.Equal(3, roll.Terms[2].Value);
        Assert.Equal(10, roll.Total); // 5 + 2 + 3
    }

    [Fact]
    public void Half_db_term_propagates_the_underlying_dice_faces()
    {
        var expression = DiceExpression.Parse("½db");
        var damageBonus = DiceExpression.Parse("2D4");
        var context = new DiceContext(damageBonus);
        IEntropySource entropy = new FixedEntropySource(1, 4); // db rolls to 1 + 4 = 5

        var roll = expression.Roll(entropy, context);
        var term = Assert.Single(roll.Terms);

        int[] expectedFaces = [1, 4];
        Assert.Equal(expectedFaces, term.Faces);
        Assert.Equal(3, term.Value); // ceil(5 / 2)
    }

    [Fact]
    public void Same_seed_and_call_sequence_produces_an_identical_roll_sequence()
    {
        var expression = DiceExpression.Parse("3D6+1D4+db");
        var context = new DiceContext(DiceExpression.Parse("1D4"));

        IEntropySource entropyA = new Xoshiro256StarStar(seed: 2468);
        IEntropySource entropyB = new Xoshiro256StarStar(seed: 2468);

        for (var i = 0; i < 300; i++)
        {
            var rollA = expression.Roll(entropyA, context);
            var rollB = expression.Roll(entropyB, context);

            Assert.Equal(rollA.Total, rollB.Total);
            Assert.Equal(rollA.RawTotal, rollB.RawTotal);
            Assert.Equal(
                rollA.Terms.SelectMany(t => t.Faces),
                rollB.Terms.SelectMany(t => t.Faces));
        }
    }

    [Fact]
    public void Capture_and_restore_mid_sequence_resumes_an_identical_roll_sequence()
    {
        var expression = DiceExpression.Parse("2D6+1D8+db");
        var context = new DiceContext(DiceExpression.Parse("1D4-1"));
        var entropy = new Xoshiro256StarStar(seed: 909);

        for (var i = 0; i < 50; i++)
        {
            expression.Roll(entropy, context);
        }

        var checkpoint = entropy.Capture();

        var continuedA = new List<(int Total, int RawTotal)>();
        for (var i = 0; i < 100; i++)
        {
            var roll = expression.Roll(entropy, context);
            continuedA.Add((roll.Total, roll.RawTotal));
        }

        entropy.Restore(checkpoint);

        var continuedB = new List<(int Total, int RawTotal)>();
        for (var i = 0; i < 100; i++)
        {
            var roll = expression.Roll(entropy, context);
            continuedB.Add((roll.Total, roll.RawTotal));
        }

        Assert.Equal(continuedA, continuedB);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("abc")]
    [InlineData("1D")]
    [InlineData("D")]
    [InlineData("0D6")]
    [InlineData("1D0")]
    [InlineData("1001D6")]
    [InlineData("1D6+")]
    [InlineData("++1D6")]
    [InlineData("1D6++2")]
    [InlineData("1D6*2")]
    [InlineData("sdb")]
    [InlineData("1.5D6")]
    [InlineData("-")]
    public void Invalid_notation_fails_TryParse_and_throws_on_Parse(string notation)
    {
        Assert.False(DiceExpression.TryParse(notation, out var expression));
        Assert.Null(expression);
        Assert.Throws<FormatException>(() => DiceExpression.Parse(notation));
    }

    [Fact]
    public void Parse_throws_null_for_null_notation()
    {
        Assert.Throws<FormatException>(() => DiceExpression.Parse(null!));
        Assert.False(DiceExpression.TryParse(null!, out _));
    }

    [Fact]
    public void Exceeding_the_dice_count_cap_names_the_cap_in_the_message()
    {
        var exception = Assert.Throws<FormatException>(() => DiceExpression.Parse("1001D6"));
        Assert.Contains("1000", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Roll_rejects_a_null_entropy_source()
    {
        var expression = DiceExpression.Parse("1D6");
        Assert.Throws<ArgumentNullException>(() => expression.Roll(null!));
    }
}
