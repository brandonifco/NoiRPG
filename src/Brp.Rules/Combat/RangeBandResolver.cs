using Brp.Core.Modifiers;
using Brp.Core.Primitives;
using Brp.Rules.Gear;

namespace Brp.Rules.Combat;

/// <summary>
/// Maps a missile or firearm attack's distance to a <see cref="RangeBand"/> and the modifiers
/// that band contributes to <see cref="ModifierPipeline"/>, per Ch 6: Combat, "Missile Weapons"
/// (p.153-154) and Ch 7: Spot Rules, "Extended Range" (p.170). Consumes <c>Brp.Core.Modifiers</c>
/// (ADR 0007's pipeline) rather than working around it: point blank and medium range are ordinary
/// <see cref="DifficultyModifier"/> contributions and take part in ADR 0007's non-stacking
/// collapse like any other Difficult/Easy condition; only long range needs the special handling
/// documented on <see cref="RangeBandModifiers.IsExclusive"/>.
/// <para>
/// Deliberately does not consume the Ch 5: System, "Situational Modifiers" table's generic
/// "Range" row (p.132: "Far beyond the normal range -50%", etc.) for a missile/firearm attack --
/// that row is a general-purpose gamemaster tool for skills the book gives no dedicated range
/// mechanic to (Track, Perception checks, and so on). For missile and firearm attacks Ch 6/7
/// print a specific, multiplicative ladder, and applying the generic additive row on top would
/// double-count the same distance. See <c>docs/decisions/0014-range-bands.md</c>.
/// </para>
/// </summary>
public static class RangeBandResolver
{
    /// <summary>
    /// Ch 6 (p.153): "within the attacker's DEX/3 meters (round up)". The point-blank distance
    /// is derived from DEX, not a fixed distance.
    /// </summary>
    public static int PointBlankDistanceMeters(int dexterity, RangeBandRuleset ruleset)
    {
        ArgumentNullException.ThrowIfNull(ruleset);
        ArgumentOutOfRangeException.ThrowIfNegative(dexterity);

        return CeilDiv(dexterity, ruleset.PointBlankDexDivisor);
    }

    /// <summary>
    /// Ch 7 (p.170): "Small hand-propelled weapons such as the throwing knife and the throwing
    /// axe have no chance to hit beyond double base range." A weapon-class rule, not a distance
    /// tier: only weapons in <see cref="RangeBandRuleset.ThrowingWeaponClasses"/> are ever cut
    /// off, regardless of which <see cref="RangeBand"/> the distance would otherwise fall into.
    /// </summary>
    public static bool IsBeyondThrowingCutoff(
        WeaponClass weaponClass, int distanceMeters, int listedRangeMeters, RangeBandRuleset ruleset)
    {
        ArgumentNullException.ThrowIfNull(ruleset);
        ArgumentOutOfRangeException.ThrowIfNegative(distanceMeters);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(listedRangeMeters, 0);

        return ruleset.ThrowingWeaponClasses.Contains(weaponClass)
            && distanceMeters > listedRangeMeters * ruleset.ThrowingCutoffMultiplier;
    }

    /// <summary>
    /// Determines which <see cref="RangeBand"/> a shot at <paramref name="distanceMeters"/>
    /// falls into. Does not consider the throwing-weapon cutoff -- that is a separate,
    /// weapon-class-keyed check (<see cref="IsBeyondThrowingCutoff"/>), not a fifth band, per
    /// the settled decision on Issue #21.
    /// </summary>
    public static RangeBand DetermineBand(
        int distanceMeters, int dexterity, int listedRangeMeters, RangeBandRuleset ruleset)
    {
        ArgumentNullException.ThrowIfNull(ruleset);
        ArgumentOutOfRangeException.ThrowIfNegative(distanceMeters);
        ArgumentOutOfRangeException.ThrowIfNegative(dexterity);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(listedRangeMeters, 0);

        var pointBlankMeters = PointBlankDistanceMeters(dexterity, ruleset);
        if (distanceMeters <= pointBlankMeters)
        {
            return RangeBand.PointBlank;
        }

        if (distanceMeters <= listedRangeMeters)
        {
            return RangeBand.Normal;
        }

        if (distanceMeters <= listedRangeMeters * ruleset.MediumRangeMultiplier)
        {
            return RangeBand.Medium;
        }

        return RangeBand.LongRange;
    }

    /// <summary>
    /// Builds the modifier(s) a <paramref name="band"/> contributes to a roll against
    /// <paramref name="baseChance"/> -- the character's current, unmodified-for-this-shot skill
    /// rating (<c>ModifierChain.BaseChance</c>'s sense of "base", not the printed 5%-floor value
    /// from ADR 0007 / #27; see the remarks there for why the two must not be conflated).
    /// </summary>
    /// <param name="band">The band the shot falls into.</param>
    /// <param name="baseChance">
    /// The rating the range effect is computed against. For <see cref="RangeBand.LongRange"/> this is
    /// the value the book's "1/5 normal skill chance" divides -- the current rating, not the
    /// running value after any other stage of the pipeline has touched it.
    /// </param>
    /// <param name="aimedWithTargetingEquipment">
    /// True when the attacker used a scope, laser sight, or similar targeting system and spent
    /// one combat round aiming. Ch 6 (p.154), "Targeting Gear": halves the severity of the range
    /// modifier -- see <see cref="RangeBandRuleset.TargetingEquipmentDampeningNumerator"/> for
    /// the house interpretation of the book's ambiguous phrasing.
    /// </param>
    /// <param name="ruleset">The thresholds and multipliers to read.</param>
    /// <param name="source">A label prefix identifying the shot, used in the rendered chain.</param>
    public static RangeBandModifiers Resolve(
        RangeBand band,
        Percent baseChance,
        bool aimedWithTargetingEquipment,
        RangeBandRuleset ruleset,
        string source = "range")
    {
        ArgumentNullException.ThrowIfNull(ruleset);
        ArgumentNullException.ThrowIfNull(source);

        switch (band)
        {
            case RangeBand.PointBlank:
                return RangeBandModifiers.NonExclusive(
                    band,
                    [DifficultyModifier.Easy($"{source}: point blank (Ch 6, Missile Weapons; Ch 7, Extended Range)")]);

            case RangeBand.Normal:
                return RangeBandModifiers.NonExclusive(band, []);

            case RangeBand.Medium when !aimedWithTargetingEquipment:
                return RangeBandModifiers.NonExclusive(
                    band,
                    [DifficultyModifier.Difficult($"{source}: medium range (Ch 6, Missile Weapons; Ch 7, Extended Range)")]);

            case RangeBand.Medium:
                {
                    var (numerator, denominator) = Dampen(1, 2, ruleset);
                    return RangeBandModifiers.NonExclusive(
                        band,
                        [
                            new MultiplicativeModifier(
                            $"{source}: medium range, aimed with targeting equipment (Ch 6, Targeting Gear)",
                            numerator,
                            denominator),
                        ]);
                }

            case RangeBand.LongRange:
                {
                    var (numerator, denominator) = aimedWithTargetingEquipment
                        ? Dampen(ruleset.LongRangeChanceNumerator, ruleset.LongRangeChanceDenominator, ruleset)
                        : (ruleset.LongRangeChanceNumerator, ruleset.LongRangeChanceDenominator);

                    var overrideChance = baseChance.Scale(numerator, denominator, RoundingMode.Up);
                    var label = aimedWithTargetingEquipment
                        ? $"{source}: long range, aimed with targeting equipment (Ch 6, Long Range; Targeting Gear)"
                        : $"{source}: long range (Ch 6, Long Range; Ch 7, Extended Range)";

                    return RangeBandModifiers.Exclusive(band, [new OverrideModifier(label, overrideChance)]);
                }

            default:
                throw new ArgumentOutOfRangeException(nameof(band), band, "Unknown range band.");
        }
    }

    /// <summary>
    /// Resolves a full <see cref="ModifierChain"/> for a missile/firearm attack at
    /// <paramref name="band"/>, folding in <paramref name="otherModifiers"/> (the shot's other
    /// situational and permanent modifiers) -- except at <see cref="RangeBand.LongRange"/>, where the
    /// settled decision on Issue #21 requires the override to stand alone (see
    /// <see cref="RangeBandModifiers.IsExclusive"/>): <paramref name="otherModifiers"/> is
    /// discarded rather than composed with it, so a Difficult condition from elsewhere cannot
    /// halve the override again and arrive at base ÷ 10.
    /// </summary>
    public static ModifierChain Evaluate(
        Percent baseChance,
        RangeBand band,
        IEnumerable<Modifier> otherModifiers,
        bool aimedWithTargetingEquipment,
        RangeBandRuleset ruleset,
        ModifierPolicy? policy = null,
        string source = "range")
    {
        ArgumentNullException.ThrowIfNull(otherModifiers);

        var rangeModifiers = Resolve(band, baseChance, aimedWithTargetingEquipment, ruleset, source);
        var modifiers = rangeModifiers.IsExclusive
            ? rangeModifiers.Modifiers
            : rangeModifiers.Modifiers.Concat(otherModifiers);

        return ModifierPipeline.Evaluate(baseChance, modifiers, policy);
    }

    /// <summary>
    /// Halves the severity of a range penalty for a shot aimed with targeting equipment (Ch 6,
    /// p.154, "Targeting Gear"; see <see cref="RangeBandRuleset.TargetingEquipmentDampeningNumerator"/>
    /// for the house interpretation this implements): the raw multiplier <c>n/d</c> moves
    /// halfway back to 1 rather than being applied at full strength.
    /// </summary>
    private static (int Numerator, int Denominator) Dampen(int numerator, int denominator, RangeBandRuleset ruleset)
    {
        var dampeningNumerator = ruleset.TargetingEquipmentDampeningNumerator;
        var dampeningDenominator = ruleset.TargetingEquipmentDampeningDenominator;

        // effective = 1 - (1 - n/d) * (dampNum/dampDen), kept as an exact rational rather than
        // reduced for floating-point convenience or a lossy intermediate division.
        var resultDenominator = denominator * dampeningDenominator;
        var resultNumerator = resultDenominator - ((denominator - numerator) * dampeningNumerator);
        return (resultNumerator, resultDenominator);
    }

    private static int CeilDiv(int value, int divisor) => (value + divisor - 1) / divisor;
}
