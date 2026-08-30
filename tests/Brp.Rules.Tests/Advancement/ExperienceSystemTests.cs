using Brp.Core.Abilities;
using Brp.Core.Primitives;
using Brp.Core.Skills;
using Brp.Data;
using Brp.Rules.Advancement;
using Brp.Rules.Characters;

namespace Brp.Rules.Tests.Advancement;

public class ExperienceSystemTests
{
    private static CharacterSkill MakeSkill(int rating = 20) => new(
        new SkillDefinition(new SkillId("Test Skill"), "Test Skill", SkillCategory.Mental, new ConstantBaseChance(Percent.Of(5))),
        rating);

    /// <summary>Marks a skill's experience check via the public gate, exactly as play would.</summary>
    private static void TickViaRealStakes(CharacterSkill skill) =>
        ExperienceSystem.RecordUse(new CaseExperienceLedger(), skill, CheckStakes.RealStakes, succeeded: true, ExperiencePolicy.TickOnUse);

    private static AbilitySet MakeAbilities()
    {
        var ruleset = NoirAbilityRuleset.Load();
        var values = ruleset.Characteristics.Keys.ToDictionary(id => id, _ => 10);
        return new AbilitySet(ruleset, values);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Tick_on_use_records_a_tick_regardless_of_success_or_failure(bool succeeded)
    {
        var ledger = new CaseExperienceLedger();
        var skill = MakeSkill();

        var ticked = ExperienceSystem.RecordUse(ledger, skill, CheckStakes.RealStakes, succeeded, ExperiencePolicy.TickOnUse);

        Assert.True(ticked);
        Assert.True(skill.HasExperienceCheck);
    }

    [Theory]
    [InlineData(CheckStakes.Easy)]
    [InlineData(CheckStakes.NoStakes)]
    public void Easy_or_no_stakes_checks_never_record_a_tick_under_either_policy(CheckStakes stakes)
    {
        // Ch 5: System, "Skill Improvement" (p.138): "If a skill roll was Easy, no
        // experience check is allowed." The "nothing at stake" gate is enforced the same
        // way -- mechanically, with no gamemaster to adjudicate it either way.
        var tickOnUseLedger = new CaseExperienceLedger();
        var rawLedger = new CaseExperienceLedger();
        var tickOnUseSkill = MakeSkill();
        var rawSkill = MakeSkill();

        Assert.False(ExperienceSystem.RecordUse(tickOnUseLedger, tickOnUseSkill, stakes, succeeded: true, ExperiencePolicy.TickOnUse));
        Assert.False(ExperienceSystem.RecordUse(rawLedger, rawSkill, stakes, succeeded: true, ExperiencePolicy.RawTickOnSuccess));
        Assert.False(tickOnUseSkill.HasExperienceCheck);
        Assert.False(rawSkill.HasExperienceCheck);
    }

    [Fact]
    public void Raw_policy_requires_success_to_tick_the_falsification_target()
    {
        // rules-conformance's falsification target: the RAW toggle must reproduce BRP's
        // tick-on-success behavior exactly. Ch 5 p.138: "If a skill is used successfully,
        // you almost always get an experience check."
        var ledger = new CaseExperienceLedger();
        var failedSkill = MakeSkill();
        var succeededSkill = MakeSkill();

        var tickedOnFailure = ExperienceSystem.RecordUse(
            ledger, failedSkill, CheckStakes.RealStakes, succeeded: false, ExperiencePolicy.RawTickOnSuccess);
        var tickedOnSuccess = ExperienceSystem.RecordUse(
            ledger, succeededSkill, CheckStakes.RealStakes, succeeded: true, ExperiencePolicy.RawTickOnSuccess);

        Assert.False(tickedOnFailure);
        Assert.True(tickedOnSuccess);
    }

    [Fact]
    public void A_skill_ticks_at_most_once_per_case_no_matter_how_many_times_it_is_used()
    {
        // Ch 5 p.138: "An experience check for a particular skill is made only once per
        // adventure, no matter how many times the skill is successfully used."
        var ledger = new CaseExperienceLedger();
        var skill = MakeSkill();

        var firstUse = ExperienceSystem.RecordUse(ledger, skill, CheckStakes.RealStakes, succeeded: true, ExperiencePolicy.TickOnUse);
        var secondUse = ExperienceSystem.RecordUse(ledger, skill, CheckStakes.RealStakes, succeeded: false, ExperiencePolicy.TickOnUse);

        Assert.True(firstUse);
        Assert.False(secondUse);
    }

    [Fact]
    public void A_fresh_case_ledger_allows_the_same_skill_to_tick_again()
    {
        var skill = MakeSkill();
        var caseOne = new CaseExperienceLedger();
        var caseTwo = new CaseExperienceLedger();

        ExperienceSystem.RecordUse(caseOne, skill, CheckStakes.RealStakes, succeeded: true, ExperiencePolicy.TickOnUse);
        var tickedAgain = ExperienceSystem.RecordUse(caseTwo, skill, CheckStakes.RealStakes, succeeded: true, ExperiencePolicy.TickOnUse);

        Assert.True(tickedAgain);
    }

    [Fact]
    public void Improvement_roll_gains_when_the_roll_exceeds_the_current_rating()
    {
        // Ch 5 p.138: "If the result of an experience roll is higher than your character's
        // current skill rating, then the experience roll succeeds", and "add +1D6 to the
        // skill rating."
        var skill = MakeSkill(rating: 20);
        TickViaRealStakes(skill);
        var entropy = new FixedEntropySource(21, 4);

        var gain = ExperienceSystem.ImprovementRoll(skill, entropy);

        Assert.Equal(4, gain);
        Assert.Equal(24, skill.CurrentRating);
        Assert.False(skill.HasExperienceCheck);
    }

    [Fact]
    public void Improvement_roll_grants_nothing_when_the_roll_does_not_exceed_the_current_rating()
    {
        var skill = MakeSkill(rating: 20);
        TickViaRealStakes(skill);
        // Only one scripted value: a second draw (the gain die) would throw, proving the
        // implementation does not draw it when the roll fails.
        var entropy = new FixedEntropySource(20);

        var gain = ExperienceSystem.ImprovementRoll(skill, entropy);

        Assert.Equal(0, gain);
        Assert.Equal(20, skill.CurrentRating);
        Assert.False(skill.HasExperienceCheck);
    }

    [Fact]
    public void Improvement_roll_is_a_no_op_without_an_experience_check()
    {
        var skill = MakeSkill(rating: 20);
        var entropy = new FixedEntropySource(); // no scripted values: any draw throws

        var gain = ExperienceSystem.ImprovementRoll(skill, entropy);

        Assert.Equal(0, gain);
        Assert.Equal(20, skill.CurrentRating);
    }

    [Fact]
    public void Improvement_roll_adds_the_experience_bonus_to_the_roll_but_not_to_the_gain()
    {
        // Ch 5 p.138: "The experience bonus is not added to the actual skill points
        // gained, just to the roll to see if there is improvement."
        var skill = MakeSkill(rating: 20);
        TickViaRealStakes(skill);
        var entropy = new FixedEntropySource(19, 3); // 19 alone would fail; +2 bonus succeeds

        var gain = ExperienceSystem.ImprovementRoll(skill, entropy, experienceBonus: 2);

        Assert.Equal(3, gain);
        Assert.Equal(23, skill.CurrentRating);
    }

    [Fact]
    public void Close_case_resolves_every_ticked_skill_in_a_stable_deterministic_order()
    {
        var skillA = MakeSkill(rating: 10);
        var skillB = MakeSkill(rating: 90);
        TickViaRealStakes(skillA);
        TickViaRealStakes(skillB);

        var character = new Character(
            new CharacterId("pc"),
            "Case Closer",
            MakeAbilities(),
            new Dictionary<SkillId, CharacterSkill>
            {
                [new SkillId("A")] = skillA,
                [new SkillId("B")] = skillB,
            });

        // A ticks first (alphabetical id order): rolls 50 (> 10, gains 2). B ticks second:
        // rolls 50 (not > 90, no gain).
        var entropy = new FixedEntropySource(50, 2, 50);

        var results = ExperienceSystem.CloseCase(character, entropy);

        Assert.Equal(2, results[new SkillId("A")]);
        Assert.Equal(0, results[new SkillId("B")]);
        Assert.Equal(12, skillA.CurrentRating);
        Assert.Equal(90, skillB.CurrentRating);
    }

    [Fact]
    public void Teach_grants_a_gain_on_a_successful_teach_roll()
    {
        var student = MakeSkill(rating: 30);
        var entropy = new FixedEntropySource(50, 5); // teach roll 50 <= 60% chance, then +5

        var taught = ExperienceSystem.Teach(student, Percent.Of(60), entropy);

        Assert.True(taught);
        Assert.Equal(35, student.CurrentRating);
    }

    [Fact]
    public void Teach_grants_nothing_on_a_failed_teach_roll()
    {
        var student = MakeSkill(rating: 30);
        var entropy = new FixedEntropySource(70); // teach roll 70 > 60% chance: fails

        var taught = ExperienceSystem.Teach(student, Percent.Of(60), entropy);

        Assert.False(taught);
        Assert.Equal(30, student.CurrentRating);
    }
}
