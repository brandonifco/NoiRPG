using Brp.Core.Abilities;
using Brp.Core.Primitives;
using Brp.Core.Skills;

namespace Brp.Data.Tests;

public class NoirSkillRulesetTests
{
    [Fact]
    public void The_registry_loads_the_full_in_scope_skill_list_with_no_duplicate_ids()
    {
        var registry = NoirSkillRuleset.Load();

        Assert.NotEmpty(registry.Skills);
        Assert.Equal(registry.Skills.Count, registry.Skills.Values.Select(s => s.Id).Distinct().Count());
    }

    public static TheoryData<string, string> CanonicalNamingRule => new()
    {
        // orc-scope-filter.md, "Skill naming: the framework's names win" -- the framework's
        // canonical id is the registry key; the book's own name is recorded separately.
        { "Streetwise", "Knowledge (Streetwise)" },
        { "Shadow", "Stealth" },
        { "Locksmith", "Fine Manipulation" },
        { "Accounting", "Knowledge (Accounting)" },
        { "Photography", "Art (Photography)" },
        { "Law", "Knowledge (Law)" },
    };

    [Theory]
    [MemberData(nameof(CanonicalNamingRule))]
    public void The_canonical_naming_rule_maps_framework_names_inward_to_book_skills(string frameworkName, string bookEquivalent)
    {
        var registry = NoirSkillRuleset.Load();

        var skill = registry[new SkillId(frameworkName)];

        Assert.Equal(frameworkName, skill.Name);
        Assert.Equal(bookEquivalent, skill.BookEquivalent);
    }

    [Fact]
    public void Intimidate_is_recorded_as_an_original_skill_with_no_book_equivalent()
    {
        // docs/decisions/0006-skill-bonus-system.md: "Intimidate is the only skill with no
        // book equivalent; Communication is the natural home." House rule, no book citation.
        var registry = NoirSkillRuleset.Load();

        var intimidate = registry[new SkillId("Intimidate")];

        Assert.Equal(SkillCategory.Communication, intimidate.Category);
        Assert.DoesNotContain("Knowledge", intimidate.BookEquivalent, StringComparison.Ordinal);
    }

    [Fact]
    public void Streetwise_and_law_are_independent_specialties_sharing_the_knowledge_parent()
    {
        var registry = NoirSkillRuleset.Load();

        var streetwise = registry[new SkillId("Streetwise")];
        var law = registry[new SkillId("Law")];

        Assert.True(streetwise.IsSpecialty);
        Assert.True(law.IsSpecialty);
        Assert.NotNull(streetwise.Parent);
        Assert.Same(streetwise.Parent, law.Parent);
        Assert.NotEqual(streetwise.Id, law.Id);
    }

    [Fact]
    public void Spot_reproduces_its_printed_constant_base_chance()
    {
        // Ch 3: Skills, "Spot" (p.50), "Base Chance: 25%".
        var registry = NoirSkillRuleset.Load();

        var spot = registry[new SkillId("Spot")];

        Assert.Equal(Percent.Of(25), spot.BaseChanceFor(Average()));
    }

    [Fact]
    public void Dodge_reproduces_its_printed_characteristic_formula()
    {
        // Ch 3: Skills, "Dodge" (p.37), "Base Chance: DEX×2".
        var registry = NoirSkillRuleset.Load();

        var dodge = registry[new SkillId("Dodge")];

        Assert.Equal(Percent.Of(30), dodge.BaseChanceFor(Average(dexterity: 15)));
    }

    [Fact]
    public void Drive_evaluates_to_the_modern_value_not_the_printed_historical_alternative()
    {
        // Ch 3: Skills, "Drive (various)" (p.37): "Base Chance: 20% or 01%". NoiRPG always
        // takes the modern (common-vehicle) value: AGENTS.md invariant 4.
        var registry = NoirSkillRuleset.Load();

        var drive = registry[new SkillId("Drive")];

        Assert.IsType<EraConditionalBaseChance>(drive.BaseChance);
        Assert.Equal(Percent.Of(20), drive.BaseChanceFor(Average()));
    }

    [Fact]
    public void Firearm_specialties_are_weapon_derived_and_cannot_evaluate_without_weapon_data()
    {
        // Ch 3: Skills, "Firearm (various)" (p.39), "Base Chance: As per weapon specialty".
        var registry = NoirSkillRuleset.Load();

        var handgun = registry[new SkillId("Firearm (Handgun)")];

        Assert.IsType<WeaponDerivedBaseChance>(handgun.BaseChance);
        Assert.Throws<InvalidOperationException>(() => handgun.BaseChanceFor(Average()));
    }

    public static TheoryData<string, int> SubFivePercentPrintedBaseSkills => new()
    {
        // Ch 3: Skills, "Alphabetical Skill List" (pp.33-34): Science and Martial Arts are
        // printed on p.33, Strategy on p.34, each at 01% -- the falsification target named
        // in #35. The Chemistry/Forensics specialties are authored at their parent's
        // printed value; the book does not price individual Science specialties separately.
        { "Science (Chemistry)", 1 },
        { "Science (Forensics)", 1 },
        { "Strategy", 1 },
        { "Martial Arts", 1 },
    };

    [Theory]
    [MemberData(nameof(SubFivePercentPrintedBaseSkills))]
    public void Falsification_target_skills_reproduce_their_printed_01_percent_base(string id, int expected)
    {
        var registry = NoirSkillRuleset.Load();

        var skill = registry[new SkillId(id)];

        Assert.Equal(Percent.Of(expected), skill.BaseChanceFor(Average()));
    }

    private static AbilitySet Average(int dexterity = 12)
    {
        var ruleset = NoirAbilityRuleset.Load();
        var values = ruleset.Characteristics.Keys.ToDictionary(id => id, _ => 12);
        values[new CharacteristicId("DEX")] = dexterity;
        return new AbilitySet(ruleset, values);
    }
}
