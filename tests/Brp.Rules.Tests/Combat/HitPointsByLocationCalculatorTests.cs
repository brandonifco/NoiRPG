using Brp.Data;
using Brp.Rules.Combat;

namespace Brp.Rules.Tests.Combat;

/// <summary>
/// Reproduces Ch 6: Combat, "Hit Points by Hit Location (Option)" (p.14), both the formula ("Leg,
/// Abdomen, Head: 1/3 total hit points. Chest: 4/10 total hit points. Arm: 1/4 total hit points",
/// rounded up) and its own printed lookup table for totals 1-21, cell by cell -- 45 cells (15 totals
/// times 3 distinct formulas: Leg/Abdomen/Head share one, Chest and Arm each their own) -- plus the
/// one cell where the two disagree. See <see cref="HitPointsByLocationCalculator"/>'s remarks and
/// <c>docs/decisions/0024-hit-locations.md</c>.
/// </summary>
public class HitPointsByLocationCalculatorTests
{
    private static readonly HitLocationRuleset Ruleset = NoirHitLocationRuleset.Load();

    // (totalHitPoints, legAbdomenHead, chest, arm) -- one row per printed column, Ch 6, p.14. The
    // printed columns group totals sharing identical values (e.g. "1-2", "11-12", "13-15"); each
    // such group is expanded to one row per total so every printed cell gets its own case.
    public static TheoryData<int, int, int, int> PrintedTable => new()
    {
        { 1, 1, 1, 1 },
        { 2, 1, 1, 1 },
        { 3, 1, 2, 1 },
        { 4, 2, 2, 1 },
        { 5, 2, 2, 2 },
        { 6, 2, 3, 2 },
        { 7, 3, 3, 2 },
        { 8, 3, 4, 2 },
        { 9, 3, 4, 3 },
        { 10, 4, 4, 3 },
        { 11, 4, 5, 3 },
        { 12, 4, 5, 3 },
        { 13, 5, 6, 4 },
        { 14, 5, 6, 4 },
        { 15, 5, 6, 4 },
        { 16, 6, 7, 4 },

        // 17: the one printed cell that disagrees with the formula (Arm: printed 4, formula 5) --
        // see Formula_reproduces_every_printed_cell_except_the_one_documented_anomaly below.
        { 18, 6, 8, 5 },
        { 19, 7, 8, 5 },
        { 20, 7, 8, 5 },
        { 21, 7, 9, 6 },
    };

    private static readonly int KnownAnomalyTotalHitPoints = 17;

    [Theory]
    [MemberData(nameof(PrintedTable))]
    public void Formula_reproduces_every_printed_cell_except_the_one_documented_anomaly(
        int totalHitPoints, int legAbdomenHead, int chest, int arm)
    {
        var result = HitPointsByLocationCalculator.Compute(totalHitPoints, Ruleset);

        Assert.Equal(legAbdomenHead, result.RightLeg);
        Assert.Equal(legAbdomenHead, result.LeftLeg);
        Assert.Equal(legAbdomenHead, result.Abdomen);
        Assert.Equal(legAbdomenHead, result.Head);
        Assert.Equal(chest, result.Chest);
        Assert.Equal(arm, result.RightArm);
        Assert.Equal(arm, result.LeftArm);
    }

    [Fact]
    public void Printed_table_disagrees_with_its_own_formula_at_exactly_one_cell()
    {
        // The printed "16-17" Arm column gives a single value, 4, for both totals -- correct for
        // 16 (ceiling(16/4) = 4) but not 17 (ceiling(17/4) = 5). Pinned per the ResistanceTableTests
        // precedent: both the printed value and the engine's differing value are asserted.
        const int printedArmValueAt17 = 4;

        var result = HitPointsByLocationCalculator.Compute(KnownAnomalyTotalHitPoints, Ruleset);

        Assert.Equal(6, result.RightLeg); // Leg/Abdomen/Head at 17 matches the printed table.
        Assert.Equal(7, result.Chest); // Chest at 17 matches the printed table.
        Assert.Equal(5, result.RightArm); // the formula's value.
        Assert.NotEqual(printedArmValueAt17, result.RightArm);
    }

    [Fact]
    public void A_total_of_zero_or_less_is_rejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => HitPointsByLocationCalculator.Compute(0, Ruleset));
        Assert.Throws<ArgumentOutOfRangeException>(() => HitPointsByLocationCalculator.Compute(-1, Ruleset));
    }
}
