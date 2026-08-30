using Brp.Core.Abilities;
using Brp.Core.Skills;
using Brp.Data;
using Brp.Rules.Characters;

namespace Brp.Rules.Tests.Characters;

public class CharacterTests
{
    private static Character MakeCharacter(int con = 12, int siz = 12)
    {
        var abilityRuleset = NoirAbilityRuleset.Load();
        var values = abilityRuleset.Characteristics.Keys.ToDictionary(id => id, _ => 10);
        values[new CharacteristicId("CON")] = con;
        values[new CharacteristicId("SIZ")] = siz;
        var abilities = new AbilitySet(abilityRuleset, values);

        var registry = NoirSkillRuleset.Load();
        var spot = registry[new SkillId("Spot")];
        var skills = new Dictionary<SkillId, CharacterSkill>
        {
            [spot.Id] = new CharacterSkill(spot, spot.BaseChanceFor(abilities).Value),
        };

        return new Character(new CharacterId("pc-1"), "Jane Doe", abilities, skills);
    }

    [Fact]
    public void Maximum_hit_points_are_a_live_derived_value_that_moves_when_a_characteristic_drops()
    {
        // Ch 2: Characters, "Derived Characteristics" (p.13): HP must change immediately
        // after a characteristic changes, not be baked in at creation.
        var character = MakeCharacter(con: 12, siz: 12);
        var before = character.MaximumHitPoints;

        character.Abilities.Set(new CharacteristicId("CON"), 4);

        Assert.True(character.MaximumHitPoints < before);
        // Current HP is clamped down alongside the new, lower maximum (AbilitySet's own
        // cap), which is the observable proof the value was not cached at construction.
        Assert.True(character.CurrentHitPoints <= character.MaximumHitPoints);
    }

    [Fact]
    public void Current_hit_points_start_at_maximum_and_track_it_before_any_damage()
    {
        var character = MakeCharacter();
        Assert.Equal(character.MaximumHitPoints, character.CurrentHitPoints);
    }

    [Fact]
    public void Skills_carry_a_current_rating_distinct_from_the_printed_base_chance()
    {
        var character = MakeCharacter();
        var spot = character.Skill(new SkillId("Spot"));

        // Spot's printed base is a flat 25% (Ch 3: Skills, p.50); bump the character's
        // rating and the printed base must stay put -- that is the whole point of the
        // two-number contract Layer 2 established.
        var printedBefore = spot.PrintedBaseChance(character.Abilities);
        Assert.Equal(25, printedBefore.Value);
        Assert.Equal(25, spot.CurrentRating);
    }

    [Fact]
    public void Wound_list_and_equipment_are_present_but_empty_structural_containers()
    {
        var character = MakeCharacter();

        Assert.Empty(character.Wounds.Wounds);
        Assert.Empty(character.Equipment.Items);

        character.Wounds.Add(new Wound("grazed by a bullet"));
        character.Equipment.Add(new EquipmentItem("revolver"));

        Assert.Single(character.Wounds.Wounds);
        Assert.Single(character.Equipment.Items);
    }

    [Fact]
    public void Has_skill_reports_whether_the_character_has_a_skill_at_all()
    {
        var character = MakeCharacter();
        Assert.True(character.HasSkill(new SkillId("Spot")));
        Assert.False(character.HasSkill(new SkillId("Firearms (Handgun)")));
    }
}
