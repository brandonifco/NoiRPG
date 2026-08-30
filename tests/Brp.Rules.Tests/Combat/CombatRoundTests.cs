using Brp.Rules.Combat;

namespace Brp.Rules.Tests.Combat;

/// <summary>
/// Covers <see cref="CombatRound"/>'s phase structure and Action-phase ordering against Ch 6:
/// Combat, "Combat Round Phases" (p.142) and "Action" (p.143). See
/// <c>docs/decisions/0015-combat-round.md</c>.
/// </summary>
public class CombatRoundTests
{
    private static readonly CombatRoundRuleset Ruleset = new(
        phases: [CombatRoundPhase.Statements, CombatRoundPhase.Action, CombatRoundPhase.Resolution],
        dexRankSourceCharacteristic: "DEX",
        dexRankOrderedDescending: true,
        weaponTypeTiebreakOrder: [WeaponTypeTier.Missile, WeaponTypeTier.LongWeapon, WeaponTypeTier.MediumWeapon, WeaponTypeTier.ShortOrUnarmed],
        movementTiers: [new MovementTier(6, 15, 1, 2), new MovementTier(16, 29, 1, 4)],
        drawWeaponDexRankPenalty: 5,
        multipleActionDexRankPenalty: 5,
        dexRankFloor: 0);

    // ---- Phase sequence: three phases, Powers omitted -----------------------------------------

    [Fact]
    public void A_round_has_the_three_phases_in_book_order_with_powers_omitted()
    {
        var round = CombatRound.Create([], Ruleset);

        Assert.Equal(
            [CombatRoundPhase.Statements, CombatRoundPhase.Action, CombatRoundPhase.Resolution],
            round.Phases);
    }

    // ---- DEX-rank ordering, high to low ---------------------------------------------------------

    [Fact]
    public void Combatants_are_ordered_by_effective_DEX_rank_high_to_low()
    {
        var requests = new[]
        {
            new CombatActionRequest("low-dex", 8, 0, 0, WeaponTypeTier.MediumWeapon),
            new CombatActionRequest("high-dex", 17, 0, 0, WeaponTypeTier.MediumWeapon),
            new CombatActionRequest("mid-dex", 12, 0, 0, WeaponTypeTier.MediumWeapon),
        };

        var round = CombatRound.Create(requests, Ruleset);

        Assert.Equal(["high-dex", "mid-dex", "low-dex"], round.ActionPhaseOrder.Select(t => t.CombatantId));
    }

    // ---- Weapon-type tiebreak on an exact DEX-rank tie ------------------------------------------

    [Fact]
    public void A_tied_DEX_rank_is_broken_by_weapon_type_missile_before_medium()
    {
        // Ch 6, p.143: "Attackers armed with missile weapons ... are considered to act before
        // those in hand-to-hand (melee) combat," e.g. a gunslinger and a swordsman at equal DEX.
        var requests = new[]
        {
            new CombatActionRequest("swordsman", 13, 0, 0, WeaponTypeTier.MediumWeapon),
            new CombatActionRequest("gunslinger", 13, 0, 0, WeaponTypeTier.Missile),
        };

        var round = CombatRound.Create(requests, Ruleset);

        Assert.Equal(["gunslinger", "swordsman"], round.ActionPhaseOrder.Select(t => t.CombatantId));
    }

    [Fact]
    public void The_full_weapon_type_tiebreak_order_is_missile_long_medium_short()
    {
        var requests = new[]
        {
            new CombatActionRequest("short", 10, 0, 0, WeaponTypeTier.ShortOrUnarmed),
            new CombatActionRequest("medium", 10, 0, 0, WeaponTypeTier.MediumWeapon),
            new CombatActionRequest("long", 10, 0, 0, WeaponTypeTier.LongWeapon),
            new CombatActionRequest("missile", 10, 0, 0, WeaponTypeTier.Missile),
        };

        var round = CombatRound.Create(requests, Ruleset);

        Assert.Equal(["missile", "long", "medium", "short"], round.ActionPhaseOrder.Select(t => t.CombatantId));
    }

    // ---- Movement tiers, applied before penalties ------------------------------------------------

    [Theory]
    [InlineData(0, 16)]  // no movement: unmodified
    [InlineData(10, 8)]  // 6-15m: half of 16 (exact, so rounding direction doesn't discriminate here)
    [InlineData(20, 4)]  // 16-29m: quarter of 16 (also exact)
    public void Movement_fractions_the_effective_DEX_rank_before_any_flat_penalty(int movementMeters, int expectedRank)
    {
        var requests = new[] { new CombatActionRequest("mover", 16, movementMeters, 0, WeaponTypeTier.ShortOrUnarmed) };

        var round = CombatRound.Create(requests, Ruleset);

        Assert.Equal(expectedRank, Assert.Single(round.ActionPhaseOrder).EffectiveDexRank);
    }

    [Fact]
    public void Movement_fraction_rounds_up_per_Ch5_p140s_half_of_X_convention()
    {
        // Ch 5: System, "Characteristic Increases" (p.140): "1/2 of 13 rounds up to 7" -- the
        // book's only explicit rounding rule for this fraction shape. DEX rank 15 moving 6-15m ->
        // ceil(15/2) = 8, not the floor(15/2) = 7 an earlier draft used.
        var requests = new[] { new CombatActionRequest("mover", 15, 10, 0, WeaponTypeTier.ShortOrUnarmed) };

        var round = CombatRound.Create(requests, Ruleset);

        Assert.Equal(8, Assert.Single(round.ActionPhaseOrder).EffectiveDexRank);
    }

    [Fact]
    public void A_DEX_rank_one_combatant_moving_6_to_15_meters_still_acts()
    {
        // Rounding up avoids the pathological truncate-to-zero case: a rank-1 combatant walking
        // 6-15m stays at rank 1 (ceil(1/2) = 1) and is present in the action order, rather than
        // being dropped for taking an ordinary walk.
        var requests = new[] { new CombatActionRequest("walker", 1, 10, 0, WeaponTypeTier.ShortOrUnarmed) };

        var round = CombatRound.Create(requests, Ruleset);

        var turn = Assert.Single(round.ActionPhaseOrder);
        Assert.Equal("walker", turn.CombatantId);
        Assert.Equal(1, turn.EffectiveDexRank);
    }

    [Fact]
    public void Movement_and_a_flat_penalty_are_cumulative_with_movement_applied_first()
    {
        // Ch 6, p.144: "These modified DEX ranks are cumulative with penalties for additional
        // actions, with movement modifiers to DEX rank being applied first." DEX 16, moving 10m
        // (half rank -> 8), then drawing a weapon combined with an attack (-5) -> 3.
        var requests = new[] { new CombatActionRequest("mover", 16, 10, 5, WeaponTypeTier.ShortOrUnarmed) };

        var round = CombatRound.Create(requests, Ruleset);

        Assert.Equal(3, Assert.Single(round.ActionPhaseOrder).EffectiveDexRank);
    }

    // ---- Drawing a weapon: -5 DEX ranks -----------------------------------------------------------

    [Fact]
    public void Drawing_a_weapon_combined_with_another_action_costs_five_DEX_ranks()
    {
        var requests = new[]
        {
            new CombatActionRequest("draws", 15, 0, Ruleset.DrawWeaponDexRankPenalty, WeaponTypeTier.ShortOrUnarmed),
        };

        var round = CombatRound.Create(requests, Ruleset);

        Assert.Equal(10, Assert.Single(round.ActionPhaseOrder).EffectiveDexRank);
    }

    // ---- The DEX-rank-0 floor: actions below rank 1 are lost --------------------------------------

    [Fact]
    public void An_action_pushed_below_DEX_rank_one_is_lost_not_ordered_last()
    {
        var requests = new[]
        {
            new CombatActionRequest("survives", 6, 0, 0, WeaponTypeTier.ShortOrUnarmed),
            new CombatActionRequest("lost", 4, 0, 5, WeaponTypeTier.ShortOrUnarmed), // 4 - 5 = -1, below the floor
        };

        var round = CombatRound.Create(requests, Ruleset);

        var turn = Assert.Single(round.ActionPhaseOrder);
        Assert.Equal("survives", turn.CombatantId);
    }

    [Fact]
    public void An_action_landing_exactly_on_DEX_rank_zero_is_also_lost()
    {
        var requests = new[] { new CombatActionRequest("at-zero", 5, 0, 5, WeaponTypeTier.ShortOrUnarmed) };

        var round = CombatRound.Create(requests, Ruleset);

        Assert.Empty(round.ActionPhaseOrder);
    }

    // ---- Scope: the optional Initiative Rolls variant is absent ------------------------------------

    [Fact]
    public void The_optional_initiative_rolls_variant_is_not_implemented()
    {
        // Ch 6, "Initiative Rolls (Option)" (p.143): D10 + DEX (or D10 + INT for powers), cut per
        // orc-scope-filter.md's OFF-list. CombatRound exposes no D10/initiative concept anywhere
        // in its public surface -- only the default DEX-rank system.
        var members = typeof(CombatRound).GetMembers()
            .Concat(typeof(CombatRoundRuleset).GetMembers())
            .Concat(typeof(CombatActionRequest).GetMembers())
            .Concat(typeof(CombatantTurn).GetMembers())
            .Select(m => m.Name);

        Assert.DoesNotContain(members, name =>
            name.Contains("Initiative", StringComparison.OrdinalIgnoreCase)
            || name.Contains("D10", StringComparison.OrdinalIgnoreCase));
    }
}
