using System.Globalization;
using Brp.Core.Contests;

namespace Brp.Core.Tests.Contests;

/// <summary>
/// Conformance fixture for the Resistance Table, Ch 5: System, "The Resistance Table" (BRP ORC
/// Content Document, p.129). Transcribed directly from the printed grid -- all 24 rows and all
/// 24 columns (576 cells) -- via the PDF's word bounding boxes (not a whitespace-sensitive text
/// dump, which misaligns on a grid this wide), so a transcription error surfaces as a single
/// named failing cell rather than hiding inside a loop.
/// <para>
/// The top axis is the active characteristic (1-24), the left axis is the passive characteristic
/// (1-24); each row below is one passive value, left to right across active 1-24. <c>"-"</c>
/// stands for either dash glyph the book prints for an automatic cell (both appear in the
/// source; the distinction is a font artifact, not a rules difference).
/// </para>
/// </summary>
public class ResistanceTableTests
{
    /// <summary>
    /// Row <c>r</c> (0-indexed) is passive characteristic <c>r + 1</c>; each row lists active
    /// characteristics 1 through 24 left to right, exactly as printed on p.129-130.
    /// </summary>
    public static readonly string[] PrintedRows =
    {
        "50 55 60 65 70 75 80 85 90 95 - - - - - - - - - - - - - -",
        "45 50 55 60 65 70 75 80 85 90 95 - - - - - - - - - - - - -",
        "40 45 50 55 60 65 70 75 80 85 90 95 - - - - - - - - - - - -",
        "35 40 45 50 55 60 65 70 75 80 85 90 95 - - - - - - - - - - -",
        "30 35 40 45 50 55 60 65 70 75 80 85 90 95 - - - - - - - - - -",
        "25 30 35 40 45 50 55 60 65 70 75 80 85 90 95 - - - - - - - - -",
        "20 25 30 35 40 45 50 55 60 65 70 75 80 85 90 95 - - - - - - - -",
        "15 20 25 30 35 40 45 50 55 60 65 70 75 80 85 90 95 - - - - - - -",
        "10 15 20 25 30 35 40 45 50 55 60 65 70 75 80 85 90 95 - - - - - -",
        "05 10 15 20 25 30 35 40 45 50 55 60 65 70 75 80 85 90 95 - - - - -",
        "- 05 10 15 20 25 30 35 40 45 50 55 60 65 70 75 80 85 90 95 - - - -",
        "- - 05 10 15 20 25 30 35 40 45 50 55 60 65 70 75 80 85 90 95 - - -",
        "- - - 05 10 15 20 25 30 35 40 45 50 55 60 65 70 75 80 85 90 95 - -",
        "- - - - 05 10 15 20 25 30 35 40 45 50 55 60 65 70 75 80 85 90 95 -",

        // Passive 15: the printed table's one departure from its own formula -- the last cell
        // (active 24) prints 85, where every other diff-of-9 cell in the table (and the formula
        // itself) gives 95. See Printed_table_disagrees_with_its_own_formula_at_exactly_one_cell.
        "- - - - - 05 10 15 20 25 30 35 40 45 50 55 60 65 70 75 80 85 90 85",

        "- - - - - - 05 10 15 20 25 30 35 40 45 50 55 60 65 70 75 80 85 90",
        "- - - - - - - 05 10 15 20 25 30 35 40 45 50 55 60 65 70 75 80 85",
        "- - - - - - - - 05 10 15 20 25 30 35 40 45 50 55 60 65 70 75 80",
        "- - - - - - - - - 05 10 15 20 25 30 35 40 45 50 55 60 65 70 75",
        "- - - - - - - - - - 05 10 15 20 25 30 35 40 45 50 55 60 65 70",
        "- - - - - - - - - - - 05 10 15 20 25 30 35 40 45 50 55 60 65",
        "- - - - - - - - - - - - 05 10 15 20 25 30 35 40 45 50 55 60",
        "- - - - - - - - - - - - - 05 10 15 20 25 30 35 40 45 50 55",
        "- - - - - - - - - - - - - - 05 10 15 20 25 30 35 40 45 50",
    };

    /// <summary>The one cell (passive 15, active 24) where the printed table disagrees with itself.</summary>
    private static readonly (int Active, int Passive) KnownAnomaly = (24, 15);

    [Fact]
    public void Printed_table_has_24_rows_of_24_columns_576_cells_total()
    {
        Assert.Equal(24, PrintedRows.Length);
        Assert.All(PrintedRows, row => Assert.Equal(24, row.Split(' ').Length));
        Assert.Equal(576, PrintedRows.Sum(row => row.Split(' ').Length));
    }

    public static TheoryData<int, int, string> AllPrintedCells()
    {
        var data = new TheoryData<int, int, string>();
        for (var passive = 1; passive <= 24; passive++)
        {
            var cells = PrintedRows[passive - 1].Split(' ');
            for (var active = 1; active <= 24; active++)
            {
                data.Add(active, passive, cells[active - 1]);
            }
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(AllPrintedCells))]
    public void Closed_form_reproduces_every_printed_cell_except_the_one_documented_anomaly(
        int active, int passive, string printed)
    {
        if ((active, passive) == KnownAnomaly)
        {
            return; // covered by its own dedicated test below.
        }

        var outcome = ResistanceResolver.Resolve(active, passive, roll: 1);

        if (printed == "-")
        {
            // Direction matters, not just "some automatic flag is set" -- a resolver that had
            // the two flags swapped would still pass a same-or check on every one of these 552
            // cells. An active advantage (active > passive) must land in the automatic-success
            // zone; a passive advantage must land in the automatic-failure zone.
            if (active > passive)
            {
                Assert.True(outcome.IsAutomaticSuccess);
                Assert.False(outcome.IsAutomaticFailure);
            }
            else
            {
                Assert.True(outcome.IsAutomaticFailure);
                Assert.False(outcome.IsAutomaticSuccess);
            }
        }
        else
        {
            Assert.False(outcome.IsAutomaticSuccess);
            Assert.False(outcome.IsAutomaticFailure);
            Assert.Equal(int.Parse(printed, CultureInfo.InvariantCulture), outcome.Chance.Value);
        }
    }

    /// <summary>
    /// Pins the one cell where the printed table disagrees with its own formula: passive 15,
    /// active 24 (a 9-point active advantage) prints 85, but every other 9-point-advantage cell
    /// in the table -- and the book's own prose ("Every point the active factor exceeds the
    /// passive factor by modifies the chance of success by +5%") -- gives 95. This is almost
    /// certainly a printing error: the formula, 575 of the table's own 576 cells, and the prose
    /// all agree with each other and disagree only with this one cell.
    /// <para>
    /// The engine follows the formula (and the other 575 cells), not the anomalous cell -- an
    /// unmotivated ±10 dip at a single grid position is not a plausible intentional rule, and
    /// implementing the printed 85 would silently create a corresponding anomaly at whatever
    /// row/column shares its active-minus-passive difference (24 - 15 = 9, same as every other
    /// diff-9 cell), which would then disagree with 100% of the diff-9 cells that don't happen to
    /// share this specific coordinate.
    /// </para>
    /// </summary>
    [Fact]
    public void Printed_table_disagrees_with_its_own_formula_at_exactly_one_cell()
    {
        var (active, passive) = KnownAnomaly;
        var printedValue = PrintedRows[passive - 1].Split(' ')[active - 1];
        Assert.Equal("85", printedValue); // pins what the book actually prints here.

        var outcome = ResistanceResolver.Resolve(active, passive, roll: 1);

        Assert.False(outcome.IsAutomaticSuccess);
        Assert.False(outcome.IsAutomaticFailure);
        Assert.Equal(95, outcome.Chance.Value); // the formula's (and every sibling cell's) value.
        Assert.NotEqual(int.Parse(printedValue, CultureInfo.InvariantCulture), outcome.Chance.Value);
    }

    [Theory]
    [InlineData(1, 1, 50)]   // parity: "the active factor has a 50% chance of success" (p.129)
    [InlineData(10, 1, 95)]  // largest printed active advantage before the automatic zone
    [InlineData(1, 10, 5)]   // largest printed passive advantage before the automatic zone
    public void Spot_checked_boundary_cells_match_the_book(int active, int passive, int expectedChance)
    {
        var outcome = ResistanceResolver.Resolve(active, passive, roll: 1);
        Assert.Equal(expectedChance, outcome.Chance.Value);
    }

    /// <summary>
    /// active 10 / passive 1 is a 9-point active advantage: chance 95, the last printed
    /// (non-automatic) cell on that diagonal. This is the cell that actually distinguishes
    /// "a 9-point advantage is an ordinary roll, compared to its chance like any other resistance
    /// roll" from "a 10-point advantage ignores the roll entirely except for 00" -- the automatic
    /// -success tests elsewhere in this suite exercise the 10-point side, but nothing previously
    /// exercised the 9-point side landing on a plain, un-automatic 96 failing for the ordinary
    /// reason (roll &gt; chance), not the skill kernel's unrelated 96+ rule.
    /// </summary>
    [Theory]
    [InlineData(95, true)]
    [InlineData(96, false)]
    public void Ordinary_zone_upper_boundary_is_not_automatic(int roll, bool expectedSucceeded)
    {
        var outcome = ResistanceResolver.Resolve(active: 10, passive: 1, roll);

        Assert.False(outcome.IsAutomaticSuccess);
        Assert.False(outcome.IsAutomaticFailure);
        Assert.Equal(95, outcome.Chance.Value);
        Assert.Equal(expectedSucceeded, outcome.Succeeded);
    }
}
