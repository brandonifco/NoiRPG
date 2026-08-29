using Brp.Core.Primitives;
using Brp.Core.Resolution;

namespace Brp.Core.Tests.Resolution;

/// <summary>
/// Conformance fixture for the Skill Results Table, Ch 5: System (BRP ORC Content Document,
/// p.127-128). Transcribed directly from the printed table -- all 24 rows, including the four
/// rows above 100% -- so a transcription error surfaces as a single named failing row rather
/// than hiding inside a loop.
/// <para>
/// Printed table (Base Chance | Critical | Special | Fumble):
/// <code>
/// 01-05   | 01   | 01    | 96-00
/// 06-10   | 01   | 01-02 | 96-00
/// 11-15   | 01   | 01-03 | 96-00
/// 16-20   | 01   | 01-04 | 96-00
/// 21-25   | 01-02| 01-05 | 97-00
/// 26-30   | 01-02| 01-06 | 97-00
/// 31-35   | 01-02| 01-07 | 97-00
/// 36-40   | 01-02| 01-08 | 97-00
/// 41-45   | 01-03| 01-09 | 98-00
/// 46-50   | 01-03| 01-10 | 98-00
/// 51-55   | 01-03| 01-11 | 98-00
/// 56-60   | 01-03| 01-12 | 98-00
/// 61-65   | 01-04| 01-13 | 99-00
/// 66-70   | 01-04| 01-14 | 99-00
/// 71-75   | 01-04| 01-15 | 99-00
/// 76-80   | 01-04| 01-16 | 99-00
/// 81-85   | 01-05| 01-17 | 00
/// 86-90   | 01-05| 01-18 | 00
/// 91-95   | 01-05| 01-19 | 00
/// 96-00   | 01-05| 01-20 | 00
/// 101-105 | 01-06| 01-21 | 00
/// 106-110 | 01-06| 01-22 | 00
/// 111-115 | 01-06| 01-23 | 00
/// 116-120 | 01-06| 01-24 | 00
/// Each +5 | Etc. | Etc.  | 00
/// </code>
/// (A base chance of "96-00" reads as 96 through 100; "00" alone as a fumble range reads as
/// 100 only.)
/// </para>
/// </summary>
public class SkillResultsTableTests
{
    /// <summary>
    /// One entry per printed row: the row's base-chance range, and the printed critical /
    /// special / fumble-start values (fumble always runs to 100 inclusive).
    /// </summary>
    public static TheoryData<string, int, int, int, int, int> PrintedRows => new()
    {
        { "01-05", 1, 5, 1, 1, 96 },
        { "06-10", 6, 10, 1, 2, 96 },
        { "11-15", 11, 15, 1, 3, 96 },
        { "16-20", 16, 20, 1, 4, 96 },
        { "21-25", 21, 25, 2, 5, 97 },
        { "26-30", 26, 30, 2, 6, 97 },
        { "31-35", 31, 35, 2, 7, 97 },
        { "36-40", 36, 40, 2, 8, 97 },
        { "41-45", 41, 45, 3, 9, 98 },
        { "46-50", 46, 50, 3, 10, 98 },
        { "51-55", 51, 55, 3, 11, 98 },
        { "56-60", 56, 60, 3, 12, 98 },
        { "61-65", 61, 65, 4, 13, 99 },
        { "66-70", 66, 70, 4, 14, 99 },
        { "71-75", 71, 75, 4, 15, 99 },
        { "76-80", 76, 80, 4, 16, 99 },
        { "81-85", 81, 85, 5, 17, 100 },
        { "86-90", 86, 90, 5, 18, 100 },
        { "91-95", 91, 95, 5, 19, 100 },
        { "96-00", 96, 100, 5, 20, 100 },
        { "101-105", 101, 105, 6, 21, 100 },
        { "106-110", 106, 110, 6, 22, 100 },
        { "111-115", 111, 115, 6, 23, 100 },
        { "116-120", 116, 120, 6, 24, 100 },
    };

    [Fact]
    public void Printed_rows_cover_all_24_lines_of_the_table()
    {
        Assert.Equal(24, PrintedRows.Count);
    }

    [Theory]
    [MemberData(nameof(PrintedRows))]
    public void Row_thresholds_match_the_printed_table_at_the_low_end_of_its_range(
        string row, int chanceLow, int chanceHigh, int expectedCritical, int expectedSpecial, int expectedFumbleStart)
    {
        _ = row;
        _ = chanceHigh;
        AssertThresholds(chanceLow, expectedCritical, expectedSpecial, expectedFumbleStart);
    }

    [Theory]
    [MemberData(nameof(PrintedRows))]
    public void Row_thresholds_match_the_printed_table_at_the_high_end_of_its_range(
        string row, int chanceLow, int chanceHigh, int expectedCritical, int expectedSpecial, int expectedFumbleStart)
    {
        _ = row;
        _ = chanceLow;
        AssertThresholds(chanceHigh, expectedCritical, expectedSpecial, expectedFumbleStart);
    }

    private static void AssertThresholds(
        int chance, int expectedCritical, int expectedSpecial, int expectedFumbleStart)
    {
        // The roll passed in only decides Level, not the thresholds under test, so any
        // in-range roll works here.
        var outcome = SkillResolver.Resolve(Percent.Of(chance), Percent.Of(chance), roll: 1);

        Assert.Equal(expectedCritical, outcome.CriticalThreshold.Value);
        Assert.Equal(expectedSpecial, outcome.SpecialThreshold.Value);
        Assert.Equal(expectedFumbleStart, outcome.FumbleThreshold.Value); // fumble always runs to 100 (00) from here.
    }

    [Fact]
    public void Closed_form_thresholds_agree_with_every_printed_row_at_every_integer_chance_from_1_to_120()
    {
        // Strengthens the 24-row spot check above into full coverage: every one of the 120
        // integer effective-chance values from the printed portion of the table (not just the
        // 24 row boundaries) must resolve to its row's printed thresholds.
        var verified = 0;

        foreach (var row in PrintedRows)
        {
            var chanceLow = (int)row[1]!;
            var chanceHigh = (int)row[2]!;
            var expectedCritical = (int)row[3]!;
            var expectedSpecial = (int)row[4]!;
            var expectedFumbleStart = (int)row[5]!;

            for (var chance = chanceLow; chance <= chanceHigh; chance++)
            {
                AssertThresholds(chance, expectedCritical, expectedSpecial, expectedFumbleStart);
                verified++;
            }
        }

        Assert.Equal(120, verified);
    }

    [Theory]
    [InlineData(121, 500)]
    [InlineData(500, 2000)]
    public void Closed_form_thresholds_continue_the_printed_pattern_above_120(int from, int to)
    {
        // The table's final row reads "Each +5 | Etc. | Etc. | 00": critical and special keep
        // climbing by the same 1/20 and 1/5 division past the last printed row (116-120), and
        // fumble stays pinned at 100 (00) forever, per Ch 5, "Failure" / "Fumble" -- 96 and
        // above always fails, and 00 always fumbles, regardless of how high the rating goes.
        for (var chance = from; chance <= to; chance += 7) // odd step: not a multiple of 5 or 20
        {
            var outcome = SkillResolver.Resolve(Percent.Of(chance), Percent.Of(chance), roll: 1);

            var expectedCritical = Rounding.Divide(chance, 20, RoundingMode.Up);
            var expectedSpecial = Rounding.Divide(chance, 5, RoundingMode.Up);

            Assert.Equal(expectedCritical, outcome.CriticalThreshold.Value);
            Assert.Equal(expectedSpecial, outcome.SpecialThreshold.Value);
            Assert.Equal(100, outcome.FumbleThreshold.Value);
        }
    }
}
