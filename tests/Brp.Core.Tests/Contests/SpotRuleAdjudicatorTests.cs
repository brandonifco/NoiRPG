using Brp.Core.Contests;

namespace Brp.Core.Tests.Contests;

/// <summary>
/// Covers the named gamemaster-discretion ports for the Ch 7 situational combat spot rules: the
/// canonical decision ids, the documented neutral defaults of <see cref="DefaultSpotRuleAdjudicator"/>,
/// and that a deterministic stub can drive every port for replayable tests. See
/// <c>docs/decisions/0018-spot-rules.md</c>.
/// </summary>
public class SpotRuleAdjudicatorTests
{
    [Theory]
    [InlineData(SpotRuleDecisionId.CoverPenetration, "cover-penetration")]
    [InlineData(SpotRuleDecisionId.CoverExtent, "cover-extent")]
    [InlineData(SpotRuleDecisionId.DarknessSeverity, "darkness-severity")]
    [InlineData(SpotRuleDecisionId.BackstabHelplessReprieve, "backstab-helpless-reprieve")]
    [InlineData(SpotRuleDecisionId.FiringIntoCombatStrayTarget, "firing-into-combat-stray-target")]
    public void Every_decision_id_has_its_canonical_kebab_case_string(SpotRuleDecisionId id, string expected)
    {
        Assert.Equal(expected, SpotRuleDecisionIds.CanonicalId(id));
    }

    [Fact]
    public void The_five_named_discretion_points_are_exactly_those_issue_50_calls_out()
    {
        Assert.Equal(5, Enum.GetValues<SpotRuleDecisionId>().Length);
    }

    [Fact]
    public void The_default_adjudicator_returns_the_documented_minimal_assumption_answers()
    {
        var adjudicator = new DefaultSpotRuleAdjudicator();

        Assert.Equal(DarknessSeverity.SemiDarkness, adjudicator.DecideDarknessSeverity());
        Assert.Equal(CoverPenetrationRuling.StoppedByCover, adjudicator.DecideCoverPenetration());
        Assert.Equal(CoverExtentRuling.PartiallyProtected, adjudicator.DecideCoverExtent());
        Assert.Equal(HelplessReprieveRuling.NoReprieve, adjudicator.DecideBackstabHelplessReprieve());
        Assert.Null(adjudicator.DecideFiringIntoCombatStrayTarget(bystanderCount: 3).StruckBystanderIndex);
    }

    [Fact]
    public void A_deterministic_stub_drives_every_port_with_scripted_answers()
    {
        ISpotRuleAdjudicator adjudicator = new ScriptedSpotRuleAdjudicator(
            darkness: DarknessSeverity.PitchBlack,
            coverPenetration: CoverPenetrationRuling.PenetratesToTarget,
            coverExtent: CoverExtentRuling.FullyProtected,
            reprieve: HelplessReprieveRuling.ReprievedThisRound,
            strayTargetIndex: 1);

        Assert.Equal(DarknessSeverity.PitchBlack, adjudicator.DecideDarknessSeverity());
        Assert.Equal(CoverPenetrationRuling.PenetratesToTarget, adjudicator.DecideCoverPenetration());
        Assert.Equal(CoverExtentRuling.FullyProtected, adjudicator.DecideCoverExtent());
        Assert.Equal(HelplessReprieveRuling.ReprievedThisRound, adjudicator.DecideBackstabHelplessReprieve());
        Assert.Equal(1, adjudicator.DecideFiringIntoCombatStrayTarget(bystanderCount: 3).StruckBystanderIndex);
    }

    /// <summary>A deterministic test double returning pre-scripted rulings for each port.</summary>
    private sealed class ScriptedSpotRuleAdjudicator(
        DarknessSeverity darkness,
        CoverPenetrationRuling coverPenetration,
        CoverExtentRuling coverExtent,
        HelplessReprieveRuling reprieve,
        int? strayTargetIndex) : ISpotRuleAdjudicator
    {
        public DarknessSeverity DecideDarknessSeverity() => darkness;

        public CoverPenetrationRuling DecideCoverPenetration() => coverPenetration;

        public CoverExtentRuling DecideCoverExtent() => coverExtent;

        public HelplessReprieveRuling DecideBackstabHelplessReprieve() => reprieve;

        public StrayTargetRuling DecideFiringIntoCombatStrayTarget(int bystanderCount) => new(strayTargetIndex);
    }
}
