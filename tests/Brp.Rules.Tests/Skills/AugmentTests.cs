using Brp.Core.Modifiers;
using Brp.Core.Primitives;
using Brp.Rules.Skills;

namespace Brp.Rules.Tests.Skills;

/// <summary>
/// Ch 3: Skills, "Augments and Complementary skills" (p.34) (Issue #114): the roll-based
/// difficulty-shift half of the rule. See
/// <c>docs/decisions/NNNN-complementary-skills-and-augments.md</c>.
/// </summary>
public class AugmentTests
{
    [Fact]
    public void Successful_augment_shifts_the_primary_roll_easier_by_one_step()
    {
        // "If the augmenting skill roll is successful, you may adjust the difficulty of the
        // primary skill by one step, such as turning a Difficult roll into an Average one."
        var modifier = Augment.DifficultyShift("Streetwise", augmentSucceeded: true);
        Assert.Equal(DifficultyDirection.Easier, modifier.Direction);

        var chain = ModifierPipeline.Evaluate(
            Percent.Of(30), [DifficultyModifier.Difficult("test"), modifier]);

        // Ch 3 p.34: "only one degree of adjustment is possible" -- exactly ADR 0007's
        // non-stacking difficulty state, so a Difficult condition plus a successful augment
        // cancel pairwise back to the unmodified rating rather than composing into two shifts.
        Assert.Equal(30, chain.EffectiveChance!.Value.Value);
    }

    [Fact]
    public void Failed_augment_shifts_the_primary_roll_harder_by_one_step()
    {
        // "If the augment fails, the primary skill is adjusted by one step [toward Difficult]
        // due to confusion or conflicting information."
        var modifier = Augment.DifficultyShift("Streetwise", augmentSucceeded: false);
        Assert.Equal(DifficultyDirection.Harder, modifier.Direction);

        var chain = ModifierPipeline.Evaluate(Percent.Of(60), [modifier]);

        // Round up per the standard policy: 60 / 2 = 30.
        Assert.Equal(30, chain.EffectiveChance!.Value.Value);
    }

    [Fact]
    public void Only_one_degree_of_adjustment_is_possible_even_with_multiple_failed_augments()
    {
        // "[O]nly one degree of adjustment is possible" -- a second failed augment (were a
        // gamemaster to allow stacking helper skills at all, which the book forbids elsewhere)
        // must not compound into a second halving; ADR 0007's collapse already guarantees this.
        var first = Augment.DifficultyShift("Streetwise", augmentSucceeded: false);
        var second = Augment.DifficultyShift("Insight", augmentSucceeded: false);

        var chain = ModifierPipeline.Evaluate(Percent.Of(60), [first, second]);

        Assert.Equal(30, chain.EffectiveChance!.Value.Value);
    }
}
