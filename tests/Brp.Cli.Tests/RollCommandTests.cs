using System.Globalization;
using System.Text.RegularExpressions;

namespace Brp.Cli.Tests;

/// <summary>
/// Behaviour of the <c>roll</c> command: the acceptance invocation from
/// <c>engine-implementation-plan.md</c> §5, reproducibility, and what the tool does with a
/// command line it cannot understand.
/// </summary>
public class RollCommandTests
{
    private static readonly string[] AcceptanceInvocation =
    [
        "roll", "--skill", "65", "--difficulty", "difficult",
        "--modifier", "-20 firing-into-combat", "--seed", "42",
    ];

    /// <summary>
    /// Acceptance criterion 5 of Milestone 1, verbatim: a rating, a named flat penalty, a
    /// difficulty grade and a seed, printing the full modifier chain and the graded outcome.
    /// Pinned as an exact string because the output <em>is</em> the deliverable here -- a
    /// gamemaster reads it, and it gets pasted into Issues and diffed. A change to any line of
    /// it is a change to the contract, and should have to be made deliberately.
    /// </summary>
    [Fact]
    public void Acceptance_invocation_prints_the_whole_chain_and_the_graded_outcome()
    {
        var result = CliHarness.Run(AcceptanceInvocation);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(string.Empty, result.Error);
        Assert.Equal(
            """
            brp roll  (seed 42)

            Chance
              base rating                                        65%
              difficult ÷2                                       33%
              firing-into-combat -20% [situational]              13%
              effective chance                                   13%

            Outcome bands  (effective 13%, base chance 65%)
              01      Critical success
              02-03   Special success
              04-13   Success
              14-95   Failure
              96-100  Fumble

            Roll  43  →  Failure

            """,
            result.Output);
    }

    [Fact]
    public void Same_seed_produces_identical_output_across_runs()
    {
        var first = CliHarness.Run(AcceptanceInvocation);
        var second = CliHarness.Run(AcceptanceInvocation);

        Assert.Equal(first.Output, second.Output);
        Assert.Equal(first.ExitCode, second.ExitCode);
    }

    /// <summary>
    /// The companion to the reproducibility test: identical output across runs would also be
    /// satisfied by a tool that ignored the seed entirely and always rolled the same number.
    /// </summary>
    [Fact]
    public void Different_seeds_produce_different_rolls()
    {
        var rolls = Enumerable.Range(1, 20)
            .Select(seed => RollOf(CliHarness.Run("roll", "--skill", "65", "--seed", seed.ToString(CultureInfo.InvariantCulture))))
            .Distinct()
            .ToList();

        Assert.True(rolls.Count > 1, $"20 seeds produced only these rolls: {string.Join(", ", rolls)}");
    }

    [Fact]
    public void Inline_and_separated_option_forms_are_equivalent()
    {
        var separated = CliHarness.Run(
            "roll", "--skill", "65", "--difficulty", "difficult", "--modifier", "-20 firing-into-combat",
            "--seed", "42");
        var inline = CliHarness.Run(
            "roll", "--skill=65", "--difficulty=difficult", "--modifier=-20 firing-into-combat", "--seed=42");

        Assert.Equal(separated.Output, inline.Output);
    }

    /// <summary>
    /// ADR 0007: a permanent modifier is figured into the rating before a Difficult grade halves
    /// it; a situational one is applied after, so its stated weight is not itself halved. The
    /// command has to make that visible, because a chain that hid it would hide the single most
    /// error-prone step in the pipeline. 65 +10 = 75, halved = 38, −20 = 18 -- not 65 halved to
    /// 33 with a net −10 applied, which would read 23.
    /// </summary>
    [Fact]
    public void Permanent_modifiers_land_before_the_difficulty_grade_and_situational_ones_after()
    {
        var result = CliHarness.Run(
            "roll", "--skill", "65",
            "--permanent-modifier", "+10 specialist-training",
            "--difficulty", "difficult",
            "--modifier", "-20 firing-into-combat",
            "--seed", "42");

        var chance = ChanceLines(result.Output);
        Assert.Equal(
            [
                "base rating|65%",
                "specialist-training +10% [permanent]|75%",
                "difficult ÷2|38%",
                "firing-into-combat -20% [situational]|18%",
                "effective chance|18%",
            ],
            chance);
    }

    /// <summary>
    /// A failed or fumbled roll is a result, not a tool error, so every grade exits zero.
    /// A script wrapping this command has to be able to tell "the dice went badly" from
    /// "you typed it wrong", and the exit code is the only channel that carries that.
    /// </summary>
    [Theory]
    [InlineData("32", "Critical success")]
    [InlineData("3", "Special success")]
    [InlineData("0", "Success")]
    [InlineData("2", "Failure")]
    [InlineData("8", "Fumble")]
    public void Every_grade_exits_zero_because_a_bad_roll_is_a_result_not_an_error(string seed, string grade)
    {
        var result = CliHarness.Run("roll", "--skill", "65", "--seed", seed);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(string.Empty, result.Error);
        Assert.EndsWith($"→  {grade}\n", result.Output, StringComparison.Ordinal);
    }

    [Theory]
    // Missing required options -- a seed especially, because inventing one would make this the
    // one place in the engine where a result cannot be reproduced from what was typed.
    [InlineData(new[] { "roll", "--seed", "42" }, "--skill")]
    [InlineData(new[] { "roll", "--skill", "65" }, "--seed")]
    // Values that are not what the option takes.
    [InlineData(new[] { "roll", "--skill", "sixty-five", "--seed", "42" }, "--skill")]
    [InlineData(new[] { "roll", "--skill", "-5", "--seed", "42" }, "--skill")]
    [InlineData(new[] { "roll", "--skill", "40", "--base-chance", "-1", "--seed", "42" }, "--base-chance")]
    [InlineData(new[] { "roll", "--skill", "40", "--base-chance", "one", "--seed", "42" }, "--base-chance")]
    [InlineData(new[] { "roll", "--skill", "65", "--seed", "-1" }, "--seed")]
    [InlineData(new[] { "roll", "--skill", "65", "--seed", "42", "--difficulty", "tricky" }, "--difficulty")]
    // A modifier with no source label defeats the purpose of the command.
    [InlineData(new[] { "roll", "--skill", "65", "--seed", "42", "--modifier", "-20" }, "source label")]
    [InlineData(new[] { "roll", "--skill", "65", "--seed", "42", "--modifier", "dark" }, "percentage adjustment")]
    // Repeats are rejected rather than silently taking the last one.
    [InlineData(new[] { "roll", "--skill", "65", "--skill", "40", "--seed", "42" }, "more than once")]
    [InlineData(new[] { "roll", "--skill", "65", "--seed", "42", "--seed", "7" }, "more than once")]
    [InlineData(new[] { "roll", "--skill", "65", "--base-chance", "5", "--base-chance", "1", "--seed", "42" }, "more than once")]
    // Shapes the parser does not recognise.
    [InlineData(new[] { "roll", "--skill", "65", "--seed", "42", "--bonus", "10" }, "unknown option")]
    // A misspelled option reports itself rather than swallowing the next argument as its value.
    [InlineData(new[] { "roll", "--skill", "65", "--seed", "42", "--bonus" }, "unknown option '--bonus'")]
    [InlineData(new[] { "roll", "65", "--seed", "42" }, "unexpected argument")]
    [InlineData(new[] { "roll", "--skill" }, "needs a value")]
    public void A_command_line_that_cannot_be_understood_exits_two_and_says_why(string[] args, string expected)
    {
        var result = CliHarness.Run(args);

        Assert.Equal(2, result.ExitCode);
        Assert.Contains(expected, result.Error, StringComparison.Ordinal);
        // Nothing is rolled, so nothing goes to stdout: a caller piping this into a file gets
        // an empty file rather than half a report.
        Assert.Equal(string.Empty, result.Output);
    }

    [Theory]
    [InlineData("--help")]
    [InlineData("-h")]
    [InlineData("roll --help")]
    public void Help_goes_to_stdout_and_exits_zero(string commandLine)
    {
        var result = CliHarness.Run(Words(commandLine));

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(string.Empty, result.Error);
        Assert.Contains("usage:", result.Output, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("")]
    [InlineData("simulate")]
    public void An_unusable_command_exits_two_with_the_usage_on_stderr(string commandLine)
    {
        var result = CliHarness.Run(Words(commandLine));

        Assert.Equal(2, result.ExitCode);
        Assert.Equal(string.Empty, result.Output);
        Assert.Contains("usage:", result.Error, StringComparison.Ordinal);
    }

    private static string[] Words(string commandLine) =>
        commandLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);

    private static int RollOf(CliHarness.CliResult result)
    {
        var match = Regex.Match(result.Output, @"^Roll\s+(\d+)\s", RegexOptions.Multiline);
        Assert.True(match.Success, $"No roll line in:\n{result.Output}");
        return int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
    }

    /// <summary>Every line of the Chance section as <c>description|chance</c>.</summary>
    private static List<string> ChanceLines(string output) =>
        output.Split('\n')
            .SkipWhile(line => line != "Chance")
            .Skip(1)
            .TakeWhile(line => line.Length > 0)
            .Select(line => Regex.Replace(line.Trim(), @"\s{2,}", "|"))
            .ToList();
}
