namespace Brp.Rules.Combat;

/// <summary>
/// The data-defined values for Ch 6: Combat, "Hit Locations" (p.145) and Ch 2: Characters, "Hit
/// Points by Hit Location (Option)" (p.14) that <see cref="HitLocationResolver"/>,
/// <see cref="HitPointsByLocationCalculator"/>, and <see cref="HitLocationDamageResolver"/> read
/// (AGENTS.md invariant 7: rules values are data, not constants). Loaded from
/// <c>hit-location-ruleset.json</c> by <c>Brp.Data.NoirHitLocationRuleset.Load()</c>. See
/// <c>docs/decisions/0024-hit-locations.md</c>.
/// </summary>
public sealed class HitLocationRuleset
{
    /// <summary>Creates a hit location ruleset from data-defined values.</summary>
    public HitLocationRuleset(
        HitLocationTable table,
        int limbHeadAbdomenDivisor,
        int chestNumerator,
        int chestDenominator,
        int armDivisor,
        int limbDamageCapMultiplier)
    {
        ArgumentNullException.ThrowIfNull(table);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(limbHeadAbdomenDivisor);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(chestNumerator);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(chestDenominator);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(armDivisor);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(limbDamageCapMultiplier);

        Table = table;
        LimbHeadAbdomenDivisor = limbHeadAbdomenDivisor;
        ChestNumerator = chestNumerator;
        ChestDenominator = chestDenominator;
        ArmDivisor = armDivisor;
        LimbDamageCapMultiplier = limbDamageCapMultiplier;
    }

    /// <summary>Ch 6, "Hit Locations" (p.145): the D20 hit-location table.</summary>
    public HitLocationTable Table { get; }

    /// <summary>
    /// Ch 2: Characters, "Hit Points by Hit Location" (p.14): "Leg, Abdomen, Head: 1/3 total hit
    /// points." Divides <see cref="Core.Abilities.AbilitySet.MaximumHitPoints"/>, rounded up.
    /// </summary>
    public int LimbHeadAbdomenDivisor { get; }

    /// <summary>Ch 2: Characters, p.14: "Chest: 4/10 total hit points" -- the fraction's numerator.</summary>
    public int ChestNumerator { get; }

    /// <summary>Ch 2: Characters, p.14: "Chest: 4/10 total hit points" -- the fraction's denominator.</summary>
    public int ChestDenominator { get; }

    /// <summary>Ch 2: Characters, p.14: "Arm: 1/4 total hit points."</summary>
    public int ArmDivisor { get; }

    /// <summary>
    /// Ch 6, "Damage Equals or Exceeds Double the Location's Hit Points" (p.157): "your character
    /// cannot take more than twice the possible points of damage in an arm or leg from a single
    /// blow" -- the multiplier applied to a limb's hit points to find the total-hit-point cap for a
    /// single blow. See <see cref="HitLocationDamageResolver"/> for the falling exception
    /// (Ch 7: Spot Rules, "Falling", p.172).
    /// </summary>
    public int LimbDamageCapMultiplier { get; }
}
