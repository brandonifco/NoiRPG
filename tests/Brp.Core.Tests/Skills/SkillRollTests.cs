using Brp.Core.Abilities;
using Brp.Core.Primitives;
using Brp.Core.Randomness;
using Brp.Core.Resolution;
using Brp.Core.Skills;

namespace Brp.Core.Tests.Skills;

public class SkillRollTests
{
    [Fact]
    public void Resolve_supplies_the_base_chance_from_the_skill_definition_and_the_rating_from_the_caller()
    {
        var dodge = new SkillDefinition(
            new SkillId("Dodge"), "Dodge", SkillCategory.Physical,
            new CharacteristicFormulaBaseChance(new CharacteristicTerm(new CharacteristicId("DEX"), 2)));

        var outcome = SkillRoll.Resolve(dodge, AbilityHelper.Default(), effectiveChance: Percent.Of(60), roll: 30);

        // DEX 12 (AbilityHelper.Default) => base chance 24, carried through for provenance/floor use.
        Assert.Equal(Percent.Of(24), outcome.BaseChance);
        Assert.Equal(Percent.Of(60), outcome.EffectiveChance);
    }

    [Fact]
    public void Resolve_with_an_explicit_roll_matches_SkillResolver_given_the_same_inputs()
    {
        var spot = new SkillDefinition(new SkillId("Spot"), "Spot", SkillCategory.Perception, new ConstantBaseChance(Percent.Of(25)));

        var viaSkillRoll = SkillRoll.Resolve(spot, AbilityHelper.Default(), Percent.Of(40), roll: 7);
        var viaResolver = SkillResolver.Resolve(Percent.Of(25), Percent.Of(40), roll: 7);

        Assert.Equal(viaResolver, viaSkillRoll);
    }

    [Fact]
    public void Resolve_with_an_entropy_source_draws_exactly_one_D100()
    {
        var spot = new SkillDefinition(new SkillId("Spot"), "Spot", SkillCategory.Perception, new ConstantBaseChance(Percent.Of(25)));
        var entropy = new Xoshiro256StarStar(seed: 7);
        var before = entropy.DrawCount;

        SkillRoll.Resolve(spot, AbilityHelper.Default(), Percent.Of(40), entropy);

        Assert.Equal(before + 1, entropy.DrawCount);
    }

    public static TheoryData<string, int> PrintedSubFivePercentBaseSkills => new()
    {
        // Ch 3: Skills, "Alphabetical Skill List" (pp.33-34): Science and Martial Arts are
        // printed on p.33, Strategy on p.34 -- each at a 01% base chance, below the
        // 5%-floor rule's threshold (Ch 5, "Skill Rolls", p.128).
        { "Science", 1 },
        { "Strategy", 1 },
        { "Martial Arts", 1 },
    };

    [Theory]
    [MemberData(nameof(PrintedSubFivePercentBaseSkills))]
    public void Falsification_a_sub_5_percent_printed_base_does_not_gain_successes_on_01_to_05(
        string name, int printedBase)
    {
        // The pinning target named in #35: Science, Strategy, and Martial Arts print a 01%
        // base chance. Ch 5, "Skill Rolls" (p.128) rescues rolls 01-05 only when the *base*
        // chance is 5% or higher -- this base is 1%, so even though this character has
        // trained the skill to an effective rating (3%) above the printed base, a roll of
        // 04 (inside 01-05) must still fail rather than being rescued by the floor. Routed
        // through SkillRoll, not bare SkillResolver.Resolve numbers, to prove the Layer 2
        // path preserves the distinction rather than silently keying the floor on the
        // effective rating instead.
        var skill = new SkillDefinition(new SkillId(name), name, SkillCategory.Mental, new ConstantBaseChance(Percent.Of(printedBase)));

        var outcome = SkillRoll.Resolve(skill, AbilityHelper.Default(), effectiveChance: Percent.Of(3), roll: 4);

        Assert.Equal(SuccessLevel.Failure, outcome.Level);
    }

    [Fact]
    public void Contrast_a_5_percent_or_higher_printed_base_is_rescued_on_the_same_01_to_05_roll()
    {
        // The contrasting case for the pinning test above: a skill printed at 25% (Spot)
        // held at the same low effective rating (3%) *is* rescued by the floor on the same
        // roll of 04, because its base chance qualifies. This is what tells the previous
        // test its assertion is actually testing the base-chance gate, not some unrelated
        // reason the roll failed.
        var spot = new SkillDefinition(new SkillId("Spot"), "Spot", SkillCategory.Perception, new ConstantBaseChance(Percent.Of(25)));

        var outcome = SkillRoll.Resolve(spot, AbilityHelper.Default(), effectiveChance: Percent.Of(3), roll: 4);

        Assert.Equal(SuccessLevel.Success, outcome.Level);
    }
}
