using Brp.Core.Abilities;
using Brp.Core.Skills;
using Brp.Data;
using Brp.Rules.Characters;
using Brp.Rules.Creation;

namespace Brp.Rules.Tests.Creation;

public class CharacterBuilderTests
{
    private static readonly CharacterCreationRuleset Ruleset = NoirCharacterCreationRuleset.Load();
    private static readonly AbilityRuleset AbilityRuleset = NoirAbilityRuleset.Load();
    private static readonly SkillRegistry SkillRegistry = NoirSkillRuleset.Load();

    private static Dictionary<CharacteristicId, int> ZeroDeltas() => Ruleset.CharacteristicCosts.Keys
        .ToDictionary(id => id, _ => 0);

    private static CharacterBuilder MakeBuilder() => new(Ruleset, AbilityRuleset, SkillRegistry);

    [Fact]
    public void Builds_a_valid_character_from_a_minimal_request()
    {
        var request = new CharacterCreationRequest
        {
            Id = new CharacterId("pc-1"),
            Name = "Jane Doe",
            CharacteristicDeltas = ZeroDeltas(),
        };

        var character = MakeBuilder().Build(request);

        Assert.Equal("Jane Doe", character.Name);
        // Spot's printed base is a flat 25% (Ch 3, p.50); with no points spent, the
        // starting rating is exactly the printed base.
        Assert.Equal(25, character.Skill(new SkillId("Spot")).CurrentRating);
    }

    [Fact]
    public void Applying_a_freeform_profession_package_sets_starting_skill_ratings()
    {
        var package = NoirBackgroundPackageRuleset.LoadAll().Single();
        var request = new CharacterCreationRequest
        {
            Id = new CharacterId("pc-2"),
            Name = "Investigator",
            CharacteristicDeltas = ZeroDeltas(),
            Package = package,
        };

        var character = MakeBuilder().Build(request);

        // Research: printed base 25% (Ch 3, p.48) + the package's 40 points.
        Assert.Equal(65, character.Skill(new SkillId("Research")).CurrentRating);
        // Streetwise: printed base 5% + the package's 30 points.
        Assert.Equal(35, character.Skill(new SkillId("Streetwise")).CurrentRating);
    }

    [Fact]
    public void Additional_professional_points_stack_with_the_packages_own_allocation()
    {
        var package = NoirBackgroundPackageRuleset.LoadAll().Single();
        var request = new CharacterCreationRequest
        {
            Id = new CharacterId("pc-3"),
            Name = "Investigator",
            CharacteristicDeltas = ZeroDeltas(),
            Package = package,
            AdditionalProfessionalSkillPoints = new Dictionary<SkillId, int>
            {
                [new SkillId("Research")] = 5,
            },
        };

        var character = MakeBuilder().Build(request);

        Assert.Equal(70, character.Skill(new SkillId("Research")).CurrentRating);
    }

    [Fact]
    public void Professional_points_spent_beyond_the_pool_are_rejected()
    {
        var request = new CharacterCreationRequest
        {
            Id = new CharacterId("pc-4"),
            Name = "Overspender",
            CharacteristicDeltas = ZeroDeltas(),
            AdditionalProfessionalSkillPoints = new Dictionary<SkillId, int>
            {
                [new SkillId("Spot")] = 50,
                [new SkillId("Insight")] = Ruleset.ProfessionalSkillPoints, // pushes total over 250
            },
        };

        Assert.Throws<ArgumentException>(() => MakeBuilder().Build(request));
    }

    [Fact]
    public void Personal_skill_points_are_capped_at_int_times_the_raw_multiplier_when_increased_points_are_off()
    {
        // INT 10 (default) x10 RAW multiplier (Ch 2 p.8) = 100 personal points available.
        var request = new CharacterCreationRequest
        {
            Id = new CharacterId("pc-5"),
            Name = "Overspender",
            CharacteristicDeltas = ZeroDeltas(),
            UseIncreasedPersonalSkillPoints = false,
            PersonalSkillPoints = new Dictionary<SkillId, int>
            {
                [new SkillId("Sense")] = 50,
                [new SkillId("Navigate")] = 51, // 101 total, one over the INT x10 cap of 100
            },
        };

        Assert.Throws<ArgumentException>(() => MakeBuilder().Build(request));
    }

    [Fact]
    public void Increased_personal_skill_points_raises_the_pool_from_int_times_ten_to_int_times_fifteen()
    {
        // Ch 2 p.8, "Increased Personal Skill Points (Option)": this is a deliberate NoiRPG
        // extension of the heroic-tier INT x15 multiplier to Normal power level -- see the
        // ADR and CharacterCreationRuleset.IncreasedPersonalSkillPointsIntMultiplier.
        var request = new CharacterCreationRequest
        {
            Id = new CharacterId("pc-6"),
            Name = "Competent Professional",
            CharacteristicDeltas = ZeroDeltas(),
            UseIncreasedPersonalSkillPoints = true,
            PersonalSkillPoints = new Dictionary<SkillId, int>
            {
                // 130 points: impossible under INT x10 = 100, allowed under INT x15 = 150.
                [new SkillId("Sense")] = 65,
                [new SkillId("Navigate")] = 65,
            },
        };

        var character = MakeBuilder().Build(request);

        Assert.Equal(75, character.Skill(new SkillId("Sense")).CurrentRating);
    }

    [Fact]
    public void No_skill_may_start_above_the_seventy_five_percent_soft_cap()
    {
        // Ch 2 p.8: "No skill should begin higher than 75%."
        var request = new CharacterCreationRequest
        {
            Id = new CharacterId("pc-7"),
            Name = "Overqualified",
            CharacteristicDeltas = ZeroDeltas(),
            AdditionalProfessionalSkillPoints = new Dictionary<SkillId, int>
            {
                // Spot's printed base is 25%; 55 points would push it to 80%, above the cap.
                [new SkillId("Spot")] = 55,
            },
        };

        Assert.Throws<ArgumentException>(() => MakeBuilder().Build(request));
    }

    [Fact]
    public void Education_defaults_to_the_ruleset_starting_value_and_can_be_overridden()
    {
        var defaultRequest = new CharacterCreationRequest
        {
            Id = new CharacterId("pc-8"),
            Name = "Default EDU",
            CharacteristicDeltas = ZeroDeltas(),
        };
        var overriddenRequest = new CharacterCreationRequest
        {
            Id = new CharacterId("pc-9"),
            Name = "Overridden EDU",
            CharacteristicDeltas = ZeroDeltas(),
            Education = 16,
        };

        var defaultCharacter = MakeBuilder().Build(defaultRequest);
        var overriddenCharacter = MakeBuilder().Build(overriddenRequest);

        Assert.Equal(10, defaultCharacter.Abilities.ValueOf(new CharacteristicId("EDU")));
        Assert.Equal(16, overriddenCharacter.Abilities.ValueOf(new CharacteristicId("EDU")));
    }
}
