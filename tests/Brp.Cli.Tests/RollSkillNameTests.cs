namespace Brp.Cli.Tests;

/// <summary>
/// The <c>--skill-name</c> option (#38): the printed base chance is looked up from the shipped
/// ruleset so the 5% floor cannot be mis-applied to a 01%-base skill just because the caller
/// forgot <c>--base-chance</c>. The engine binding is correct as of #27; this closes the one
/// player-facing gap that remained, at the CLI's input.
/// </summary>
public class RollSkillNameTests
{
    /// <summary>
    /// The reason the option exists, pinned as exact output. Science (Forensics) is printed at
    /// 01% (Ch 3: Skills, p.49). Rolled Difficult with a -18 penalty the effective chance is 2%,
    /// and because the printed base is below 5% the floor must NOT rescue 01-05: the bands show
    /// 03 onward as Failure and there is no floor note. The base-chance line records where the
    /// number came from. The companion test below shows the same line, base-defaulted to the
    /// rating, wrongly rescuing -- which is exactly what naming the skill prevents.
    /// </summary>
    [Fact]
    public void A_named_sub_5_percent_skill_is_not_rescued_on_01_to_05()
    {
        var result = CliHarness.Run(
            "roll", "--skill", "40", "--skill-name", "Science (Forensics)",
            "--difficulty", "difficult", "--modifier", "-18 no-lab", "--seed", "3");

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(string.Empty, result.Error);
        Assert.Equal(
            """
            brp roll  (seed 3)

            Chance
              base rating                                        40%
              difficult ÷2                                       20%
              no-lab -18% [situational]                           2%
              effective chance                                    2%

            Outcome bands  (effective 2%, base chance 1% from "Science (Forensics)")
              01      Critical success
              02      Success
              03-95   Failure
              96-100  Fumble

            Roll  09  →  Failure

            """,
            result.Output);
    }

    /// <summary>
    /// The same command without <c>--skill-name</c> defaults the base to the rating (40%), and the
    /// floor then wrongly rescues 03-05 -- the bug #38 lets a caller avoid by naming the skill.
    /// Pinned so the contrast is a test, not a comment.
    /// </summary>
    [Fact]
    public void Without_a_skill_name_the_base_defaults_to_the_rating_and_the_floor_rescues()
    {
        var result = CliHarness.Run(
            "roll", "--skill", "40",
            "--difficulty", "difficult", "--modifier", "-18 no-lab", "--seed", "3");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("base chance 40%)", result.Output);
        Assert.Contains("02-05   Success", result.Output);
        Assert.Contains("note: 03-05 succeed on the base-chance floor (5% or more)", result.Output);
    }

    [Fact]
    public void A_named_constant_skill_resolves_its_base_and_records_the_source()
    {
        // Spot is printed at 25% (Ch 3, p.50). No --base-chance needed.
        var result = CliHarness.Run("roll", "--skill", "60", "--skill-name", "Spot", "--seed", "1");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("base chance 25% from \"Spot\"", result.Output);
    }

    [Fact]
    public void An_unknown_skill_name_is_a_usage_error()
    {
        var result = CliHarness.Run("roll", "--skill", "50", "--skill-name", "Nonesuch", "--seed", "1");

        Assert.Equal(2, result.ExitCode);
        Assert.Contains("unknown skill 'Nonesuch'", result.Error);
    }

    [Fact]
    public void A_formula_based_skill_named_without_a_base_chance_is_refused_not_guessed()
    {
        // Dodge is DEX×2 (Ch 3, p.37): the CLI has no characteristics, so it must refuse rather
        // than invent a base chance.
        var result = CliHarness.Run("roll", "--skill", "50", "--skill-name", "Dodge", "--seed", "1");

        Assert.Equal(2, result.ExitCode);
        Assert.Contains("cannot compute on its own", result.Error);
    }

    [Fact]
    public void A_weapon_derived_skill_named_without_a_base_chance_is_refused()
    {
        var result = CliHarness.Run("roll", "--skill", "50", "--skill-name", "Firearms (Handgun)", "--seed", "1");

        Assert.Equal(2, result.ExitCode);
        Assert.Contains("cannot compute on its own", result.Error);
    }

    [Fact]
    public void Giving_both_a_skill_name_and_a_base_chance_is_rejected()
    {
        var result = CliHarness.Run(
            "roll", "--skill", "50", "--skill-name", "Spot", "--base-chance", "25", "--seed", "1");

        Assert.Equal(2, result.ExitCode);
        Assert.Contains("not both", result.Error);
    }
}
