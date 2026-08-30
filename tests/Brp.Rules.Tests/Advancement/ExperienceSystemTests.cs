using Brp.Core.Abilities;
using Brp.Core.Primitives;
using Brp.Core.Skills;
using Brp.Data;
using Brp.Rules.Advancement;
using Brp.Rules.Characters;

namespace Brp.Rules.Tests.Advancement;

public class ExperienceSystemTests
{
    private static readonly ExperienceRuleset Ruleset = NoirExperienceRuleset.Load();

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

    [Theory]
    [InlineData(100)]
    [InlineData(130)]
    public void Improvement_roll_grants_an_improvement_on_a_natural_100_once_the_skill_is_at_or_above_100_percent(int rating)
    {
        // Ch 5 p.138, "Exceeding 100% in a Skill": "No matter how much over 100% the skill
        // has risen, any roll of 100 or over earns a skill improvement." Before this fix, a
        // skill at or above 100% could never improve: `roll <= skill.CurrentRating` with
        // roll capped at 100 is always true once the rating reaches 100.
        var skill = MakeSkill(rating);
        TickViaRealStakes(skill);
        var entropy = new FixedEntropySource(100, 3); // natural 100, then a gain of 3

        var gain = ExperienceSystem.ImprovementRoll(skill, entropy);

        Assert.Equal(3, gain);
        Assert.Equal(rating + 3, skill.CurrentRating);
    }

    [Fact]
    public void Improvement_roll_below_100_percent_still_requires_beating_the_rating_not_just_reaching_100()
    {
        // A rating of 99 is still below the 100%-and-above regime: the roll must exceed
        // 99, so a roll of exactly 99 still fails (unlike the >=100 case above).
        var skill = MakeSkill(rating: 99);
        TickViaRealStakes(skill);
        var entropy = new FixedEntropySource(99); // one value only: a gain draw would throw

        var gain = ExperienceSystem.ImprovementRoll(skill, entropy);

        Assert.Equal(0, gain);
        Assert.Equal(99, skill.CurrentRating);
    }

    [Fact]
    public void Teach_grants_a_gain_on_a_successful_teach_roll()
    {
        var student = MakeSkill(rating: 30);
        var entropy = new FixedEntropySource(50, 5); // teach roll 50: a Success at 60% chance, then +5

        var gain = ExperienceSystem.Teach(student, Percent.Of(60), entropy, Ruleset);

        Assert.Equal(5, gain);
        Assert.Equal(35, student.CurrentRating);
    }

    [Fact]
    public void Teach_grants_nothing_on_a_failed_teach_roll()
    {
        var student = MakeSkill(rating: 30);
        var entropy = new FixedEntropySource(70); // teach roll 70: a plain Failure at 60% chance

        var gain = ExperienceSystem.Teach(student, Percent.Of(60), entropy, Ruleset);

        Assert.Equal(0, gain);
        Assert.Equal(30, student.CurrentRating);
    }

    [Fact]
    public void Teach_caps_the_gain_so_training_alone_cannot_carry_a_skill_past_the_seventy_five_percent_ceiling()
    {
        // Ch 5 p.139: "No skill can be trained above 75%, no matter how good the
        // instructor. Any increase above this must come through successful use of the
        // skill." 74 + a full 1D6 gain of 6 would reach 80%; training must stop at 75%.
        var student = MakeSkill(rating: 74);
        var entropy = new FixedEntropySource(50, 6); // teach roll 50: a Success at 80% chance, then +6

        var gain = ExperienceSystem.Teach(student, Percent.Of(80), entropy, Ruleset);

        Assert.Equal(1, gain);
        Assert.Equal(75, student.CurrentRating);
    }

    [Fact]
    public void Teach_grants_nothing_more_once_a_skill_is_already_at_the_training_ceiling()
    {
        var student = MakeSkill(rating: 75);
        var entropy = new FixedEntropySource(50, 6); // a Success that would otherwise grant +6

        var gain = ExperienceSystem.Teach(student, Percent.Of(80), entropy, Ruleset);

        Assert.Equal(0, gain);
        Assert.Equal(75, student.CurrentRating);
    }

    [Fact]
    public void Shipped_experience_ruleset_data_reproduces_ch5_p139s_seventy_five_percent_training_cap()
    {
        Assert.Equal(75, Ruleset.TrainingCapPercent);
    }

    [Fact]
    public void Teach_reads_the_training_cap_from_ruleset_data_not_a_hardcoded_constant()
    {
        // A ruleset with a different training cap must produce a different clamp -- proof
        // the value is read from the injected ExperienceRuleset, not a bare constant inside
        // ExperienceSystem.
        var lowCapRuleset = new ExperienceRuleset(trainingCapPercent: 40);
        var student = MakeSkill(rating: 35);
        var entropy = new FixedEntropySource(50, 6); // teach roll 50: a Success at 80% chance, then +6

        var gain = ExperienceSystem.Teach(student, Percent.Of(80), entropy, lowCapRuleset);

        Assert.Equal(5, gain);
        Assert.Equal(40, student.CurrentRating);
    }

    [Fact]
    public void Teach_fumble_degrades_the_students_skill_instead_of_improving_it()
    {
        // Ch 5 p.138: "a fumble is counterproductive, with the teacher causing self-doubt
        // and contradicting your character's prior learnings, reducing the skill by -1D3."
        // At a 60% teach chance, the fumble band is 98-100 (Ch 5's standard fumble rule,
        // Brp.Core.Resolution.ResolutionPolicy), so a roll of 99 fumbles.
        var student = MakeSkill(rating: 30);
        var entropy = new FixedEntropySource(99, 2); // fumble roll, then a -1D3 of 2

        var gain = ExperienceSystem.Teach(student, Percent.Of(60), entropy, Ruleset);

        Assert.Equal(-2, gain);
        Assert.Equal(28, student.CurrentRating);
    }

    [Fact]
    public void Teach_fumble_never_degrades_a_skill_below_zero()
    {
        var student = MakeSkill(rating: 1);
        var entropy = new FixedEntropySource(99, 3); // fumble roll, then a -1D3 of 3

        var gain = ExperienceSystem.Teach(student, Percent.Of(60), entropy, Ruleset);

        // The rating can only fall from 1 to 0 -- the returned change reflects what was
        // actually applied, not the raw, unclamped -1D3 penalty.
        Assert.Equal(-1, gain);
        Assert.Equal(0, student.CurrentRating);
    }
}
