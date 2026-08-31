using Brp.Core.Abilities;
using Brp.Core.Contests;

namespace Brp.Core.Tests.Contests;

/// <summary>
/// Covers the named gamemaster-discretion ports for the Ch 7 injury/effect spot rules (Falling,
/// Poison, Disease): the canonical decision ids, the documented neutral defaults of
/// <see cref="DefaultInjuryAdjudicator"/>, and that a deterministic stub can drive every port for
/// replayable tests. See <c>docs/decisions/0019-injury-spot-rules.md</c>.
/// </summary>
public class InjuryAdjudicatorTests
{
    [Theory]
    [InlineData(InjuryDecisionId.FallingSurface, "falling-surface")]
    [InlineData(InjuryDecisionId.PoisonOnset, "poison-onset")]
    [InlineData(InjuryDecisionId.AntidoteCrossType, "antidote-cross-type")]
    [InlineData(InjuryDecisionId.DiseaseAffectedCharacteristic, "disease-affected-characteristic")]
    public void Every_decision_id_has_its_canonical_kebab_case_string(InjuryDecisionId id, string expected)
    {
        Assert.Equal(expected, InjuryDecisionIds.CanonicalId(id));
    }

    [Fact]
    public void The_named_discretion_points_are_exactly_those_issue_96_calls_out()
    {
        Assert.Equal(4, Enum.GetValues<InjuryDecisionId>().Length);
    }

    [Fact]
    public void The_default_adjudicator_returns_the_documented_minimal_assumption_answers()
    {
        var adjudicator = new DefaultInjuryAdjudicator();

        Assert.Equal(0, adjudicator.DecideFallingSurface().DamageAdjustment);
        Assert.Equal(new PoisonOnsetRuling(PoisonOnsetSpeed.FastActing, null), adjudicator.DecidePoisonOnset());
        Assert.Equal(0, adjudicator.DecideAntidoteCrossTypePotency(crossTypeAntidotePotency: 8));
        Assert.Equal(new CharacteristicId("CON"), adjudicator.DecideDiseaseAffectedCharacteristic());
    }

    [Fact]
    public void A_deterministic_stub_drives_every_port_with_scripted_answers()
    {
        IInjuryAdjudicator adjudicator = new ScriptedInjuryAdjudicator(
            fallingSurface: new FallingSurfaceRuling(DamageAdjustment: -3),
            poisonOnset: new PoisonOnsetRuling(PoisonOnsetSpeed.SlowActing, GamemasterSpecifiedDelay: 5),
            crossTypeAntidotePotency: 4,
            diseaseCharacteristic: new CharacteristicId("STR"));

        Assert.Equal(-3, adjudicator.DecideFallingSurface().DamageAdjustment);
        Assert.Equal(
            new PoisonOnsetRuling(PoisonOnsetSpeed.SlowActing, 5), adjudicator.DecidePoisonOnset());
        Assert.Equal(4, adjudicator.DecideAntidoteCrossTypePotency(crossTypeAntidotePotency: 10));
        Assert.Equal(new CharacteristicId("STR"), adjudicator.DecideDiseaseAffectedCharacteristic());
    }

    /// <summary>A deterministic test double returning pre-scripted rulings for each port.</summary>
    private sealed class ScriptedInjuryAdjudicator(
        FallingSurfaceRuling fallingSurface,
        PoisonOnsetRuling poisonOnset,
        int crossTypeAntidotePotency,
        CharacteristicId diseaseCharacteristic) : IInjuryAdjudicator
    {
        public FallingSurfaceRuling DecideFallingSurface() => fallingSurface;

        public PoisonOnsetRuling DecidePoisonOnset() => poisonOnset;

        public int DecideAntidoteCrossTypePotency(int crossTypeAntidotePotency2) => crossTypeAntidotePotency;

        public CharacteristicId DecideDiseaseAffectedCharacteristic() => diseaseCharacteristic;
    }
}
