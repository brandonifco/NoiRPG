using Brp.Rules.Combat;

namespace Brp.Data.Tests;

/// <summary>
/// Confirms the shipped fumble ruleset data loads and reproduces Ch 6: Combat's four D100 fumble
/// tables -- "Melee Weapon Attack Fumbles", "Melee Weapon Parry Fumbles", "Missile Weapon Attack
/// Fumbles" (p.148), and "Natural Weapon Attack and Parry Fumbles" (p.149) -- <strong>row by
/// row</strong> rather than sampled, per AGENTS.md's "table-backed rule" convention. Each table has
/// an exact-count fact so a dropped or added row fails loudly. See
/// <c>docs/decisions/0020-fumble-tables.md</c>.
/// </summary>
public class NoirFumbleRulesetTests
{
    private static readonly FumbleRuleset Ruleset = NoirFumbleRuleset.Load();

    // Each row: table, minRoll, maxRoll, kind, amount (or null), magnitude (or null),
    // hitGrade (or null), fallback condition (or null), fallback min (or -1), fallback max (or -1),
    // rerollCount (or -1). Transcribed from the cited pages.

    public static IEnumerable<object?[]> MeleeAttackRows()
    {
        // Ch 6, "Melee Weapon Attack Fumbles" table (p.148) -- 12 rows.
        yield return Row(FumbleTable.MeleeAttack, 1, 15, FumbleEffectKind.LoseNextCombatRound);
        yield return Row(FumbleTable.MeleeAttack, 16, 25, FumbleEffectKind.LoseMultipleCombatRounds, amount: "1D3");
        yield return Row(FumbleTable.MeleeAttack, 26, 40, FumbleEffectKind.FallProne);
        yield return Row(FumbleTable.MeleeAttack, 41, 50, FumbleEffectKind.DropWeapon);
        yield return Row(FumbleTable.MeleeAttack, 51, 60, FumbleEffectKind.ThrowWeapon, amount: "1D10");
        yield return Row(FumbleTable.MeleeAttack, 61, 65, FumbleEffectKind.LoseWeaponHitPoints, amount: "1D10");
        yield return Row(FumbleTable.MeleeAttack, 66, 75, FumbleEffectKind.VisionObscured, amount: "1D3", magnitude: -30);
        yield return Row(FumbleTable.MeleeAttack, 76, 85, FumbleEffectKind.HitNearestAlly, hitGrade: LandedGrade.Normal,
            fallbackCondition: FumbleFallbackCondition.NoAllyNearby, fallbackMin: 41, fallbackMax: 50);
        yield return Row(FumbleTable.MeleeAttack, 86, 90, FumbleEffectKind.HitNearestAlly, hitGrade: LandedGrade.Special,
            fallbackCondition: FumbleFallbackCondition.NoAllyNearby, fallbackMin: 51, fallbackMax: 60);
        yield return Row(FumbleTable.MeleeAttack, 91, 98, FumbleEffectKind.HitNearestAlly, hitGrade: LandedGrade.Critical,
            fallbackCondition: FumbleFallbackCondition.NoAllyNearby, fallbackMin: 61, fallbackMax: 65);
        yield return Row(FumbleTable.MeleeAttack, 99, 99, FumbleEffectKind.Reroll, rerollCount: 2);
        yield return Row(FumbleTable.MeleeAttack, 100, 100, FumbleEffectKind.Reroll, rerollCount: 3);
    }

    public static IEnumerable<object?[]> MeleeParryRows()
    {
        // Ch 6, "Melee Weapon Parry Fumbles" table (p.148) -- 10 rows.
        yield return Row(FumbleTable.MeleeParry, 1, 20, FumbleEffectKind.LoseNextCombatRound);
        yield return Row(FumbleTable.MeleeParry, 21, 40, FumbleEffectKind.FallProne);
        yield return Row(FumbleTable.MeleeParry, 41, 50, FumbleEffectKind.DropWeapon);
        yield return Row(FumbleTable.MeleeParry, 51, 60, FumbleEffectKind.ThrowWeapon, amount: "1D10");
        yield return Row(FumbleTable.MeleeParry, 61, 75, FumbleEffectKind.VisionObscured, amount: "1D3", magnitude: -30);
        yield return Row(FumbleTable.MeleeParry, 76, 85, FumbleEffectKind.FoeAutomaticHit, hitGrade: LandedGrade.Normal);
        yield return Row(FumbleTable.MeleeParry, 86, 90, FumbleEffectKind.FoeAutomaticHit, hitGrade: LandedGrade.Special);
        yield return Row(FumbleTable.MeleeParry, 91, 93, FumbleEffectKind.FoeAutomaticHit, hitGrade: LandedGrade.Critical);
        yield return Row(FumbleTable.MeleeParry, 94, 98, FumbleEffectKind.Reroll, rerollCount: 2);
        yield return Row(FumbleTable.MeleeParry, 99, 100, FumbleEffectKind.Reroll, rerollCount: 3);
    }

    public static IEnumerable<object?[]> MissileAttackRows()
    {
        // Ch 6, "Missile Weapon Attack Fumbles" table (p.148) -- 12 rows.
        yield return Row(FumbleTable.MissileAttack, 1, 15, FumbleEffectKind.LoseNextCombatRound);
        yield return Row(FumbleTable.MissileAttack, 16, 25, FumbleEffectKind.LoseMultipleCombatRounds, amount: "1D3");
        yield return Row(FumbleTable.MissileAttack, 26, 40, FumbleEffectKind.FallProne);
        yield return Row(FumbleTable.MissileAttack, 41, 55, FumbleEffectKind.VisionObscured, amount: "1D3", magnitude: -30);
        yield return Row(FumbleTable.MissileAttack, 56, 65, FumbleEffectKind.DropWeaponAndScatter, amount: "1D6-1");
        yield return Row(FumbleTable.MissileAttack, 66, 80, FumbleEffectKind.LoseWeaponHitPoints, amount: "1D6",
            fallbackCondition: FumbleFallbackCondition.WeaponHasNoHitPoints, fallbackMin: 81, fallbackMax: 85);
        yield return Row(FumbleTable.MissileAttack, 81, 85, FumbleEffectKind.BreakWeapon);
        yield return Row(FumbleTable.MissileAttack, 86, 90, FumbleEffectKind.HitNearestAlly, hitGrade: LandedGrade.Normal,
            fallbackCondition: FumbleFallbackCondition.NoAllyNearby, fallbackMin: 56, fallbackMax: 65);
        yield return Row(FumbleTable.MissileAttack, 91, 95, FumbleEffectKind.HitNearestAlly, hitGrade: LandedGrade.Special,
            fallbackCondition: FumbleFallbackCondition.NoAllyNearby, fallbackMin: 66, fallbackMax: 80);
        yield return Row(FumbleTable.MissileAttack, 96, 98, FumbleEffectKind.HitNearestAlly, hitGrade: LandedGrade.Critical,
            fallbackCondition: FumbleFallbackCondition.NoAllyNearby, fallbackMin: 81, fallbackMax: 85);
        yield return Row(FumbleTable.MissileAttack, 99, 99, FumbleEffectKind.Reroll, rerollCount: 2);
        yield return Row(FumbleTable.MissileAttack, 100, 100, FumbleEffectKind.Reroll, rerollCount: 3);
    }

    public static IEnumerable<object?[]> NaturalRows()
    {
        // Ch 6, "Natural Weapon Attack and Parry Fumbles" table (p.149) -- 11 rows.
        yield return Row(FumbleTable.Natural, 1, 25, FumbleEffectKind.LoseNextCombatRound);
        yield return Row(FumbleTable.Natural, 26, 30, FumbleEffectKind.LoseMultipleCombatRounds, amount: "1D3");
        yield return Row(FumbleTable.Natural, 31, 50, FumbleEffectKind.FallProne);
        yield return Row(FumbleTable.Natural, 51, 60, FumbleEffectKind.FallProneAndTwistAnkle, amount: "1D10", magnitude: -1);
        yield return Row(FumbleTable.Natural, 61, 75, FumbleEffectKind.VisionObscured, amount: "1D3", magnitude: -30);
        yield return Row(FumbleTable.Natural, 76, 85, FumbleEffectKind.StrainSelf, magnitude: 1);
        yield return Row(FumbleTable.Natural, 86, 90, FumbleEffectKind.HitNearestAlly, hitGrade: LandedGrade.Normal,
            fallbackCondition: FumbleFallbackCondition.NoAllyNearby, fallbackMin: 76, fallbackMax: 85);
        yield return Row(FumbleTable.Natural, 91, 94, FumbleEffectKind.HitNearestAlly, hitGrade: LandedGrade.Special,
            fallbackCondition: FumbleFallbackCondition.NoAllyNearby, fallbackMin: 76, fallbackMax: 85);
        yield return Row(FumbleTable.Natural, 95, 98, FumbleEffectKind.HitHardSurface, hitGrade: LandedGrade.Normal);
        yield return Row(FumbleTable.Natural, 99, 99, FumbleEffectKind.Reroll, rerollCount: 2);
        yield return Row(FumbleTable.Natural, 100, 100, FumbleEffectKind.Reroll, rerollCount: 3);
    }

    [Theory]
    [MemberData(nameof(MeleeAttackRows))]
    [MemberData(nameof(MeleeParryRows))]
    [MemberData(nameof(MissileAttackRows))]
    [MemberData(nameof(NaturalRows))]
    public void Every_printed_fumble_row_matches_the_book(
        FumbleTable table,
        int minRoll,
        int maxRoll,
        FumbleEffectKind kind,
        string? amount,
        int? magnitude,
        LandedGrade? hitGrade,
        FumbleFallbackCondition? fallbackCondition,
        int fallbackMin,
        int fallbackMax,
        int rerollCount)
    {
        var row = Ruleset.ForTable(table).Rows.Single(r => r.MinimumRoll == minRoll);

        Assert.Equal(maxRoll, row.MaximumRoll);
        Assert.Equal(kind, row.Kind);
        Assert.Equal(amount, row.Amount?.Notation);
        Assert.Equal(magnitude, row.Magnitude);
        Assert.Equal(hitGrade, row.HitGrade);

        if (fallbackCondition is null)
        {
            Assert.Null(row.Fallback);
        }
        else
        {
            Assert.NotNull(row.Fallback);
            Assert.Equal(fallbackCondition, row.Fallback!.Condition);
            Assert.Equal(fallbackMin, row.Fallback.MinimumRoll);
            Assert.Equal(fallbackMax, row.Fallback.MaximumRoll);
        }

        Assert.Equal(rerollCount == -1 ? (int?)null : rerollCount, row.RerollCount);
    }

    [Theory]
    [InlineData(FumbleTable.MeleeAttack, 12)]
    [InlineData(FumbleTable.MeleeParry, 10)]
    [InlineData(FumbleTable.MissileAttack, 12)]
    [InlineData(FumbleTable.Natural, 11)]
    public void Each_table_has_exactly_its_printed_number_of_rows(FumbleTable table, int expectedRows)
    {
        Assert.Equal(expectedRows, Ruleset.ForTable(table).Rows.Count);
    }

    [Theory]
    [InlineData(FumbleTable.MeleeAttack)]
    [InlineData(FumbleTable.MeleeParry)]
    [InlineData(FumbleTable.MissileAttack)]
    [InlineData(FumbleTable.Natural)]
    public void Every_d100_result_maps_to_exactly_one_row(FumbleTable table)
    {
        var consequences = Ruleset.ForTable(table);

        // The validating constructor already pins full 1-100 coverage; this walks every result to
        // confirm the loaded data actually tiles the range with no throw.
        for (var roll = 1; roll <= 100; roll++)
        {
            Assert.Single(consequences.Rows, r => r.Contains(roll));
        }
    }

    private static object?[] Row(
        FumbleTable table,
        int minRoll,
        int maxRoll,
        FumbleEffectKind kind,
        string? amount = null,
        int? magnitude = null,
        LandedGrade? hitGrade = null,
        FumbleFallbackCondition? fallbackCondition = null,
        int fallbackMin = -1,
        int fallbackMax = -1,
        int rerollCount = -1) =>
        new object?[]
        {
            table, minRoll, maxRoll, kind, amount, magnitude, hitGrade,
            fallbackCondition, fallbackMin, fallbackMax, rerollCount,
        };
}
