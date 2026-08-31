using Brp.Core.Abilities;
using Brp.Core.Skills;
using Brp.Data;
using Brp.Rules.Characters;
using Brp.Rules.Creation;

namespace Brp.Rules.Tests.Characters;

/// <summary>
/// Confirms the ADR 0006 policy is actually applied to a character in the engine (ADR 0022):
/// a player-built character's effective skill rating is base + category bonus, it recomputes
/// when a characteristic changes, and it is NOT double-applied to an authored, already-final
/// effective rating (which stores its base by subtraction). Before #110 the C# builder set every
/// skill to base + points with no bonus term, so player-built characters silently lacked their
/// category bonuses; these tests are the regression guard.
/// </summary>
public class SkillCategoryBonusApplicationTests
{
    private static readonly CharacterCreationRuleset CreationRuleset = NoirCharacterCreationRuleset.Load();
    private static readonly AbilityRuleset AbilityRuleset = NoirAbilityRuleset.Load();
    private static readonly SkillRegistry SkillRegistry = NoirSkillRuleset.Load();
    private static readonly SkillCategoryBonusRuleset Bonuses = NoirSkillCategoryBonusRuleset.Load();

    private static readonly SkillId Brawl = new("Brawl"); // Combat: primary DEX, secondary INT+STR.

    private static Dictionary<CharacteristicId, int> Deltas(params (string Id, int Delta)[] overrides)
    {
        var deltas = CreationRuleset.CharacteristicCosts.Keys.ToDictionary(id => id, _ => 0);
        foreach (var (id, delta) in overrides)
        {
            deltas[new CharacteristicId(id)] = delta;
        }

        return deltas;
    }

    private static Character Build(params (string Id, int Delta)[] characteristicDeltas) =>
        new CharacterBuilder(CreationRuleset, AbilityRuleset, SkillRegistry).Build(new CharacterCreationRequest
        {
            Id = new CharacterId("pc"),
            Name = "Test Subject",
            CharacteristicDeltas = Deltas(characteristicDeltas),
        });

    [Fact]
    public void A_player_built_characters_effective_rating_is_base_rating_plus_its_category_bonus()
    {
        // DEX 15 (delta +5, costs 15 of 24) -> Combat category bonus +5 (INT/STR neutral).
        var character = Build(("DEX", 5));

        var brawl = character.Skill(Brawl);
        Assert.Equal(25, brawl.CurrentRating);                 // base = printed 25% + 0 points
        Assert.Equal(5, character.CategoryBonus(Brawl, Bonuses));
        Assert.Equal(30, character.EffectiveRating(Brawl, Bonuses)); // base + bonus, the ADR 0006 rating
    }

    [Fact]
    public void With_neutral_characteristics_the_bonus_is_zero_so_effective_equals_base()
    {
        var character = Build(); // every characteristic at 10

        Assert.Equal(0, character.CategoryBonus(Brawl, Bonuses));
        Assert.Equal(character.Skill(Brawl).CurrentRating, character.EffectiveRating(Brawl, Bonuses));
    }

    [Fact]
    public void The_effective_rating_recomputes_when_a_characteristic_is_drained()
    {
        var character = Build(("DEX", 5)); // DEX 15 -> Combat +5
        Assert.Equal(30, character.EffectiveRating(Brawl, Bonuses));

        // Poison/injury drains DEX to 8. Ch 2 p.13 requires derived values to move immediately;
        // ADR 0006 puts the category bonus on the same footing.
        character.Abilities.Set(new CharacteristicId("DEX"), 8); // -> Combat -2

        Assert.Equal(25, character.Skill(Brawl).CurrentRating);       // base rating is untouched
        Assert.Equal(-2, character.CategoryBonus(Brawl, Bonuses));
        Assert.Equal(23, character.EffectiveRating(Brawl, Bonuses));  // recomputed live
    }

    [Fact]
    public void An_authored_final_effective_rating_is_reproduced_exactly_and_never_double_bonused()
    {
        // Authored content (per ADR 0006 / tools/skill_bonus.py) treats its numbers as FINAL
        // effective ratings and stores the base by subtraction: base = effective - bonus. A
        // character with DEX 15 has a +5 Combat bonus, so an authored Brawl 60 stores base 55.
        var abilities = new AbilitySet(
            AbilityRuleset,
            AbilityRuleset.Characteristics.Keys.ToDictionary(
                id => id, id => id.Value == "DEX" ? 15 : 10));
        var definition = SkillRegistry[Brawl];

        var authored = CharacterSkill.FromEffectiveRating(definition, effectiveRating: 60, abilities, Bonuses);

        Assert.Equal(55, authored.CurrentRating);                     // base was derived by subtraction
        Assert.Equal(60, authored.EffectiveRating(abilities, Bonuses)); // reproduces the authored 60 exactly

        // The trap the subtraction avoids: had the authored 60 been stored as a base rating, the
        // engine would add the +5 bonus a second time and inflate it to 65.
        var doubleApplied = new CharacterSkill(definition, currentRating: 60);
        Assert.Equal(65, doubleApplied.EffectiveRating(abilities, Bonuses));
        Assert.NotEqual(authored.EffectiveRating(abilities, Bonuses), doubleApplied.EffectiveRating(abilities, Bonuses));
    }
}
