using Brp.Rules.Skills;

namespace Brp.Data.Tests;

/// <summary>
/// Confirms the shipped Complementary Skills / Augment fraction loads and matches the book: Ch 3:
/// Skills, "Augments and Complementary skills" (p.34) (Issue #114). See
/// <c>docs/decisions/NNNN-complementary-skills-and-augments.md</c>.
/// </summary>
public class NoirComplementarySkillsRulesetTests
{
    private static readonly ComplementarySkillRuleset Ruleset = NoirComplementarySkillsRuleset.Load();

    [Fact]
    public void Bonus_fraction_matches_the_book_page_34()
    {
        // "your character may temporarily add 1/5 of your rating in a complementary skill to
        // your rating in another skill for skill rolls."
        Assert.Equal(1, Ruleset.BonusNumerator);
        Assert.Equal(5, Ruleset.BonusDenominator);
    }
}
