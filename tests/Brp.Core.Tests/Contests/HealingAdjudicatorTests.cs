using Brp.Core.Contests;

namespace Brp.Core.Tests.Contests;

/// <summary>
/// Covers the named gamemaster-discretion ports for the Ch 6 healing rules (#109): the canonical
/// decision ids, the documented neutral defaults of <see cref="DefaultHealingAdjudicator"/>, and that
/// a deterministic stub can drive every port for replayable tests. See
/// <c>docs/decisions/0023-healing-and-recovery.md</c>.
/// </summary>
public class HealingAdjudicatorTests
{
    [Theory]
    [InlineData(HealingDecisionId.ConditionsTier, "healing-conditions-tier")]
    [InlineData(HealingDecisionId.Caregiver, "healing-caregiver")]
    public void Every_decision_id_has_its_canonical_kebab_case_string(HealingDecisionId id, string expected)
    {
        Assert.Equal(expected, HealingDecisionIds.CanonicalId(id));
    }

    [Fact]
    public void The_named_discretion_points_are_exactly_those_issue_109_calls_out()
    {
        Assert.Equal(2, Enum.GetValues<HealingDecisionId>().Length);
    }

    [Fact]
    public void The_default_adjudicator_returns_the_documented_minimal_assumption_answers()
    {
        var adjudicator = new DefaultHealingAdjudicator();

        // The middle tier heals normally with no gating roll -- the least-assuming reading.
        Assert.Equal(MedicalCareTier.Decent, adjudicator.DecideConditionsTier());

        // First Aid is the more broadly trained skill (30% vs Medicine's 05%).
        Assert.Equal(new CaregiverRuling(CaregiverSkill.FirstAid), adjudicator.DecideCaregiver());
    }

    [Fact]
    public void A_deterministic_stub_drives_every_port_with_scripted_answers()
    {
        IHealingAdjudicator adjudicator = new ScriptedHealingAdjudicator(
            tier: MedicalCareTier.Poor, caregiver: new CaregiverRuling(CaregiverSkill.Medicine));

        Assert.Equal(MedicalCareTier.Poor, adjudicator.DecideConditionsTier());
        Assert.Equal(new CaregiverRuling(CaregiverSkill.Medicine), adjudicator.DecideCaregiver());
    }

    /// <summary>A deterministic test double returning pre-scripted rulings for each port.</summary>
    private sealed class ScriptedHealingAdjudicator(MedicalCareTier tier, CaregiverRuling caregiver)
        : IHealingAdjudicator
    {
        public MedicalCareTier DecideConditionsTier() => tier;

        public CaregiverRuling DecideCaregiver() => caregiver;
    }
}
