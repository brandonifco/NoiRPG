using Brp.Core.Abilities;
using Brp.Data;
using Brp.Rules.Characters;
using Brp.Rules.Combat;

namespace Brp.Rules.Tests.Combat;

/// <summary>
/// The non-weapon <see cref="DamageResolver.ApplyDamage(AbilitySet, WoundTrack, int, DamageRuleset, string)"/>
/// overload the injury spot rules (#96) use for falling and poison. Mirrors the weapon overload's
/// hit-point tracking (Ch 2, p.13) and condition classification (Ch 2, p.13; Ch 6, p.156) without a
/// fabricated <see cref="DamageRoll"/>. See <c>docs/decisions/0019-injury-spot-rules.md</c>.
/// </summary>
public class DamageResolverInjuryOverloadTests
{
    private static readonly DamageRuleset Ruleset = NoirDamageRuleset.Load();

    private static AbilitySet MakeTarget(int con, int siz)
    {
        var ruleset = NoirAbilityRuleset.Load();
        var values = ruleset.Characteristics.Keys.ToDictionary(id => id, _ => 12);
        values[new CharacteristicId("CON")] = con;
        values[new CharacteristicId("SIZ")] = siz;
        return new AbilitySet(ruleset, values);
    }

    [Fact]
    public void Applies_flat_damage_records_a_wound_and_reports_unaffected()
    {
        var target = MakeTarget(con: 14, siz: 12); // max HP 13.
        var wounds = new WoundTrack();

        var result = DamageResolver.ApplyDamage(target, wounds, hitPointDamage: 5, Ruleset, "Falling");

        Assert.Equal(5, result.DamageDealt);
        Assert.Equal(8, result.ResultingHitPoints);
        Assert.Equal(HitPointCondition.Unaffected, result.Condition);
        Assert.Single(wounds.Wounds);
    }

    [Fact]
    public void Damage_to_two_or_fewer_hit_points_reports_unconscious()
    {
        var target = MakeTarget(con: 14, siz: 12); // max HP 13.
        var wounds = new WoundTrack();

        var result = DamageResolver.ApplyDamage(target, wounds, hitPointDamage: 12, Ruleset, "Poison");

        Assert.Equal(1, result.ResultingHitPoints);
        Assert.Equal(HitPointCondition.Unconscious, result.Condition);
    }

    [Fact]
    public void Damage_to_zero_or_below_reports_fatally_wounded_and_allows_negative_hit_points()
    {
        var target = MakeTarget(con: 14, siz: 12); // max HP 13.
        var wounds = new WoundTrack();

        var result = DamageResolver.ApplyDamage(target, wounds, hitPointDamage: 20, Ruleset, "Falling");

        Assert.Equal(-7, result.ResultingHitPoints);
        Assert.Equal(HitPointCondition.FatallyWounded, result.Condition);
    }

    [Fact]
    public void Negative_damage_is_rejected()
    {
        var target = MakeTarget(con: 14, siz: 12);
        var wounds = new WoundTrack();

        Assert.Throws<ArgumentOutOfRangeException>(
            () => DamageResolver.ApplyDamage(target, wounds, hitPointDamage: -1, Ruleset, "Falling"));
    }
}
