using System.Globalization;
using System.Text.RegularExpressions;
using Brp.Core.Primitives;
using Brp.Core.Resolution;

namespace Brp.Cli.Tests;

/// <summary>
/// The outcome bands the report prints, checked against the resolution kernel and against the
/// printed Skill Results Table, plus the layout constraint that the report stays readable in a
/// narrow terminal.
/// </summary>
public class RollBandTests
{
    /// <summary>
    /// Acceptance criterion: the thresholds shown match the kernel's for that rating. Swept
    /// across the whole printed portion of the Skill Results Table rather than spot-checked,
    /// per the rules-conformance rule in <c>AGENTS.md</c> -- the kernel's own conformance
    /// fixture pins these values to the book, and this pins the display to the kernel.
    /// </summary>
    [Fact]
    public void Rendered_bands_match_the_kernels_thresholds_at_every_rating_from_1_to_120()
    {
        for (var rating = 1; rating <= 120; rating++)
        {
            var chance = Percent.Of(rating);
            var kernel = SkillResolver.Resolve(chance, chance, roll: 1);
            var bands = BandsOf(CliHarness.Run("roll", "--skill", rating.ToString(CultureInfo.InvariantCulture), "--seed", "42"));

            Assert.Equal(kernel.CriticalThreshold.Value, Last(bands, "Critical success", rating));
            Assert.Equal(kernel.FumbleThreshold.Value, First(bands, "Fumble", rating));

            // A special band only appears when it is wider than the critical band inside it;
            // at a rating of 1 both thresholds are 1 and the critical result wins outright.
            if (kernel.SpecialThreshold.Value > kernel.CriticalThreshold.Value)
            {
                Assert.Equal(kernel.SpecialThreshold.Value, Last(bands, "Special success", rating));
            }
            else
            {
                Assert.DoesNotContain(bands, b => b.Grade == "Special success");
            }
        }
    }

    /// <summary>
    /// The acceptance invocation resolves against an effective 13%, which is row <c>11-15</c> of
    /// the printed Skill Results Table (Ch 5: System, BRP ORC Content Document, p.127-128):
    /// critical <c>01</c>, special <c>01-03</c>, fumble <c>96-00</c>. Transcribed here from the
    /// book so the demonstration this Issue delivers is checked against the source directly,
    /// not only against the code that produced it.
    /// </summary>
    [Fact]
    public void Bands_for_the_acceptance_rating_match_the_printed_skill_results_table_row()
    {
        var bands = BandsOf(CliHarness.Run(
            "roll", "--skill", "65", "--difficulty", "difficult",
            "--modifier", "-20 firing-into-combat", "--seed", "42"));

        Assert.Equal(
            [
                (1, 1, "Critical success"),
                (2, 3, "Special success"),
                (4, 13, "Success"),
                (14, 95, "Failure"),
                (96, 100, "Fumble"),
            ],
            bands);
    }

    /// <summary>
    /// Ch 5, "Skill Rolls": a skill whose <em>base</em> chance is 5% or higher always succeeds
    /// on 01-05 however far modifiers push the effective chance down. Called out in the report
    /// only when it actually widens the success band past the effective chance -- otherwise the
    /// band would look like an arithmetic error.
    /// </summary>
    [Theory]
    // 65 halved to 33, less 30, is 3 -- below the floor, and the base chance is above it.
    [InlineData(new[] { "roll", "--skill", "65", "--difficulty", "difficult", "--modifier", "-30 pitch-dark", "--seed", "1" }, true)]
    // Nothing pushes the effective chance below 5.
    [InlineData(new[] { "roll", "--skill", "65", "--seed", "1" }, false)]
    // Effective chance is below 5, but so is the base chance, so the floor does not apply.
    [InlineData(new[] { "roll", "--skill", "3", "--seed", "1" }, false)]
    public void The_base_chance_floor_is_named_only_when_it_widens_the_success_band(string[] args, bool expected)
    {
        var result = CliHarness.Run(args);

        Assert.Equal(expected, result.Output.Contains("base-chance floor", StringComparison.Ordinal));
    }

    /// <summary>
    /// The floor is keyed on the skill's <em>printed base chance</em>, not on the character's
    /// rating: Ch 5, "Skill Rolls" says "any skill which normally has a base chance of 5% or
    /// higher... even if difficulty, conditional modifiers, or other factors reduce the skill
    /// rating below 5%". Those are two different numbers, and several in-scope skills are
    /// printed at 01% -- Science, Strategy and Martial Arts (Ch 3: Skills) -- so a trained
    /// character can hold a rating well above the floor in a skill the floor does not cover.
    /// <para>
    /// Worked case: Science (Forensics) at 40%, rolled Difficult with no lab access. 40 halved
    /// is 20, less 18 is an effective 2%. Rolls 03-05 are failures, because Science is printed
    /// at 01%. Binding the rating into the base-chance slot would rescue them.
    /// </para>
    /// </summary>
    [Fact]
    public void The_floor_reads_the_printed_base_chance_not_the_characters_rating()
    {
        var args = new[]
        {
            "roll", "--skill", "40", "--difficulty", "difficult", "--modifier", "-18 no-lab-access", "--seed", "7",
        };

        var trained = CliHarness.Run([.. args, "--base-chance", "1"]);
        Assert.Equal(
            [(1, 1, "Critical success"), (2, 2, "Success"), (3, 95, "Failure"), (96, 100, "Fumble")],
            BandsOf(trained));
        Assert.DoesNotContain("floor", trained.Output, StringComparison.Ordinal);

        // Without the option the printed base chance defaults to the rating, which is the right
        // reading for every skill printed at 5% or above and the wrong one here. The option
        // exists precisely so the difference is expressible; this pins the default.
        var assumed = CliHarness.Run(args);
        Assert.Equal(
            [(1, 1, "Critical success"), (2, 5, "Success"), (6, 95, "Failure"), (96, 100, "Fumble")],
            BandsOf(assumed));
    }

    /// <summary>
    /// The note names only the rolls the floor actually rescues. At an effective 3% the success
    /// band runs 02-05, but 02-03 succeed on the effective chance in the ordinary way and 01 is
    /// a critical; only 04-05 are the floor's doing. Printing "01-05" there would contradict the
    /// band list one line above it.
    /// </summary>
    [Fact]
    public void The_floor_note_names_only_the_rolls_the_floor_rescues()
    {
        var result = CliHarness.Run(
            "roll", "--skill", "65", "--difficulty", "difficult", "--modifier", "-30 pitch-dark", "--seed", "1");

        Assert.Contains("note: 04-05 succeed on the base-chance floor", result.Output, StringComparison.Ordinal);
    }

    /// <summary>
    /// Acceptance criterion: readable in a terminal without a wide window. 72 columns leaves
    /// room for the quoting and indentation a diff or an Issue comment adds around it.
    /// </summary>
    [Theory]
    // Arguments are separated by '|' because some of them contain spaces.
    [InlineData("roll|--skill|65|--difficulty|difficult|--modifier|-20 firing-into-combat|--seed|42")]
    [InlineData("roll|--skill|65|--difficulty|difficult|--modifier|-30 pitch-dark|--seed|1")]
    [InlineData("roll|--skill|120|--difficulty|easy|--seed|9")]
    [InlineData("roll|--skill|0|--seed|9")]
    [InlineData("roll|--skill|65|--seed|42|--modifier|-5 a-source-label-long-enough-to-crowd-the-column")]
    public void The_report_fits_a_terminal_seventy_two_columns_wide(string commandLine)
    {
        var overlong = CliHarness.Run(commandLine.Split('|')).Output
            .Split('\n')
            .Where(line => line.Length > 72)
            .ToList();

        Assert.Empty(overlong);
    }

    private static int Last(IReadOnlyList<(int Low, int High, string Grade)> bands, string grade, int rating)
    {
        var band = bands.LastOrDefault(b => b.Grade == grade);
        Assert.True(band.Grade == grade, $"No '{grade}' band at rating {rating}.");
        return band.High;
    }

    private static int First(IReadOnlyList<(int Low, int High, string Grade)> bands, string grade, int rating)
    {
        var band = bands.FirstOrDefault(b => b.Grade == grade);
        Assert.True(band.Grade == grade, $"No '{grade}' band at rating {rating}.");
        return band.Low;
    }

    /// <summary>
    /// Reads the band table back out of the rendered report, which is the only thing a
    /// gamemaster actually sees -- asserting against the rendering rather than against an
    /// intermediate object is the point.
    /// </summary>
    private static List<(int Low, int High, string Grade)> BandsOf(CliHarness.CliResult result)
    {
        // A band covering a single roll is printed as one value, so the range half is optional.
        var bands = Regex.Matches(result.Output, @"^  (\d{2,3})(?:-(\d{2,3}))?\s+(\S.*)$", RegexOptions.Multiline)
            .Select(m => (
                Low: int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture),
                High: int.Parse(
                    m.Groups[2].Success ? m.Groups[2].Value : m.Groups[1].Value, CultureInfo.InvariantCulture),
                Grade: m.Groups[3].Value))
            .ToList();

        Assert.NotEmpty(bands);
        return bands;
    }
}
