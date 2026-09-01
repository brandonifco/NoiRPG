using Brp.Rules.Combat;

namespace Brp.Data.Tests;

/// <summary>
/// Confirms the shipped hit-location data loads and reproduces every row of Ch 6: Combat, "Hit
/// Locations" (p.145) -- row by row, per AGENTS.md's table-backed-rule convention -- and pins the
/// printed misprint the shipped table corrects. See <c>docs/decisions/0024-hit-locations.md</c>.
/// </summary>
public class NoirHitLocationRulesetTests
{
    private static readonly HitLocationRuleset Ruleset = NoirHitLocationRuleset.Load();

    // (minimum, maximum, location, description) -- one case per printed row, Ch 6, p.145.
    public static TheoryData<int, int, HitLocation, string> PrintedRows => new()
    {
        { 1, 4, HitLocation.RightLeg, "Right leg from hip to bottom of foot" },
        { 5, 8, HitLocation.LeftLeg, "Left leg from hip to bottom of foot" },
        { 9, 11, HitLocation.Abdomen, "Hip joint to bottom rib cage" },
        { 12, 12, HitLocation.Chest, "Ribcage up to neck and shoulders" },
        { 13, 15, HitLocation.RightArm, "Entire right arm" },
        { 16, 18, HitLocation.LeftArm, "Entire left arm" },
        { 19, 20, HitLocation.Head, "Neck and Head" },
    };

    [Theory]
    [MemberData(nameof(PrintedRows))]
    public void Every_printed_table_row_matches_the_book(int minimum, int maximum, HitLocation location, string description)
    {
        var row = Ruleset.Table.ForRoll(minimum);
        Assert.Same(row, Ruleset.Table.ForRoll(maximum));

        Assert.Equal(minimum, row.Minimum);
        Assert.Equal(maximum, row.Maximum);
        Assert.Equal(location, row.Location);
        Assert.Equal(description, row.Description);
    }

    [Fact]
    public void The_shipped_table_has_exactly_the_seven_printed_rows()
    {
        Assert.Equal(7, Ruleset.Table.Rows.Count);
    }

    [Fact]
    public void The_table_covers_every_d20_result_exactly_once()
    {
        for (var roll = 1; roll <= 20; roll++)
        {
            Assert.Single(Ruleset.Table.Rows, row => row.Contains(roll));
        }
    }

    [Fact]
    public void The_shipped_table_corrects_the_printed_8_11_abdomen_misprint_to_9_11()
    {
        // Ch 6, p.145 prints "5-8 Left Leg" immediately followed by "8-11 Abdomen" -- the two
        // ranges share the value 8, which is impossible for a table that must partition 1-20
        // exactly once each (verified via the PDF's glyph bounding boxes, not a whitespace
        // artifact). Corrected to 9-11 -- not because that is the only range summing to 20 (5-7
        // Left Leg / 8-11 Abdomen sums to 20 just as well), but because the printed Left Leg row,
        // 5-8, is itself clean and unambiguous; the two legs are otherwise printed symmetrically
        // (Right Leg 1-4, Left Leg 5-8, four faces each); and the canonical BRP humanoid
        // hit-location table this one descends from gives Abdomen as 09-11. See
        // docs/decisions/0024-hit-locations.md for the full argument.
        var leftLeg = Ruleset.Table.ForRoll(8);
        Assert.Equal(HitLocation.LeftLeg, leftLeg.Location);
        Assert.Equal(5, leftLeg.Minimum);
        Assert.Equal(8, leftLeg.Maximum);

        var abdomen = Ruleset.Table.ForRoll(9);
        Assert.Equal(HitLocation.Abdomen, abdomen.Location);
        Assert.Equal(9, abdomen.Minimum); // not the printed 8 -- see class remarks.
        Assert.Equal(11, abdomen.Maximum);
    }

    [Fact]
    public void The_per_location_hit_point_fractions_match_the_book()
    {
        // Ch 2: Characters, Hit Points by Hit Location (p.14): Leg/Abdomen/Head 1/3, Chest 4/10, Arm 1/4.
        Assert.Equal(3, Ruleset.LimbHeadAbdomenDivisor);
        Assert.Equal(4, Ruleset.ChestNumerator);
        Assert.Equal(10, Ruleset.ChestDenominator);
        Assert.Equal(4, Ruleset.ArmDivisor);
    }

    [Fact]
    public void The_limb_damage_cap_multiplier_matches_the_book()
    {
        // Ch 6, p.157: "cannot take more than twice the possible points of damage in an arm or leg."
        Assert.Equal(2, Ruleset.LimbDamageCapMultiplier);
    }
}
