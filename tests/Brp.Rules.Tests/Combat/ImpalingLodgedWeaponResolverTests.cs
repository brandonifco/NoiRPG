using Brp.Core.Dice;
using Brp.Core.Primitives;
using Brp.Core.Skills;
using Brp.Data;
using Brp.Rules.Combat;
using Brp.Rules.Gear;

namespace Brp.Rules.Tests.Combat;

/// <summary>
/// Ch 6: Combat, "Impaling" (pp.149-150)'s lodged-weapon effect -- deferred from #52
/// (<c>docs/decisions/0017-damage.md</c>) and built here for #113.
/// </summary>
public class ImpalingLodgedWeaponResolverTests
{
    private static readonly SpecialDamageEffectsRuleset Ruleset = NoirSpecialDamageEffectsRuleset.Load();

    private static WeaponDefinition MakeWeapon(string damage) => new(
        Id: new WeaponId("test-knife"),
        Name: "Test Knife",
        SkillId: new SkillId("Melee Weapon"),
        WeaponClass: WeaponClass.Dagger,
        Damage: DiceExpression.Parse(damage),
        ApplyDamageBonus: false,
        DamageByRange: [],
        Firearm: null,
        SpecialDamageType: SpecialDamageType.Impaling,
        Source: "Test fixture, not from the book.");

    [Fact]
    public void An_immediate_extraction_attempt_is_difficult_page_150()
    {
        // 60% rating, Difficult halves to 30%; roll 25 succeeds only under the halved chance.
        var outcome = ImpalingLodgedWeaponResolver.AttemptImmediateExtraction(
            Percent.Of(60), Percent.Of(0), new FixedEntropySource(25));

        Assert.True(outcome.Succeeded);
    }

    [Fact]
    public void A_focused_extraction_attempt_uses_the_full_undifficultied_chance_page_150()
    {
        // 60% rating, no Difficult halving; roll 50 succeeds (would fail if halved to 30%).
        var outcome = ImpalingLodgedWeaponResolver.AttemptFocusedExtraction(
            Percent.Of(60), Percent.Of(0), new FixedEntropySource(50));

        Assert.True(outcome.Succeeded);
    }

    [Fact]
    public void The_focused_extraction_consequences_match_the_printed_rule_page_150()
    {
        var consequences = ImpalingLodgedWeaponResolver.FocusedConsequences;

        Assert.True(consequences.AttacksAgainstAttackerAreEasy);
        Assert.True(consequences.AttackerCannotParryOrDodge);
    }

    [Fact]
    public void A_successful_self_extraction_frees_the_weapon_with_no_extra_damage_page_150()
    {
        // STR 15 vs damage 5: chance 50+5*(15-5)=100 -> capped/near-automatic success; roll 50 succeeds.
        var outcome = ImpalingLodgedWeaponResolver.AttemptSelfExtraction(
            targetStrength: 15, cumulativeDamageDealt: 5, Ruleset, new FixedEntropySource(50));

        Assert.True(outcome.Freed);
        Assert.Equal(0, outcome.ExtraDamage);
        Assert.Null(outcome.ExtraDamageRoll);
    }

    [Fact]
    public void A_failed_self_extraction_deals_the_printed_1D3_extra_damage_page_150()
    {
        // STR 5 vs damage 15: chance 50+5*(5-15) = 0 -> automatic failure; extra damage roll 2.
        var outcome = ImpalingLodgedWeaponResolver.AttemptSelfExtraction(
            targetStrength: 5, cumulativeDamageDealt: 15, Ruleset, new FixedEntropySource(50, 2));

        Assert.False(outcome.Freed);
        Assert.Equal(2, outcome.ExtraDamage);
        Assert.NotNull(outcome.ExtraDamageRoll);
    }

    [Fact]
    public void Movement_damage_is_half_the_fresh_weapon_roll_rounded_up_page_150()
    {
        // Fresh 1D8 roll of 7 -> half rounded up = 4.
        var weapon = MakeWeapon("1D8");
        var movement = ImpalingLodgedWeaponResolver.RollMovementDamage(weapon, new FixedEntropySource(7));

        Assert.Equal(7, movement.FreshRoll.Total);
        Assert.Equal(4, movement.HalvedDamage);
    }

    [Fact]
    public void Movement_damage_halving_of_an_even_roll_needs_no_rounding_page_150()
    {
        var weapon = MakeWeapon("1D8");
        var movement = ImpalingLodgedWeaponResolver.RollMovementDamage(weapon, new FixedEntropySource(6));

        Assert.Equal(3, movement.HalvedDamage);
    }
}
