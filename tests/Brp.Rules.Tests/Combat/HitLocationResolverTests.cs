using Brp.Data;
using Brp.Rules.Combat;

namespace Brp.Rules.Tests.Combat;

/// <summary>
/// Confirms <see cref="HitLocationResolver"/> rolls a D20 through the entropy seam (AGENTS.md
/// invariant 5) and maps every face to the correct location, per Ch 6: Combat, "Hit Locations"
/// (p.145).
/// </summary>
public class HitLocationResolverTests
{
    private static readonly HitLocationRuleset Ruleset = NoirHitLocationRuleset.Load();

    public static TheoryData<int, HitLocation> EveryRoll => new()
    {
        { 1, HitLocation.RightLeg },
        { 4, HitLocation.RightLeg },
        { 5, HitLocation.LeftLeg },
        { 8, HitLocation.LeftLeg },
        { 9, HitLocation.Abdomen },
        { 11, HitLocation.Abdomen },
        { 12, HitLocation.Chest },
        { 13, HitLocation.RightArm },
        { 15, HitLocation.RightArm },
        { 16, HitLocation.LeftArm },
        { 18, HitLocation.LeftArm },
        { 19, HitLocation.Head },
        { 20, HitLocation.Head },
    };

    [Theory]
    [MemberData(nameof(EveryRoll))]
    public void Rolling_each_face_maps_to_the_correct_location(int face, HitLocation expected)
    {
        var entropy = new FixedEntropySource(face);

        var roll = HitLocationResolver.RollLocation(Ruleset, entropy);

        Assert.Equal(face, roll.Roll);
        Assert.Equal(expected, roll.Location);
        Assert.Equal(1, entropy.DrawCount);
    }

    [Fact]
    public void Rolling_consumes_exactly_one_d20_draw()
    {
        var entropy = new FixedEntropySource(10);

        HitLocationResolver.RollLocation(Ruleset, entropy);

        Assert.Equal(1, entropy.DrawCount);
    }
}
