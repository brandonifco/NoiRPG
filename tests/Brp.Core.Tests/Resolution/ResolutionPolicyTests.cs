using Brp.Core.Primitives;
using Brp.Core.Resolution;

namespace Brp.Core.Tests.Resolution;

/// <summary>
/// Pins the grading constants and the one boundary this kernel deliberately does not own.
/// </summary>
public class ResolutionPolicyTests
{
    [Fact]
    public void Standard_policy_holds_the_printed_constants()
    {
        var policy = ResolutionPolicy.Standard;

        Assert.Equal(20, policy.CriticalDivisor);
        Assert.Equal(5, policy.SpecialDivisor);
        Assert.Equal(96, policy.AlwaysFailsAtOrAbove);
        Assert.Equal(95, policy.FumbleBandAnchor);
        Assert.Equal(5, policy.MinimumSuccessFloorChance);
        Assert.Equal(5, policy.MinimumSuccessFloorRoll);
        Assert.Equal(100, ResolutionPolicy.MaximumRoll);
    }

    [Theory]
    [InlineData(20, 1, 4, 96)]     // lowest band: fumble anchored at 96
    [InlineData(21, 2, 5, 97)]     // first critical step
    [InlineData(81, 5, 17, 100)]   // fumble collapses to 00 alone
    [InlineData(120, 6, 24, 100)]  // last printed row; the min() clamp engages
    public void Standard_policy_reproduces_printed_thresholds(
        int chance, int expectedCritical, int expectedSpecial, int expectedFumble)
    {
        var policy = ResolutionPolicy.Standard;
        var effective = Percent.Of(chance);

        Assert.Equal(expectedCritical, policy.CriticalThreshold(effective));
        Assert.Equal(expectedSpecial, policy.SpecialThreshold(effective));
        Assert.Equal(expectedFumble, policy.FumbleThreshold(effective));
    }

    /// <summary>
    /// Pins the boundary two independent conformance passes flagged. This assertion is
    /// CORRECT for skill and action rolls -- the book's 96+ rule is unconditional for them.
    /// It is deliberately WRONG for a resistance roll, which the same paragraph exempts: with
    /// a ten-point characteristic advantage only a roll of 00 fails.
    /// <para>
    /// The test exists so that whoever implements resistance rolls has to confront this rather
    /// than inherit it. If resistance is ever routed through <see cref="SkillResolver"/>, this
    /// test still passing is the signal that the exemption was missed.
    /// </para>
    /// </summary>
    [Fact]
    public void Skill_rolls_fail_at_96_even_at_full_chance_which_is_why_resistance_is_a_separate_path()
    {
        var outcome = SkillResolver.Resolve(Percent.Of(100), Percent.Of(100), roll: 96);

        Assert.Equal(SuccessLevel.Failure, outcome.Level);
        Assert.False(outcome.Succeeded);
    }
}
