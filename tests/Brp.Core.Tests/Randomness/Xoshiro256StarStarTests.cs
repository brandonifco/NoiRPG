using Brp.Core.Randomness;

namespace Brp.Core.Tests.Randomness;

public class Xoshiro256StarStarTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(6)]
    [InlineData(8)]
    [InlineData(10)]
    [InlineData(12)]
    [InlineData(20)]
    [InlineData(100)]
    public void NextDie_only_ever_returns_values_in_range(int sides)
    {
        var entropy = new Xoshiro256StarStar(seed: 12345);

        for (var i = 0; i < 5000; i++)
        {
            var face = entropy.NextDie(sides);
            Assert.InRange(face, 1, sides);
        }
    }

    [Fact]
    public void NextDie_with_one_side_always_returns_one()
    {
        var entropy = new Xoshiro256StarStar(seed: 999);

        for (var i = 0; i < 50; i++)
        {
            Assert.Equal(1, entropy.NextDie(1));
        }
    }

    [Fact]
    public void NextDie_rejects_non_positive_sides()
    {
        var entropy = new Xoshiro256StarStar(seed: 1);

        Assert.Throws<ArgumentOutOfRangeException>(() => entropy.NextDie(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => entropy.NextDie(-6));
    }

    [Fact]
    public void NextD100_only_ever_returns_1_through_100()
    {
        var entropy = new Xoshiro256StarStar(seed: 424242);

        for (var i = 0; i < 5000; i++)
        {
            Assert.InRange(entropy.NextD100(), 1, 100);
        }
    }

    [Fact]
    public void Same_seed_and_call_sequence_reproduces_an_identical_long_sequence()
    {
        var first = new Xoshiro256StarStar(seed: 77);
        var second = new Xoshiro256StarStar(seed: 77);

        var firstSequence = new List<int>();
        var secondSequence = new List<int>();

        for (var i = 0; i < 1000; i++)
        {
            // Vary the call shape (mixed die sizes and d100 reads) so the assertion covers
            // more than "the same die repeated" -- the whole call sequence must line up.
            firstSequence.Add(i % 5 == 0 ? first.NextD100() : first.NextDie((i % 20) + 1));
            secondSequence.Add(i % 5 == 0 ? second.NextD100() : second.NextDie((i % 20) + 1));
        }

        Assert.Equal(firstSequence, secondSequence);
        Assert.Equal(first.DrawCount, second.DrawCount);
    }

    [Fact]
    public void Different_seeds_produce_different_sequences()
    {
        var first = new Xoshiro256StarStar(seed: 1);
        var second = new Xoshiro256StarStar(seed: 2);

        var firstSequence = Enumerable.Range(0, 50).Select(_ => first.NextDie(1_000_000)).ToList();
        var secondSequence = Enumerable.Range(0, 50).Select(_ => second.NextDie(1_000_000)).ToList();

        Assert.NotEqual(firstSequence, secondSequence);
    }

    [Fact]
    public void Capture_and_restore_mid_sequence_resumes_identically()
    {
        var source = new Xoshiro256StarStar(seed: 2024);

        for (var i = 0; i < 37; i++)
        {
            source.NextDie((i % 10) + 2);
        }

        var checkpoint = source.Capture();

        var continuedA = new List<int>();
        for (var i = 0; i < 200; i++)
        {
            continuedA.Add(i % 7 == 0 ? source.NextD100() : source.NextDie((i % 12) + 2));
        }

        source.Restore(checkpoint);

        var continuedB = new List<int>();
        for (var i = 0; i < 200; i++)
        {
            continuedB.Add(i % 7 == 0 ? source.NextD100() : source.NextDie((i % 12) + 2));
        }

        Assert.Equal(continuedA, continuedB);
    }

    [Fact]
    public void Capture_returns_the_current_draw_count_and_restore_reinstates_it()
    {
        var source = new Xoshiro256StarStar(seed: 55);

        for (var i = 0; i < 25; i++)
        {
            source.NextDie(6);
        }

        var checkpoint = source.Capture();
        Assert.Equal(checkpoint.DrawCount, source.DrawCount);

        source.NextDie(6);
        Assert.NotEqual(checkpoint.DrawCount, source.DrawCount);

        source.Restore(checkpoint);
        Assert.Equal(checkpoint.DrawCount, source.DrawCount);
    }
}
