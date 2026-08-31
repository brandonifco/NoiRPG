using Brp.Core.Abilities;
using Brp.Rules.Combat;

namespace Brp.Data.Tests;

/// <summary>
/// Confirms the shipped major wound data loads and reproduces every row of Ch 6: Combat, "Major
/// Wounds Table" (pp.155-156) -- row by row rather than sampled, per AGENTS.md's "table-backed
/// rule" convention -- and carries the Fatal Wounds / collapse values. See
/// <c>docs/decisions/0021-major-wounds.md</c>.
/// </summary>
public class NoirMajorWoundRulesetTests
{
    private static readonly MajorWoundRuleset Ruleset = NoirMajorWoundRuleset.Load();

    // (minimum, maximum, "CHR:dice;CHR:dice" fixed losses, gmChoiceCount, gmChoiceDice,
    //  reducesMovement, requiresLimbSide, ableToFight) -- one case per printed row, Ch 6 pp.155-156.
    public static IEnumerable<object?[]> PrintedRows()
    {
        yield return new object?[] { 1, 10, "DEX:1D3", 0, null, true, false, true };
        yield return new object?[] { 11, 20, "CHA:1D3", 0, null, false, false, true };
        yield return new object?[] { 21, 30, "STR:1D3", 0, null, false, false, true };
        yield return new object?[] { 31, 40, "CON:1D3", 0, null, true, false, true };
        yield return new object?[] { 41, 50, "INT:1D3", 0, null, false, false, true };
        yield return new object?[] { 51, 60, "DEX:1D6", 0, null, true, false, false };
        yield return new object?[] { 61, 70, "CHA:1D6", 0, null, false, false, true };
        yield return new object?[] { 71, 80, "STR:1D6", 0, null, false, false, true };
        yield return new object?[] { 81, 90, "CON:1D6", 0, null, true, false, false };
        yield return new object?[] { 91, 92, "CHA:1D6", 0, null, false, false, true };
        yield return new object?[] { 93, 94, "DEX:1D6", 0, null, false, false, true };
        yield return new object?[] { 95, 96, "DEX:1D6", 0, null, false, true, true };
        yield return new object?[] { 97, 98, "DEX:1D6", 0, null, false, false, false };
        yield return new object?[] { 99, 99, "CHA:1D3;DEX:1D3;CON:1D3", 0, null, false, false, false };
        yield return new object?[] { 100, 100, "", 4, "1D4", false, false, false };
    }

    [Theory]
    [MemberData(nameof(PrintedRows))]
    public void Every_printed_table_row_matches_the_book(
        int minimum,
        int maximum,
        string fixedLosses,
        int gamemasterChoiceCount,
        string? gamemasterChoiceDice,
        bool reducesMovement,
        bool requiresLimbSide,
        bool ableToFight)
    {
        // A row is looked up by its own range so a mis-ordered or mis-ranged transcription surfaces here.
        var row = Ruleset.Table.ForRoll(minimum);
        Assert.Same(row, Ruleset.Table.ForRoll(maximum));

        Assert.Equal(minimum, row.Minimum);
        Assert.Equal(maximum, row.Maximum);
        Assert.Equal(reducesMovement, row.ReducesMovement);
        Assert.Equal(requiresLimbSide, row.RequiresLimbSide);
        Assert.Equal(ableToFight, row.AbleToFight);

        var expectedLosses = fixedLosses.Length == 0
            ? []
            : fixedLosses.Split(';').Select(pair =>
            {
                var parts = pair.Split(':');
                return (Characteristic: parts[0], Dice: parts[1]);
            }).ToArray();

        Assert.Equal(expectedLosses.Length, row.Losses.Count);
        for (var i = 0; i < expectedLosses.Length; i++)
        {
            Assert.Equal(new CharacteristicId(expectedLosses[i].Characteristic), row.Losses[i].Characteristic);
            Assert.Equal(expectedLosses[i].Dice, row.Losses[i].Dice.Notation);
        }

        if (gamemasterChoiceCount == 0)
        {
            Assert.Null(row.GamemasterChoice);
        }
        else
        {
            Assert.NotNull(row.GamemasterChoice);
            Assert.Equal(gamemasterChoiceCount, row.GamemasterChoice!.Count);
            Assert.Equal(gamemasterChoiceDice, row.GamemasterChoice.Dice.Notation);
        }
    }

    [Fact]
    public void The_shipped_table_has_exactly_the_fifteen_printed_rows()
    {
        Assert.Equal(15, Ruleset.Table.Rows.Count);
    }

    [Fact]
    public void The_table_covers_every_1D100_result_exactly_once()
    {
        // No gaps and no overlaps across the whole 1-100 range (a printed 00 read as 100).
        for (var roll = 1; roll <= 100; roll++)
        {
            Assert.Single(Ruleset.Table.Rows, row => row.Contains(roll));
        }
    }

    [Fact]
    public void The_fatal_wound_and_collapse_values_match_the_book()
    {
        // Ch 6, Fatal Wounds (p.156): the wound round or the round immediately after -> window offset 1.
        Assert.Equal(1, Ruleset.FatalWoundRescueWindowRounds);

        // Ch 6, Major Wounds (p.155): collapse "unconscious for an hour".
        Assert.Equal(1, Ruleset.CollapseUnconsciousHours);
    }
}
