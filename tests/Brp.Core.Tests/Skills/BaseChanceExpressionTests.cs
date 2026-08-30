using Brp.Core.Abilities;
using Brp.Core.Primitives;
using Brp.Core.Skills;
using Brp.Data;

namespace Brp.Core.Tests.Skills;

/// <summary>
/// Covers the four base-chance shapes named in engine-implementation-plan.md §3 and
/// verified against Ch 3: Skills, transcribed per skill below.
/// </summary>
public class BaseChanceExpressionTests
{
    [Fact]
    public void A_constant_base_chance_ignores_abilities()
    {
        // Ch 3: Skills, "Spot" (p.50), "Base Chance: 25%".
        var spot = new ConstantBaseChance(Percent.Of(25));

        Assert.Equal(Percent.Of(25), spot.Evaluate(Create()));
        Assert.Equal(Percent.Of(25), spot.Evaluate(Create(dexterity: 3)));
    }

    [Fact]
    public void A_characteristic_formula_multiplies_a_single_term()
    {
        // Ch 3: Skills, "Dodge" (p.37), "Base Chance: DEX×2".
        var dodge = new CharacteristicFormulaBaseChance(new CharacteristicTerm(new CharacteristicId("DEX"), 2));

        Assert.Equal(Percent.Of(30), dodge.Evaluate(Create(dexterity: 15)));
        Assert.Equal(Percent.Of(6), dodge.Evaluate(Create(dexterity: 3)));
    }

    [Fact]
    public void A_characteristic_formula_sums_multiple_terms()
    {
        // Ch 3: Skills, "Gaming" (p.40), "Base Chance: INT+POW%".
        var gaming = new CharacteristicFormulaBaseChance(
            new CharacteristicTerm(new CharacteristicId("INT"), 1),
            new CharacteristicTerm(new CharacteristicId("POW"), 1));

        Assert.Equal(Percent.Of(25), gaming.Evaluate(Create(intelligence: 14, power: 11)));
    }

    [Fact]
    public void A_characteristic_formula_reproduces_language_own_at_int_times_5()
    {
        // Ch 3: Skills, "Language (various)" (p.44): "Most characters begin knowing their own
        // language at INT×5."
        var languageOwn = new CharacteristicFormulaBaseChance(new CharacteristicTerm(new CharacteristicId("INT"), 5));

        Assert.Equal(Percent.Of(70), languageOwn.Evaluate(Create(intelligence: 14)));
    }

    [Fact]
    public void An_era_conditional_base_chance_always_evaluates_to_the_modern_value()
    {
        // Ch 3: Skills, "Drive (various)" (p.37): "Base Chance: 20% or 01%... For common
        // vehicles, the base chance is 20%, for unknown/uncommon vehicles, it's 01%."
        // AGENTS.md invariant 4: NoiRPG always takes the modern value -- common vehicles
        // (cars) are the modern case, so this must evaluate to 20, never 1.
        var drive = new EraConditionalBaseChance(
            Modern: new ConstantBaseChance(Percent.Of(20)),
            Historical: new ConstantBaseChance(Percent.Of(1)));

        var result = drive.Evaluate(Create());

        Assert.Equal(Percent.Of(20), result);
        Assert.NotEqual(Percent.Of(1), result);
    }

    [Fact]
    public void An_era_conditional_base_chance_can_nest_a_formula_as_either_side()
    {
        var expression = new EraConditionalBaseChance(
            Modern: new ConstantBaseChance(Percent.Of(30)),
            Historical: new CharacteristicFormulaBaseChance(new CharacteristicTerm(new CharacteristicId("INT"), 1)));

        Assert.Equal(Percent.Of(30), expression.Evaluate(Create(intelligence: 99)));
    }

    [Fact]
    public void A_weapon_derived_base_chance_cannot_evaluate_on_its_own()
    {
        // Ch 3: Skills, "Firearm (various)" (p.39), "Base Chance: As per weapon specialty".
        // The value lives on weapon data, which is Layer 4 (#21) and out of scope here.
        var firearm = new WeaponDerivedBaseChance();

        Assert.Throws<InvalidOperationException>(() => firearm.Evaluate(Create()));
    }

    // ---- TryEvaluateWithoutAbilities: the abilities-less path used by brp roll (#38) --------

    [Fact]
    public void A_constant_base_chance_evaluates_without_abilities()
    {
        var spot = new ConstantBaseChance(Percent.Of(25));

        Assert.True(spot.TryEvaluateWithoutAbilities(out var value));
        Assert.Equal(Percent.Of(25), value);
    }

    [Fact]
    public void An_era_conditional_over_constants_evaluates_without_abilities_to_its_modern_value()
    {
        // Drive: modern 20% / historical 01% (Ch 3, p.37). Both sides constant, so the modern
        // value resolves with no character sheet -- which is what lets `brp roll --skill-name`
        // price it.
        var drive = new EraConditionalBaseChance(
            new ConstantBaseChance(Percent.Of(20)), new ConstantBaseChance(Percent.Of(1)));

        Assert.True(drive.TryEvaluateWithoutAbilities(out var value));
        Assert.Equal(Percent.Of(20), value);
    }

    [Fact]
    public void A_characteristic_formula_cannot_evaluate_without_abilities()
    {
        // Dodge DEX×2 (Ch 3, p.37): needs a characteristic, so an abilities-less caller must be
        // told it cannot, not handed a wrong number.
        var dodge = new CharacteristicFormulaBaseChance(new CharacteristicTerm(new CharacteristicId("DEX"), 2));

        Assert.False(dodge.TryEvaluateWithoutAbilities(out var value));
        Assert.Equal(Percent.Zero, value);
    }

    [Fact]
    public void A_weapon_derived_base_chance_cannot_evaluate_without_abilities()
    {
        var firearm = new WeaponDerivedBaseChance();

        Assert.False(firearm.TryEvaluateWithoutAbilities(out _));
    }

    private static AbilitySet Create(int dexterity = 12, int intelligence = 12, int power = 12)
    {
        var ruleset = NoirAbilityRuleset.Load();
        var values = ruleset.Characteristics.Keys.ToDictionary(id => id, _ => 12);
        values[new CharacteristicId("DEX")] = dexterity;
        values[new CharacteristicId("INT")] = intelligence;
        values[new CharacteristicId("POW")] = power;
        return new AbilitySet(ruleset, values);
    }
}
