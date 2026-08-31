using Brp.Core.Abilities;
using Brp.Core.Contests;
using Brp.Core.Randomness;
using Brp.Core.Resolution;

namespace Brp.Rules.Combat;

/// <summary>
/// Resolves Ch 6: Combat, "Major Wounds" and "Fatal Wounds" (pp.155-156): the shock a major wound
/// imposes, the immediate Luck roll that decides whether the injury is permanent, the Major Wounds
/// Table drain on a failed Luck roll, the cumulative-minor-wound Luck-or-unconscious rule, and the
/// fatal-wound rescue window. A major wound is a single wound of at least half the character's total
/// hit points -- reusing the already-tested Layer 1 figure
/// <see cref="AbilitySet.MajorWoundLevel"/> (Ch 2, p.14) rather than re-deriving the fraction.
/// Characteristic loss is applied through <see cref="AbilitySet.Set"/> (via <see cref="InjuryDrain"/>)
/// so derived values (hit points, damage modifier, major-wound level) recompute live (Ch 2, p.13;
/// ADR 0008), the same path as the #96 injury spot rules. See <c>docs/decisions/0021-major-wounds.md</c>.
/// <para>
/// <strong>Structured outcomes, not a simulated round.</strong> "Shock" (fight on for rounds equal
/// to remaining hit points, then unconscious), the collapse-for-an-hour at 2 or fewer hit points,
/// the still-able-to-fight flag, and the MOV reduction are returned as data for whichever piece runs
/// a combat round to apply -- this resolver holds no encounter model, the same caller seam as
/// #50/#96/#97. The book's own caveat that Major Wounds are "incompatible with hit locations" (p.156)
/// is recorded in ADR 0021 for #112 to reconcile; this resolver applies loss to the single hit-point
/// pool, as the book does when hit locations are not used.
/// </para>
/// </summary>
public static class MajorWoundResolver
{
    private static readonly CharacteristicId Power = new("POW");

    /// <summary>
    /// Whether a single wound of <paramref name="woundDamage"/> hit points is a major wound for
    /// <paramref name="target"/> -- Ch 6, "Major Wounds" (p.155): "equal to or more than half the
    /// character's total hit points." Reuses <see cref="AbilitySet.MajorWoundLevel"/> (Ch 2, p.14).
    /// </summary>
    public static bool IsMajorWound(int woundDamage, AbilitySet target)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentOutOfRangeException.ThrowIfNegative(woundDamage);
        return woundDamage >= target.MajorWoundLevel;
    }

    /// <summary>
    /// Resolves a single major wound for <paramref name="target"/>, whose hit points already reflect
    /// the wound (the caller applies the damage first, e.g. through
    /// <see cref="DamageResolver.ApplyDamage(AbilitySet, Characters.WoundTrack, int, DamageRuleset, string)"/>).
    /// Computes shock from the current remaining hit points, then attempts a Luck roll (POW×5): on a
    /// success the wound heals cleanly with no permanent loss; on a failure it rolls the Major Wounds
    /// Table and applies the indicated characteristic drain, recomputing derived values.
    /// <para>
    /// Consumes entropy in a fixed order: the Luck roll (one d100); then, only on a failed Luck roll,
    /// the Major Wounds Table roll (one d100) and the row's loss dice in order. Shock consumes none.
    /// </para>
    /// </summary>
    /// <param name="target">The wounded character (hit points already reduced by the wound).</param>
    /// <param name="woundDamage">The hit points that single wound dealt.</param>
    /// <param name="damageRuleset">Supplies the shock collapse (unconscious) threshold (Ch 2, p.13).</param>
    /// <param name="majorWoundRuleset">The Major Wounds Table and its values.</param>
    /// <param name="adjudicator">Resolves the table's gamemaster-discretion points (limb side, the 00 row's four characteristics).</param>
    /// <param name="entropy">The entropy source, per AGENTS.md invariant 5.</param>
    public static MajorWoundOutcome Resolve(
        AbilitySet target,
        int woundDamage,
        DamageRuleset damageRuleset,
        MajorWoundRuleset majorWoundRuleset,
        IMajorWoundAdjudicator adjudicator,
        IEntropySource entropy)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(damageRuleset);
        ArgumentNullException.ThrowIfNull(majorWoundRuleset);
        ArgumentNullException.ThrowIfNull(adjudicator);
        ArgumentNullException.ThrowIfNull(entropy);
        if (!IsMajorWound(woundDamage, target))
        {
            throw new ArgumentOutOfRangeException(
                nameof(woundDamage),
                woundDamage,
                $"A wound of {woundDamage} is not a major wound (major wound level {target.MajorWoundLevel}); " +
                "check IsMajorWound before calling Resolve.");
        }

        // Shock is immediate and read from remaining hit points before any Luck-roll characteristic
        // drain (which could clamp hit points): Ch 6, p.155.
        var shock = ResolveShock(target, damageRuleset);

        // The immediate Luck roll (POW's "Luck" characteristic roll, POW×5): Ch 6, p.155.
        var luckRoll = RollLuck(target, entropy);
        if (luckRoll.Succeeded)
        {
            // "the major wound will heal cleanly and does not inflict any permanent loss": no table
            // roll, no drain. Shock still applies. Able to fight (no lasting restriction).
            return new MajorWoundOutcome(
                shock, luckRoll, PermanentInjury: false, TableRoll: null, Row: null,
                CharacteristicLosses: [], MovementReduction: 0, AbleToFight: true, LimbSide: null);
        }

        // Failed Luck roll: the injury is permanent. Roll the Major Wounds Table and apply it.
        var tableRoll = entropy.NextD100();
        var row = majorWoundRuleset.Table.ForRoll(tableRoll);
        var (losses, movementReduction) = ApplyRow(target, row, adjudicator, entropy);
        var limbSide = row.RequiresLimbSide ? adjudicator.DecideLimbSide() : (BodySide?)null;

        return new MajorWoundOutcome(
            shock, luckRoll, PermanentInjury: true, tableRoll, row,
            losses, movementReduction, row.AbleToFight, limbSide);
    }

    /// <summary>
    /// Resolves the cumulative-minor-wound rule (Ch 6, "Minor Wounds", p.155). Two independent
    /// checks a caller tracking a character's same-day wounds can make:
    /// <list type="bullet">
    /// <item><description>
    /// If <paramref name="totalMinorHitPointsLostToday"/> reaches the major-wound level, the character
    /// "must make a successful Luck roll or they will fall unconscious." This is <em>not</em> a major
    /// wound: the Major Wounds Table is never rolled ("do not roll on the Major Wounds Table for
    /// multiple minor wounds").
    /// </description></item>
    /// <item><description>
    /// If the character's current hit points are 1 or 2, "this knocks them out for up to an hour" --
    /// read from <paramref name="target"/>'s current hit points, no roll.
    /// </description></item>
    /// </list>
    /// Consumes one d100 only when the Luck roll is triggered.
    /// </summary>
    /// <param name="target">The character (current hit points read for the knockout check).</param>
    /// <param name="totalMinorHitPointsLostToday">The running sum of the day's minor-wound hit-point losses.</param>
    /// <param name="damageRuleset">Supplies the 1-or-2-hit-point knockout threshold (Ch 2, p.13).</param>
    /// <param name="entropy">The entropy source, per AGENTS.md invariant 5.</param>
    public static CumulativeMinorWoundOutcome ResolveCumulativeMinorWounds(
        AbilitySet target,
        int totalMinorHitPointsLostToday,
        DamageRuleset damageRuleset,
        IEntropySource entropy)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(damageRuleset);
        ArgumentNullException.ThrowIfNull(entropy);
        ArgumentOutOfRangeException.ThrowIfNegative(totalMinorHitPointsLostToday);

        // "reduce them to 1 or 2 hit points" -> knocked out up to an hour. Above the dead level, at or
        // below the unconscious level (2).
        var knockedOut = target.CurrentHitPoints > damageRuleset.DeadHitPointLevel
            && target.CurrentHitPoints <= damageRuleset.UnconsciousHitPointLevel;

        var reachedEquivalent = totalMinorHitPointsLostToday >= target.MajorWoundLevel;
        if (!reachedEquivalent)
        {
            return new CumulativeMinorWoundOutcome(
                ReachedMajorWoundEquivalent: false, LuckRoll: null, FallsUnconscious: false, knockedOut);
        }

        var luckRoll = RollLuck(target, entropy);
        return new CumulativeMinorWoundOutcome(
            ReachedMajorWoundEquivalent: true, luckRoll, FallsUnconscious: !luckRoll.Succeeded, knockedOut);
    }

    /// <summary>
    /// Whether a fatally wounded character survives, per Ch 6, "Fatal Wounds" (p.156): a fatal wound
    /// (0 or negative hit points) "may be averted with immediate successful medical assistance" if aid
    /// in "the round they received the fatal wound or the round immediately after" brings hit points to
    /// 1 or more. Reuses <see cref="DamageResolver.ResolvesToDeath"/> for the hit-point test rather
    /// than duplicating the death threshold; this method adds the <em>window</em> the death timing
    /// leaves open (<see cref="MajorWoundRuleset.FatalWoundRescueWindowRounds"/>).
    /// </summary>
    /// <param name="hitPointsAfterAid">The character's hit points after the in-window medical aid.</param>
    /// <param name="roundsSinceFatalWound">
    /// Rounds elapsed since the fatal wound: 0 is the wound round, 1 the round immediately after.
    /// </param>
    /// <param name="majorWoundRuleset">Supplies the rescue-window length.</param>
    /// <param name="damageRuleset">Supplies the dead hit-point level (Ch 2, p.13; Ch 6, p.156).</param>
    public static bool SurvivesFatalWound(
        int hitPointsAfterAid,
        int roundsSinceFatalWound,
        MajorWoundRuleset majorWoundRuleset,
        DamageRuleset damageRuleset)
    {
        ArgumentNullException.ThrowIfNull(majorWoundRuleset);
        ArgumentNullException.ThrowIfNull(damageRuleset);
        ArgumentOutOfRangeException.ThrowIfNegative(roundsSinceFatalWound);

        // In the window and restored above the dead level -> survives. Otherwise death resolves.
        return roundsSinceFatalWound <= majorWoundRuleset.FatalWoundRescueWindowRounds
            && !DamageResolver.ResolvesToDeath(hitPointsAfterAid, damageRuleset);
    }

    private static MajorWoundShock ResolveShock(AbilitySet target, DamageRuleset damageRuleset)
    {
        var remaining = target.CurrentHitPoints;

        // "A character possessing 2 or fewer hit points after suffering a major wound collapses
        // immediately... and is unconscious for an hour." (Ch 6, p.155.) The unconscious threshold is
        // the same 2-or-less the hit-point rules use (Ch 2, p.13).
        if (remaining <= damageRuleset.UnconsciousHitPointLevel)
        {
            return new MajorWoundShock(FightingRounds: 0, CollapsesImmediately: true, UnconsciousForAnHour: true);
        }

        // "your character can fight on only for combat rounds equal to their current remaining hit
        // points" -> then unconscious. (Ch 6, p.155.)
        return new MajorWoundShock(FightingRounds: remaining, CollapsesImmediately: false, UnconsciousForAnHour: false);
    }

    private static RollOutcome RollLuck(AbilitySet target, IEntropySource entropy)
    {
        // POW's characteristic roll is named "Luck" in the ability ruleset (Ch 2); the immediate roll
        // a major wound calls for is the standard POW×5. (Ch 6, p.155.)
        var luck = target.Ruleset.StandardCharacteristicRoll(Power);
        return AbilityResolver.Resolve(target, luck, [], entropy)
            ?? throw new InvalidOperationException("The major wound Luck roll was unexpectedly gated.");
    }

    private static (IReadOnlyList<MajorWoundCharacteristicResult> Losses, int MovementReduction) ApplyRow(
        AbilitySet target, MajorWoundRow row, IMajorWoundAdjudicator adjudicator, IEntropySource entropy)
    {
        var results = new List<MajorWoundCharacteristicResult>();
        var firstLossPoints = 0;

        // Fixed losses (one for most rows, three for the 99 row), rolled and applied in row order.
        foreach (var loss in row.Losses)
        {
            var points = loss.Dice.Roll(entropy).Total;
            if (results.Count == 0)
            {
                firstLossPoints = points;
            }

            var resulting = InjuryDrain.Apply(target, loss.Characteristic, points);
            results.Add(new MajorWoundCharacteristicResult(loss.Characteristic, points, resulting));
        }

        // The 00 row: 1D4 each from four gamemaster-chosen characteristics.
        if (row.GamemasterChoice is { } choice)
        {
            var chosen = adjudicator.DecideCharacteristics(choice.Count);
            if (chosen.Count != choice.Count)
            {
                throw new InvalidOperationException(
                    $"The adjudicator returned {chosen.Count} characteristics for a row requiring {choice.Count}.");
            }

            foreach (var characteristic in chosen)
            {
                var points = choice.Dice.Roll(entropy).Total;
                var resulting = InjuryDrain.Apply(target, characteristic, points);
                results.Add(new MajorWoundCharacteristicResult(characteristic, points, resulting));
            }
        }

        // MOV is reduced "by the same amount" as the (single) characteristic loss on the rows that say
        // so; every ReducesMovement row has exactly one fixed loss. Reported as a structured outcome:
        // MOV is a flat value the engine does not derive from characteristics (AbilitySet.Movement).
        var movementReduction = row.ReducesMovement ? firstLossPoints : 0;
        return (results, movementReduction);
    }
}

/// <summary>
/// The immediate shock a major wound imposes (Ch 6, "Major Wounds", p.155). A structured outcome for
/// whichever piece runs a combat round -- this resolver simulates no round.
/// </summary>
/// <param name="FightingRounds">
/// Combat rounds the character can fight on before falling unconscious -- equal to their remaining
/// hit points, or 0 when they collapse immediately.
/// </param>
/// <param name="CollapsesImmediately">Whether the character collapses at once (2 or fewer hit points).</param>
/// <param name="UnconsciousForAnHour">Whether the collapse renders them unconscious for an hour.</param>
public sealed record MajorWoundShock(int FightingRounds, bool CollapsesImmediately, bool UnconsciousForAnHour);

/// <summary>One applied characteristic loss from the Major Wounds Table (Ch 6, pp.155-156).</summary>
/// <param name="Characteristic">The characteristic lowered.</param>
/// <param name="PointsLost">The points rolled and removed.</param>
/// <param name="ResultingValue">The characteristic's value after the drain (floored at its minimum).</param>
public sealed record MajorWoundCharacteristicResult(CharacteristicId Characteristic, int PointsLost, int ResultingValue);

/// <summary>
/// The result of resolving a single major wound (Ch 6, "Major Wounds", pp.155-156): the shock, the
/// Luck roll, and -- only when the Luck roll fails -- the permanent Major Wounds Table result.
/// </summary>
/// <param name="Shock">The immediate shock effect (always present).</param>
/// <param name="LuckRoll">The immediate Luck (POW×5) roll.</param>
/// <param name="PermanentInjury">Whether the Luck roll failed, making the injury permanent.</param>
/// <param name="TableRoll">The 1D100 Major Wounds Table result, or <see langword="null"/> on a successful Luck roll.</param>
/// <param name="Row">The table row rolled, or <see langword="null"/> on a successful Luck roll.</param>
/// <param name="CharacteristicLosses">The characteristic drains applied (empty on a successful Luck roll).</param>
/// <param name="MovementReduction">
/// MOV points to reduce (0 unless the row reduces MOV by the characteristic loss). A structured
/// outcome the caller applies -- MOV is a flat value the engine does not derive.
/// </param>
/// <param name="AbleToFight">
/// Whether the character can still fight after any permanent injury (always <see langword="true"/> on
/// a successful Luck roll). Independent of shock, which may still cut the fight short.
/// </param>
/// <param name="LimbSide">
/// The gamemaster-ruled side for a row whose limb is unspecified (the 95-96 row), or
/// <see langword="null"/> otherwise. Narrative only.
/// </param>
public sealed record MajorWoundOutcome(
    MajorWoundShock Shock,
    RollOutcome LuckRoll,
    bool PermanentInjury,
    int? TableRoll,
    MajorWoundRow? Row,
    IReadOnlyList<MajorWoundCharacteristicResult> CharacteristicLosses,
    int MovementReduction,
    bool AbleToFight,
    BodySide? LimbSide);

/// <summary>
/// The result of the cumulative-minor-wound rule (Ch 6, "Minor Wounds", p.155). Never a major wound:
/// the Major Wounds Table is not rolled.
/// </summary>
/// <param name="ReachedMajorWoundEquivalent">Whether the day's minor wounds summed to the major-wound level.</param>
/// <param name="LuckRoll">The Luck roll made when the equivalent is reached, or <see langword="null"/> otherwise.</param>
/// <param name="FallsUnconscious">Whether the character falls unconscious (the equivalent was reached and the Luck roll failed).</param>
/// <param name="KnockedOutForAnHour">Whether the character is knocked out for up to an hour (reduced to 1 or 2 hit points).</param>
public sealed record CumulativeMinorWoundOutcome(
    bool ReachedMajorWoundEquivalent,
    RollOutcome? LuckRoll,
    bool FallsUnconscious,
    bool KnockedOutForAnHour);
