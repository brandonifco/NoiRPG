using Brp.Core.Modifiers;
using Brp.Core.Primitives;

namespace Brp.Rules.Combat;

/// <summary>
/// Maps a missile or firearm attack's distance to a <see cref="RangeBand"/> and the modifiers
/// that band contributes, per Ch 6: Combat, "Missile Weapons" (p.154) and Ch 7: Spot Rules,
/// "Extended Range" (p.171). Consumes <c>Brp.Core.Modifiers</c> (ADR 0007's pipeline) rather than
/// working around it: point blank and medium range are ordinary <see cref="DifficultyModifier"/>
/// contributions and take part in ADR 0007's non-stacking collapse like any other Difficult/Easy
/// condition; only long range needs the special handling documented on
/// <see cref="RangeBandOutcome.ExclusiveOverride"/>.
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
    /// Ch 6 (p.154): "within the attacker's DEX/3 meters (round up)". The point-blank distance
    /// is derived from DEX, not a fixed distance.
    /// </summary>
    public static int PointBlankDistanceMeters(int dexterity, RangeBandRuleset ruleset)
    {
        ArgumentNullException.ThrowIfNull(ruleset);
        ArgumentOutOfRangeException.ThrowIfNegative(dexterity);

        return CeilDiv(dexterity, ruleset.PointBlankDexDivisor);
    }

    /// <summary>
    /// Ch 7 (p.171): "Small hand-propelled weapons such as the throwing knife and the throwing
    /// axe have no chance to hit beyond double base range." A per-weapon fact, not a distance
    /// tier and not a whole-weapon-class rule: <paramref name="isHandThrownWeapon"/> must
    /// identify specifically the small, hand-propelled missile weapons the passage names (the
    /// throwing knife, the throwing axe, and similar). Ch 8's own "Missile" weapon class
    /// (p.196) also contains mechanism-launched weapons -- the sling and the blowgun -- which
    /// this cutoff must not reach; keying it to the whole class was a defect this parameter fixes
    /// (see <c>docs/decisions/0014-range-bands.md</c>).
    /// </summary>
    public static bool IsBeyondThrowingCutoff(
        bool isHandThrownWeapon, int distanceMeters, int listedRangeMeters, RangeBandRuleset ruleset)
    {
        ArgumentNullException.ThrowIfNull(ruleset);
        ArgumentOutOfRangeException.ThrowIfNegative(distanceMeters);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(listedRangeMeters, 0);

        return isHandThrownWeapon && distanceMeters > listedRangeMeters * ruleset.ThrowingCutoffMultiplier;
    }

    /// <summary>
    /// Determines which <see cref="RangeBand"/> a shot at <paramref name="distanceMeters"/>
    /// falls into. Does not consider the throwing-weapon cutoff -- that is a separate,
    /// per-weapon check (<see cref="IsBeyondThrowingCutoff"/>), not a fifth band, per the settled
    /// decision on Issue #21.
    /// <para>
    /// <strong>House shape:</strong> the book states Long Range as an exact threshold ("at
    /// quadruple the weapon's standard listed range") rather than a bounded range, and states no
    /// band beyond it. This implementation reads the three penalty bands as cumulative tiers --
    /// Normal up to 1x, Medium from just past 1x to 2x, Long from just past 2x onward, with no
    /// upper cutoff -- which is the only reading under which every distance has a defined band,
    /// and does not invent the "beyond quadruple range is impossible" rule an earlier draft did
    /// (see ADR 0007). A distance at exactly quadruple range, and every distance beyond it,
    /// therefore resolves the same way: <see cref="RangeBand.LongRange"/>. Because of this, the
    /// "quadruple range" multiplier itself is not read as a second boundary anywhere in this
    /// resolver -- see <see cref="RangeBandRuleset.MediumRangeMultiplier"/>'s remarks.
    /// </para>
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
    /// Builds the range effect for <paramref name="band"/> against <paramref name="baseChance"/>
    /// -- the character's current rating, before this shot's other modifiers are applied.
    /// </summary>
    /// <param name="band">The band the shot falls into.</param>
    /// <param name="baseChance">
    /// The rating the range effect is computed against -- <c>ModifierChain.BaseChance</c>'s sense
    /// of "base" (the character's current rating), not the printed 5%-floor value from ADR 0007
    /// / #27; see the remarks there for why the two must not be conflated.
    /// </param>
    /// <param name="otherModifiers">
    /// The shot's other pending modifiers. Only consulted at <see cref="RangeBand.LongRange"/>,
    /// and only for its <see cref="AdditiveKind.Permanent"/> entries: Ch 5: System, "Situational
    /// Modifiers" (p.132) figures a modifier integral to the skill into the rating <em>before</em>
    /// a Difficult/Easy grade doubles or halves it, and the long-range override is computed the
    /// same way -- against the base rating plus any permanent additive, not against the bare
    /// printed/current rating alone. Situational and difficulty modifiers in this list are read
    /// for nothing here; <see cref="Evaluate"/> is what discards them for a long-range shot.
    /// </param>
    /// <param name="aimedWithTargetingEquipment">
    /// True when the attacker used a scope, laser sight, or similar targeting system and spent
    /// one combat round aiming. Ch 6 (p.154), "Targeting Gear": halves the severity of the range
    /// modifier -- see <see cref="RangeBandRuleset.TargetingEquipmentDampeningNumerator"/> for
    /// the house interpretation of the book's ambiguous phrasing.
    /// </param>
    /// <param name="ruleset">The thresholds and multipliers to read.</param>
    /// <param name="source">A label prefix identifying the shot, used in the rendered chain.</param>
    public static RangeBandOutcome Resolve(
        RangeBand band,
        Percent baseChance,
        IEnumerable<Modifier> otherModifiers,
        bool aimedWithTargetingEquipment,
        RangeBandRuleset ruleset,
        string source = "range")
    {
        ArgumentNullException.ThrowIfNull(otherModifiers);
        ArgumentNullException.ThrowIfNull(ruleset);
        ArgumentNullException.ThrowIfNull(source);

        switch (band)
        {
            case RangeBand.PointBlank:
                return new RangeBandOutcome.Composable(
                    band,
                    [DifficultyModifier.Easy($"{source}: point blank (Ch 6, Missile Weapons; Ch 7, Extended Range)")]);

            case RangeBand.Normal:
                return new RangeBandOutcome.Composable(band, []);

            case RangeBand.Medium when !aimedWithTargetingEquipment:
                return new RangeBandOutcome.Composable(
                    band,
                    [DifficultyModifier.Difficult($"{source}: medium range (Ch 6, Missile Weapons; Ch 7, Extended Range)")]);

            case RangeBand.Medium:
                {
                    var (numerator, denominator) = Dampen(1, 2, ruleset);
                    return new RangeBandOutcome.Composable(
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
                    // Ch 5 (p.132): a permanent/integral modifier is figured into the rating before
                    // a Difficult/Easy grade -- and, by the same logic, before this override -- so it
                    // must not be among the modifiers Evaluate() discards for a long-range shot.
                    var permanentDelta = otherModifiers
                        .OfType<AdditiveModifier>()
                        .Where(a => a.Kind == AdditiveKind.Permanent)
                        .Sum(a => a.Delta);
                    var adjustedBase = baseChance.Add(permanentDelta);

                    var (numerator, denominator) = aimedWithTargetingEquipment
                        ? Dampen(ruleset.LongRangeChanceNumerator, ruleset.LongRangeChanceDenominator, ruleset)
                        : (ruleset.LongRangeChanceNumerator, ruleset.LongRangeChanceDenominator);

                    var chance = adjustedBase.Scale(numerator, denominator, RoundingMode.Up);
                    var label = aimedWithTargetingEquipment
                        ? $"{source}: long range, aimed with targeting equipment (Ch 6, Long Range; Targeting Gear)"
                        : $"{source}: long range (Ch 6, Long Range; Ch 7, Extended Range)";

                    return new RangeBandOutcome.ExclusiveOverride(band, chance, label);
                }

            default:
                throw new ArgumentOutOfRangeException(nameof(band), band, "Unknown range band.");
        }
    }

    /// <summary>
    /// Resolves a full <see cref="ModifierChain"/> for a missile/firearm attack at
    /// <paramref name="band"/>, folding in <paramref name="otherModifiers"/> (the shot's other
    /// situational and permanent modifiers) -- except at <see cref="RangeBand.LongRange"/>, where
    /// the settled decision on Issue #21 requires the override to stand alone (see
    /// <see cref="RangeBandOutcome.ExclusiveOverride"/>): the override is built from a single
    /// internally-constructed <see cref="OverrideModifier"/>, with every other pending modifier
    /// discarded, so a Difficult condition from elsewhere cannot halve the override again and
    /// arrive at base ÷ 10. This is the only place in the public API that ever materializes that
    /// <see cref="OverrideModifier"/> -- <see cref="Resolve"/> hands back a plain
    /// <see cref="Percent"/> for the exclusive case specifically so it cannot be lifted out and
    /// recomposed with other modifiers by a caller that bypasses this method.
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

        var otherList = otherModifiers as IReadOnlyCollection<Modifier> ?? otherModifiers.ToList();
        var outcome = Resolve(band, baseChance, otherList, aimedWithTargetingEquipment, ruleset, source);

        return outcome switch
        {
            RangeBandOutcome.ExclusiveOverride exclusive => ModifierPipeline.Evaluate(
                baseChance, [new OverrideModifier(exclusive.Source, exclusive.Chance)], policy),
            RangeBandOutcome.Composable composable => ModifierPipeline.Evaluate(
                baseChance, composable.Modifiers.Concat(otherList), policy),
            _ => throw new ArgumentOutOfRangeException(nameof(band), band, "Unknown range band outcome."),
        };
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
