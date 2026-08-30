using System.Globalization;
using System.Text;
using Brp.Core.Modifiers;
using Brp.Core.Resolution;

namespace Brp.Cli;

/// <summary>
/// Renders a resolved roll as text. A pure function of its inputs -- no console, no clock, no
/// culture -- so the rendering a test asserts on is byte-for-byte the rendering a gamemaster sees.
/// <para>
/// Line endings are written as <c>\n</c> explicitly rather than through
/// <see cref="Environment.NewLine"/>: the output is meant to be pasted into an Issue and
/// diffed, and a report that changes shape with the operating system is not reproducible in
/// the sense the rest of this engine means the word.
/// </para>
/// </summary>
internal static class RollReport
{
    /// <summary>Column the right-aligned percentages end at. Fits an 80-column terminal with room to spare.</summary>
    private const int Width = 56;

    private const string Indent = "  ";

    public static string Render(ulong seed, ModifierChain chain, RollOutcome outcome, string? baseChanceSkillName = null)
    {
        ArgumentNullException.ThrowIfNull(chain);
        ArgumentNullException.ThrowIfNull(outcome);

        var report = new StringBuilder();
        report.Append(CultureInfo.InvariantCulture, $"brp roll  (seed {seed})\n\n");

        // The derivation is read straight off the chain's own contributions rather than
        // recomputed here. Each one already carries the source label and the running chance
        // after that step, which is precisely the provenance this command exists to print --
        // and it means the CLI cannot drift from the pipeline that produced the number.
        report.Append("Chance\n");
        report.Append(Row("base rating", chain.BaseChance.ToString()));
        foreach (var step in chain.Contributions)
        {
            report.Append(Row(step.Description, step.ResultingChance.ToString()));
        }

        report.Append(Row("effective chance", outcome.EffectiveChance.ToString()));
        report.Append('\n');

        // Grades come from asking the kernel to judge all 100 possible rolls, not from
        // re-deriving thresholds out of RollOutcome. What is shown is therefore what the
        // resolver actually applies -- including the 5% floor, which is not a threshold on the
        // outcome at all -- so the display cannot disagree with the conformance fixtures.
        var levels = Grade(outcome);
        var bands = Runs(levels);

        // "base chance", not "base rating": this is the skill's printed starting value, the only
        // thing the floor rule below reads. The rating the chain started from is the Chance
        // section's first row, and the two are not always the same number. When the base chance
        // was looked up from a named skill (--skill-name), the source is shown so the report
        // records where the number came from rather than leaving it a bare figure; when it was
        // given with --base-chance or defaulted to the rating, the line is unchanged.
        var baseChanceProvenance = baseChanceSkillName is null
            ? string.Empty
            : $" from \"{baseChanceSkillName}\"";
        report.Append(CultureInfo.InvariantCulture,
            $"Outcome bands  (effective {outcome.EffectiveChance}, base chance {outcome.BaseChance}{baseChanceProvenance})\n");
        foreach (var band in bands)
        {
            report.Append(Indent)
                .Append(Range(band.Low, band.High).PadRight(8))
                .Append(Name(band.Level))
                .Append('\n');
        }

        // Ch 5: System, "Skill Rolls" -- a skill whose printed base chance is 5% or higher
        // always succeeds on 01-05 however far modifiers push the effective chance down. Named
        // only for the rolls it actually rescues: at an effective 3% that is 04-05, and saying
        // "01-05" there would overclaim against the band list printed immediately above, where
        // 01 is a critical and 02-03 succeed on the effective chance in the ordinary way.
        var policy = ResolutionPolicy.Standard;
        var rescued = Enumerable.Range(1, policy.MinimumSuccessFloorRoll)
            .Where(roll => roll > outcome.EffectiveChance.Value && levels[roll] == SuccessLevel.Success)
            .ToList();
        if (rescued.Count > 0)
        {
            report.Append(CultureInfo.InvariantCulture,
                $"{Indent}note: {Range(rescued[0], rescued[^1])} succeed on the base-chance floor "
                + $"({policy.MinimumSuccessFloorChance}% or more)\n");
        }

        report.Append('\n');
        report.Append(CultureInfo.InvariantCulture, $"Roll  {Pip(outcome.Roll)}  →  {Name(outcome.Level)}\n");
        return report.ToString();
    }

    /// <summary>One contiguous run of rolls that all grade the same way.</summary>
    private readonly record struct Band(int Low, int High, SuccessLevel Level);

    /// <summary>Every possible roll, graded by the kernel. Indexed by the roll itself, so index 0 is unused.</summary>
    private static SuccessLevel[] Grade(RollOutcome outcome)
    {
        var levels = new SuccessLevel[ResolutionPolicy.MaximumRoll + 1];
        for (var roll = 1; roll <= ResolutionPolicy.MaximumRoll; roll++)
        {
            levels[roll] = SkillResolver.Resolve(outcome.BaseChance, outcome.EffectiveChance, roll).Level;
        }

        return levels;
    }

    /// <summary>Collapses the graded rolls into contiguous runs of the same grade.</summary>
    private static List<Band> Runs(SuccessLevel[] levels)
    {
        var bands = new List<Band>();
        for (var roll = 1; roll < levels.Length; roll++)
        {
            if (bands.Count > 0 && bands[^1].Level == levels[roll])
            {
                bands[^1] = bands[^1] with { High = roll };
            }
            else
            {
                bands.Add(new Band(roll, roll, levels[roll]));
            }
        }

        return bands;
    }

    /// <summary>
    /// A label on the left, a percentage right-aligned at <see cref="Width"/>, and always at
    /// least one space between them however long the label runs.
    /// </summary>
    private static string Row(string label, string value)
    {
        var left = Indent + label;
        var gap = Math.Max(1, Width - left.Length - value.Length);
        return left + new string(' ', gap) + value + "\n";
    }

    /// <summary>
    /// A band of rolls, collapsed to a single value when it covers only one -- the printed Skill
    /// Results Table writes a one-roll critical as <c>01</c>, not <c>01-01</c>, and the same
    /// reading is what keeps a one-roll fumble band from rendering as <c>100-100</c>.
    /// </summary>
    private static string Range(int low, int high) =>
        low == high ? Pip(low) : $"{Pip(low)}-{Pip(high)}";

    /// <summary>
    /// A percentile result as it is written on a die face pair: two digits, except 100, which
    /// is printed in full. The book prints that result as <c>00</c>, but this report also
    /// prints ranges, and <c>96-00</c> reads backwards on a screen.
    /// </summary>
    private static string Pip(int roll) =>
        roll == ResolutionPolicy.MaximumRoll
            ? roll.ToString(CultureInfo.InvariantCulture)
            : roll.ToString("00", CultureInfo.InvariantCulture);

    private static string Name(SuccessLevel level) => level switch
    {
        SuccessLevel.Critical => "Critical success",
        SuccessLevel.Special => "Special success",
        SuccessLevel.Success => "Success",
        SuccessLevel.Failure => "Failure",
        SuccessLevel.Fumble => "Fumble",
        _ => throw new ArgumentOutOfRangeException(nameof(level), level, "Unknown success level."),
    };
}
