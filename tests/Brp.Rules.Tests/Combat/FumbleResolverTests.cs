using Brp.Core.Contests;
using Brp.Data;
using Brp.Rules.Combat;
using Brp.Rules.Gear;

namespace Brp.Rules.Tests.Combat;

/// <summary>
/// Ch 6: Combat, the four D100 fumble tables (pp.148-149). Covers table selection from the existing
/// combat context (weapon class + defense), boundary rolls landing on the right row, the "blow it"
/// (99) and "blow it badly" (00) cumulative reroll chain, and the hit-ally / weapon-hit-point
/// fallback branch driven by a deterministic <see cref="IFumbleAdjudicator"/> stub. See
/// <c>docs/decisions/0020-fumble-tables.md</c>.
/// </summary>
public class FumbleResolverTests
{
    private static readonly FumbleRuleset Ruleset = NoirFumbleRuleset.Load();

    private sealed class StubAdjudicator(bool allyInRange) : IFumbleAdjudicator
    {
        public bool IsAllyInRange() => allyInRange;
    }

    [Theory]
    // Ch 6 (pp.148-149): weapon class + defense select the table.
    [InlineData(WeaponClass.Dagger, DefenseType.None, FumbleTable.MeleeAttack)]
    [InlineData(WeaponClass.Club, DefenseType.None, FumbleTable.MeleeAttack)]
    [InlineData(WeaponClass.Dagger, DefenseType.Parry, FumbleTable.MeleeParry)]
    [InlineData(WeaponClass.Pistol, DefenseType.None, FumbleTable.MissileAttack)]
    [InlineData(WeaponClass.Revolver, DefenseType.None, FumbleTable.MissileAttack)]
    [InlineData(WeaponClass.Rifle, DefenseType.None, FumbleTable.MissileAttack)]
    [InlineData(WeaponClass.Shotgun, DefenseType.None, FumbleTable.MissileAttack)]
    [InlineData(WeaponClass.SubmachineGun, DefenseType.None, FumbleTable.MissileAttack)]
    [InlineData(WeaponClass.Missile, DefenseType.None, FumbleTable.MissileAttack)]
    [InlineData(WeaponClass.Brawl, DefenseType.None, FumbleTable.Natural)]
    [InlineData(WeaponClass.Brawl, DefenseType.Parry, FumbleTable.Natural)]
    public void Weapon_class_and_defense_select_the_table_pages_148_to_149(
        WeaponClass weaponClass, DefenseType defense, FumbleTable expected)
    {
        Assert.Equal(expected, FumbleResolver.SelectTable(weaponClass, defense));
    }

    [Fact]
    public void A_missile_weapon_cannot_parry_so_has_no_parry_table_page_148()
    {
        Assert.Throws<ArgumentException>(() => FumbleResolver.SelectTable(WeaponClass.Pistol, DefenseType.Parry));
    }

    [Fact]
    public void No_fumble_table_covers_a_dodge_pages_148_to_149()
    {
        Assert.Throws<ArgumentException>(() => FumbleResolver.SelectTable(WeaponClass.Dagger, DefenseType.Dodge));
    }

    [Theory]
    // Boundary rolls at each band edge of the Melee Weapon Attack table (Ch 6, p.148).
    [InlineData(1, FumbleEffectKind.LoseNextCombatRound)]
    [InlineData(15, FumbleEffectKind.LoseNextCombatRound)]
    [InlineData(16, FumbleEffectKind.LoseMultipleCombatRounds)]
    [InlineData(40, FumbleEffectKind.FallProne)]
    [InlineData(41, FumbleEffectKind.DropWeapon)]
    [InlineData(60, FumbleEffectKind.ThrowWeapon)]
    [InlineData(65, FumbleEffectKind.LoseWeaponHitPoints)]
    [InlineData(75, FumbleEffectKind.VisionObscured)]
    public void A_boundary_roll_maps_to_the_right_melee_attack_row_page_148(int roll, FumbleEffectKind expected)
    {
        var resolution = FumbleResolver.Resolve(
            FumbleTable.MeleeAttack, Ruleset, new FixedEntropySource(roll), new DefaultFumbleAdjudicator());

        Assert.Equal(FumbleTable.MeleeAttack, resolution.Table);
        var step = Assert.Single(resolution.Steps);
        Assert.Equal(roll, step.Roll);
        Assert.Equal(expected, step.Row.Kind);
    }

    [Fact]
    public void A_percentile_00_reads_as_100_and_lands_on_the_blow_it_badly_row_page_148()
    {
        var resolution = FumbleResolver.Resolve(
            FumbleTable.MeleeAttack, Ruleset, new FixedEntropySource(100, 26, 26, 26), new DefaultFumbleAdjudicator());

        // 00 -> "blow it badly": roll three more times (all 26 -> Fall prone).
        Assert.Equal(4, resolution.Steps.Count);
        Assert.Equal(FumbleEffectKind.Reroll, resolution.Steps[0].Row.Kind);
        Assert.Equal(3, resolution.Steps[0].Row.RerollCount);
        Assert.All(resolution.Steps.Skip(1), s => Assert.Equal(FumbleEffectKind.FallProne, s.Row.Kind));
    }

    [Fact]
    public void Blow_it_rolls_twice_more_on_the_same_table_page_148()
    {
        // 99 -> "blow it": two more rolls (26 Fall prone, 41 Drop weapon).
        var resolution = FumbleResolver.Resolve(
            FumbleTable.MeleeAttack, Ruleset, new FixedEntropySource(99, 26, 41), new DefaultFumbleAdjudicator());

        Assert.Equal(3, resolution.Steps.Count);
        Assert.Equal(FumbleEffectKind.Reroll, resolution.Steps[0].Row.Kind);
        Assert.Equal(2, resolution.Steps[0].Row.RerollCount);
        Assert.Equal(FumbleEffectKind.FallProne, resolution.Steps[1].Row.Kind);
        Assert.Equal(FumbleEffectKind.DropWeapon, resolution.Steps[2].Row.Kind);
    }

    [Fact]
    public void The_reroll_chain_is_cumulative_when_a_reroll_lands_on_99_again_page_148()
    {
        // 99 (blow it, +2) -> 99 again (blow it, +2 more) -> then three ordinary rolls.
        // Rolls consumed: 99, 99, 26, 26, 26 = 5 steps; two rolls still owed after the first 99 are
        // one-more-plus-the-second-99's-two = the second 99 adds to the outstanding count cumulatively.
        var resolution = FumbleResolver.Resolve(
            FumbleTable.MeleeAttack, Ruleset, new FixedEntropySource(99, 99, 26, 26, 26), new DefaultFumbleAdjudicator());

        Assert.Equal(5, resolution.Steps.Count);
        Assert.Equal(FumbleEffectKind.Reroll, resolution.Steps[0].Row.Kind);
        Assert.Equal(FumbleEffectKind.Reroll, resolution.Steps[1].Row.Kind);
        Assert.Equal(3, resolution.Steps.Count(s => s.Row.Kind == FumbleEffectKind.FallProne));
    }

    [Fact]
    public void With_an_ally_in_range_the_hit_ally_primary_applies_page_148()
    {
        // Roll 76 -> "Hit nearest ally for normal damage, or use result 41-50".
        var resolution = FumbleResolver.Resolve(
            FumbleTable.MeleeAttack, Ruleset, new FixedEntropySource(76), new StubAdjudicator(allyInRange: true));

        var step = Assert.Single(resolution.Steps);
        Assert.Equal(FumbleEffectKind.HitNearestAlly, step.Row.Kind);
        Assert.Equal(LandedGrade.Normal, step.Row.HitGrade);

        Assert.NotNull(step.Branch);
        Assert.Equal(FumbleFallbackCondition.NoAllyNearby, step.Branch!.Condition);
        Assert.True(step.Branch.PrimaryApplies);
        // The fallback (41-50, Drop weapon) is still named for transparency.
        Assert.Equal(FumbleEffectKind.DropWeapon, step.Branch.FallbackRow.Kind);
    }

    [Fact]
    public void With_no_ally_in_range_the_printed_fallback_result_applies_page_148()
    {
        // Roll 76 -> no ally -> use result 41-50 (Drop weapon).
        var resolution = FumbleResolver.Resolve(
            FumbleTable.MeleeAttack, Ruleset, new FixedEntropySource(76), new StubAdjudicator(allyInRange: false));

        var step = Assert.Single(resolution.Steps);
        Assert.NotNull(step.Branch);
        Assert.False(step.Branch!.PrimaryApplies);
        Assert.Equal(41, step.Branch.FallbackRow.MinimumRoll);
        Assert.Equal(50, step.Branch.FallbackRow.MaximumRoll);
        Assert.Equal(FumbleEffectKind.DropWeapon, step.Branch.FallbackRow.Kind);
    }

    [Fact]
    public void Resolving_the_ally_fallback_consumes_no_extra_entropy_page_148()
    {
        // Only the single D100 for the row itself is drawn; "use result 41-50" is a lookup, not a reroll.
        var entropy = new FixedEntropySource(76);

        FumbleResolver.Resolve(FumbleTable.MeleeAttack, Ruleset, entropy, new StubAdjudicator(allyInRange: false));

        Assert.Equal(1, entropy.DrawCount);
    }

    [Fact]
    public void The_weapon_hit_point_fallback_is_left_to_the_caller_not_an_adjudicator_page_148()
    {
        // Missile roll 66 -> "Do 1D6 damage to weapon's hit points (or use 81-85 if no hit points)".
        var resolution = FumbleResolver.Resolve(
            FumbleTable.MissileAttack, Ruleset, new FixedEntropySource(66), new DefaultFumbleAdjudicator());

        var step = Assert.Single(resolution.Steps);
        Assert.Equal(FumbleEffectKind.LoseWeaponHitPoints, step.Row.Kind);
        Assert.NotNull(step.Branch);
        Assert.Equal(FumbleFallbackCondition.WeaponHasNoHitPoints, step.Branch!.Condition);
        // Null: this layer does not model weapon hit points, so the caller decides between branches.
        Assert.Null(step.Branch.PrimaryApplies);
        Assert.Equal(FumbleEffectKind.BreakWeapon, step.Branch.FallbackRow.Kind);
    }

    [Fact]
    public void The_convenience_overload_selects_the_table_from_the_combat_context_page_148()
    {
        // A brawler (natural weapon) fumbling: roll 95 -> "Hit hard surface; normal damage to self".
        var resolution = FumbleResolver.Resolve(
            WeaponClass.Brawl, DefenseType.None, Ruleset, new FixedEntropySource(95), new DefaultFumbleAdjudicator());

        Assert.Equal(FumbleTable.Natural, resolution.Table);
        Assert.Equal(FumbleEffectKind.HitHardSurface, Assert.Single(resolution.Steps).Row.Kind);
    }

    [Fact]
    public void The_same_seed_produces_the_identical_step_chain_invariant_5()
    {
        var first = FumbleResolver.Resolve(
            FumbleTable.MissileAttack, Ruleset, new FixedEntropySource(99, 96, 66), new StubAdjudicator(true));
        var second = FumbleResolver.Resolve(
            FumbleTable.MissileAttack, Ruleset, new FixedEntropySource(99, 96, 66), new StubAdjudicator(true));

        Assert.Equal(
            first.Steps.Select(s => (s.Roll, s.Row.Kind)),
            second.Steps.Select(s => (s.Roll, s.Row.Kind)));
    }
}
