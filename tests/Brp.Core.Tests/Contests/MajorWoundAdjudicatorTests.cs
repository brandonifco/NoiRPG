using Brp.Core.Abilities;
using Brp.Core.Contests;

namespace Brp.Core.Tests.Contests;

/// <summary>
/// Covers the named gamemaster-discretion ports for Ch 6: Combat, "Major Wounds" (pp.155-156): the
/// canonical decision ids, the documented neutral defaults of
/// <see cref="DefaultMajorWoundAdjudicator"/>, and that a deterministic stub can drive every port for
/// replayable tests. See <c>docs/decisions/0021-major-wounds.md</c>.
/// </summary>
public class MajorWoundAdjudicatorTests
{
    [Theory]
    [InlineData(MajorWoundDecisionId.LimbSide, "major-wound-limb-side")]
    [InlineData(MajorWoundDecisionId.Characteristics, "major-wound-characteristics")]
    public void Every_decision_id_has_its_canonical_kebab_case_string(MajorWoundDecisionId id, string expected)
    {
        Assert.Equal(expected, MajorWoundDecisionIds.CanonicalId(id));
    }

    [Fact]
    public void The_named_discretion_points_are_exactly_those_issue_111_calls_out()
    {
        Assert.Equal(2, Enum.GetValues<MajorWoundDecisionId>().Length);
    }

    [Fact]
    public void The_default_adjudicator_returns_the_documented_minimal_assumption_answers()
    {
        var adjudicator = new DefaultMajorWoundAdjudicator();

        // Ch 6, p.155: the book's 1D6 low half (1-3) is left; the neutral default is left, without rolling.
        Assert.Equal(BodySide.Left, adjudicator.DecideLimbSide());

        // The 00 row's four distinct characteristics, deterministic and non-repeating.
        var chosen = adjudicator.DecideCharacteristics(4);
        Assert.Equal(4, chosen.Count);
        Assert.Equal(4, chosen.Distinct().Count());
        Assert.Equal(
            new[] { new CharacteristicId("STR"), new CharacteristicId("CON"), new CharacteristicId("DEX"), new CharacteristicId("INT") },
            chosen);
    }

    [Fact]
    public void The_default_adjudicator_rejects_a_non_positive_or_oversized_characteristic_count()
    {
        var adjudicator = new DefaultMajorWoundAdjudicator();

        Assert.Throws<ArgumentOutOfRangeException>(() => adjudicator.DecideCharacteristics(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => adjudicator.DecideCharacteristics(99));
    }

    [Fact]
    public void A_deterministic_stub_drives_every_port_with_scripted_answers()
    {
        IMajorWoundAdjudicator adjudicator = new ScriptedMajorWoundAdjudicator(
            limbSide: BodySide.Right,
            characteristics: [new CharacteristicId("POW"), new CharacteristicId("CHA"), new CharacteristicId("SIZ"), new CharacteristicId("STR")]);

        Assert.Equal(BodySide.Right, adjudicator.DecideLimbSide());
        Assert.Equal(
            new[] { new CharacteristicId("POW"), new CharacteristicId("CHA"), new CharacteristicId("SIZ"), new CharacteristicId("STR") },
            adjudicator.DecideCharacteristics(4));
    }

    /// <summary>A deterministic test double returning pre-scripted rulings for each port.</summary>
    private sealed class ScriptedMajorWoundAdjudicator(BodySide limbSide, IReadOnlyList<CharacteristicId> characteristics)
        : IMajorWoundAdjudicator
    {
        public BodySide DecideLimbSide() => limbSide;

        public IReadOnlyList<CharacteristicId> DecideCharacteristics(int count) => characteristics;
    }
}
