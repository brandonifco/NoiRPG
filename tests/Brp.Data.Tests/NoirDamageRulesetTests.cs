namespace Brp.Data.Tests;

/// <summary>
/// Confirms the shipped damage ruleset data loads and carries the thresholds and knockout
/// duration cited in Ch 2 (p.13), Ch 6 (pp.154-156), and Ch 7 (p.174). See
/// <c>docs/decisions/0017-damage.md</c>.
/// </summary>
public class NoirDamageRulesetTests
{
    [Fact]
    public void Load_returns_the_printed_unconscious_and_dead_thresholds()
    {
        var ruleset = NoirDamageRuleset.Load();

        // Ch 2, p.13: "Your character loses consciousness when their hit points are reduced to
        // 2 or less, and if their hit points reach 0, they die at the end of the following round."
        Assert.Equal(2, ruleset.UnconsciousHitPointLevel);
        Assert.Equal(0, ruleset.DeadHitPointLevel);
    }

    [Fact]
    public void Load_returns_the_printed_knockout_duration_dice()
    {
        // Ch 7, p.174: "knocked out for 1D10+10 rounds."
        var ruleset = NoirDamageRuleset.Load();

        Assert.Equal("1D10+10", ruleset.KnockoutDuration.Notation);
    }
}
