using Brp.Core.Contests;

namespace Brp.Core.Tests.Contests;

/// <summary>
/// Covers the named gamemaster-discretion port for the Ch 6 fumble tables (pp.148-149): the
/// canonical decision id, the documented neutral default of <see cref="DefaultFumbleAdjudicator"/>,
/// and that a deterministic stub can drive the port for replayable tests. See
/// <c>docs/decisions/0020-fumble-tables.md</c>.
/// </summary>
public class FumbleAdjudicatorTests
{
    [Theory]
    [InlineData(FumbleDecisionId.AllyInRange, "fumble-ally-in-range")]
    public void Every_decision_id_has_its_canonical_kebab_case_string(FumbleDecisionId id, string expected)
    {
        Assert.Equal(expected, FumbleDecisionIds.CanonicalId(id));
    }

    [Fact]
    public void The_named_discretion_points_are_exactly_those_issue_97_calls_out()
    {
        Assert.Single(Enum.GetValues<FumbleDecisionId>());
    }

    [Fact]
    public void The_default_adjudicator_assumes_no_ally_in_range()
    {
        Assert.False(new DefaultFumbleAdjudicator().IsAllyInRange());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void A_deterministic_stub_drives_the_port_with_a_scripted_answer(bool allyInRange)
    {
        IFumbleAdjudicator adjudicator = new ScriptedFumbleAdjudicator(allyInRange);

        Assert.Equal(allyInRange, adjudicator.IsAllyInRange());
    }

    /// <summary>A deterministic test double returning a pre-scripted ruling for the port.</summary>
    private sealed class ScriptedFumbleAdjudicator(bool allyInRange) : IFumbleAdjudicator
    {
        public bool IsAllyInRange() => allyInRange;
    }
}
