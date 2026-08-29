using Brp.Core.Contests;

namespace Brp.Core.Tests.Contests;

/// <summary>
/// Exercises <see cref="ResistanceResolver"/>'s automatic-success and automatic-failure rules
/// (Ch 5: System, "Failure" and "Resistance Rolls", p.127 and p.129) and confirms it is not
/// simply <c>Resolution.SkillResolver</c> under a different name -- see class remarks on
/// <see cref="ResistanceResolver"/> and the carried-forward pinning test in
/// <c>Resolution.ResolutionPolicyTests</c>.
/// </summary>
public class ResistanceResolverTests
{
    // active=20, passive=10: a 10-point active advantage -- the smallest difference that enters
    // the automatic-success zone (chance would be 100%, one step past the last printed cell, 95).
    private const int AutoSuccessActive = 20;
    private const int AutoSuccessPassive = 10;

    // active=10, passive=20: the mirror image, a 10-point passive advantage -- the smallest
    // difference that enters the automatic-failure zone.
    private const int AutoFailureActive = 10;
    private const int AutoFailurePassive = 20;

    [Theory]
    [InlineData(1)]
    [InlineData(50)]
    [InlineData(95)]
    [InlineData(96)]
    [InlineData(99)]
    public void Automatic_success_zone_succeeds_on_every_roll_except_00(int roll)
    {
        var outcome = ResistanceResolver.Resolve(AutoSuccessActive, AutoSuccessPassive, roll);

        Assert.True(outcome.IsAutomaticSuccess);
        Assert.False(outcome.IsAutomaticFailure);
        Assert.True(outcome.Succeeded);
    }

    [Fact]
    public void Automatic_success_zone_still_fails_on_a_roll_of_00()
    {
        // Ch 5, "Failure", p.127: "The exception are resistance rolls, where a difference of 10
        // characteristic points is enough to make only a roll of 00 a failure." A printed roll
        // of 00 is represented as 100 -- see Resolution.RollOutcome.
        var outcome = ResistanceResolver.Resolve(AutoSuccessActive, AutoSuccessPassive, roll: 100);

        Assert.True(outcome.IsAutomaticSuccess);
        Assert.False(outcome.Succeeded);
    }

    /// <summary>
    /// The concrete divergence from the skill kernel that motivated keeping resistance rolls off
    /// <c>SkillResolver</c>: a 100%-equivalent skill roll of 96 fails
    /// (<c>ResolutionPolicyTests.Skill_rolls_fail_at_96_even_at_full_chance_...</c>), but the
    /// same roll against a resistance automatic-success zone succeeds.
    /// </summary>
    [Fact]
    public void Diverges_from_SkillResolver_at_a_roll_of_96_in_the_automatic_success_zone()
    {
        var outcome = ResistanceResolver.Resolve(AutoSuccessActive, AutoSuccessPassive, roll: 96);

        Assert.True(outcome.Succeeded);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(50)]
    [InlineData(95)]
    [InlineData(100)]
    public void Automatic_failure_zone_fails_unconditionally(int roll)
    {
        // No default-rule exception is stated for this side (the book only offers a
        // gamemaster-optional "may allow a roll of 01... to succeed", p.129, which is not
        // implemented as the standing rule -- see ResistanceResolver remarks).
        var outcome = ResistanceResolver.Resolve(AutoFailureActive, AutoFailurePassive, roll);

        Assert.True(outcome.IsAutomaticFailure);
        Assert.False(outcome.IsAutomaticSuccess);
        Assert.False(outcome.Succeeded);
    }

    [Theory]
    [InlineData(50, 50, true)]  // exactly at chance: succeeds
    [InlineData(51, 50, false)] // one over chance: fails
    public void Ordinary_zone_compares_roll_to_chance_directly(int roll, int expectedChance, bool expectedSucceeded)
    {
        // active=passive=1: parity, chance 50%, well inside the printed (non-automatic) range.
        var outcome = ResistanceResolver.Resolve(active: 1, passive: 1, roll);

        Assert.False(outcome.IsAutomaticSuccess);
        Assert.False(outcome.IsAutomaticFailure);
        Assert.Equal(expectedChance, outcome.Chance.Value);
        Assert.Equal(expectedSucceeded, outcome.Succeeded);
    }

    [Fact]
    public void Standard_policy_holds_the_printed_constants()
    {
        var policy = ResistancePolicy.Standard;

        Assert.Equal(50, policy.ParityChance);
        Assert.Equal(5, policy.PercentPerPointOfDifference);
        Assert.Equal(5, policy.AutomaticFailureBelow);
        Assert.Equal(95, policy.AutomaticSuccessAbove);
        Assert.Equal(100, policy.MaximumRoll);
    }
}
