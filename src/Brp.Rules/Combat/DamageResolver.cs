using Brp.Core.Abilities;
using Brp.Core.Dice;
using Brp.Core.Randomness;
using Brp.Rules.Characters;
using Brp.Rules.Gear;

namespace Brp.Rules.Combat;

/// <summary>
/// Turns a landed attack (piece C's <see cref="AttackDefenseOutcome"/>) into damage, applies it
/// to a target's hit points, and determines the resulting condition. Ch 6: Combat, "Levels of
/// Success and Failure" (pp.146-147) and "Damage &amp; Healing" (pp.154-156); Ch 7: Spot Rules,
/// "Knockout Attacks" (p.174). This is Layer 4 piece D (#52). See
/// <c>docs/decisions/0017-damage.md</c> for the corrections made against the initial ruleset
/// transcription and the seams this leaves for piece E and the injury spot rules.
/// <para>
/// <strong>Normal and Special hits share identical damage arithmetic</strong> -- Ch 6, p.147,
/// footnote **: "For a greatsword, full damage is 2D8 on a normal success, 2D8 bleeding damage
/// on a special success" -- the dice are the same (2D8 both times); only the (out-of-scope)
/// special-effect type (bleeding, crushing, entangling, impaling, knockback) differs. Only a
/// Critical hit uses the weapon's maximum instead of a fresh roll (Ch 6, p.146: "the maximum
/// possible damage for the weapon used ... plus the normal rolled damage modifier").
/// </para>
/// </summary>
public static class DamageResolver
{
    /// <summary>
    /// Rolls damage for one landed hit (or returns zero for a Miss). Consumes entropy for the
    /// weapon dice (Normal/Special) and, when applicable, the damage bonus -- never for a Miss
    /// or for the constant weapon maximum a Critical hit uses instead.
    /// </summary>
    /// <param name="landedGrade">The effective grade of hit, from piece C's outcome.</param>
    /// <param name="armorTreatment">How armor applies, from piece C's outcome.</param>
    /// <param name="weapon">The attacker's weapon.</param>
    /// <param name="damageBonus">
    /// The attacker's damage bonus expression (<see cref="AbilitySet.DamageModifier"/>), or
    /// <see langword="null"/> if none applies. Added only when
    /// <paramref name="weapon"/>'s <see cref="WeaponDefinition.ApplyDamageBonus"/> is
    /// <see langword="true"/> -- firearms (Ch 6, p.147 footnote, "Damage modifier ... rolled
    /// separately") are exempt via that flag, not here.
    /// </param>
    /// <param name="armorValue">
    /// The applicable armor value at the location struck. Supplied by the caller -- this
    /// resolver does not roll hit locations or select armor by location.
    /// </param>
    /// <param name="entropy">The entropy source, per AGENTS.md invariant 5.</param>
    public static DamageRoll RollDamage(
        LandedGrade landedGrade,
        ArmorTreatment armorTreatment,
        WeaponDefinition weapon,
        DiceExpression? damageBonus,
        int armorValue,
        IEntropySource entropy)
    {
        ArgumentNullException.ThrowIfNull(weapon);
        ArgumentNullException.ThrowIfNull(entropy);
        ArgumentOutOfRangeException.ThrowIfNegative(armorValue);

        if (landedGrade == LandedGrade.Miss)
        {
            return new DamageRoll(
                landedGrade, WeaponRoll: null, DamageBonusRoll: null, WeaponMaximum: null,
                ArmorApplied: 0, DamageDealt: 0,
                SourceText: "Ch 6: Combat, Attack and Defense Matrix (p.147): a Miss deals no damage.");
        }

        var ignoreArmor = armorTreatment is ArmorTreatment.Bypassed or ArmorTreatment.DoesNotApply;
        if (!ignoreArmor && armorTreatment != ArmorTreatment.Subtracted)
        {
            throw new ArgumentException(
                $"Armor treatment '{armorTreatment}' is not valid for a landed hit.", nameof(armorTreatment));
        }

        if (landedGrade == LandedGrade.Critical)
        {
            var weaponMaximum = weapon.Damage.MaximumPossible();
            var (bonusValue, bonusRoll) = RollDamageBonus(weapon, damageBonus, entropy);
            var criticalDamage = Math.Max(0, weaponMaximum + bonusValue);
            return new DamageRoll(
                landedGrade, WeaponRoll: null, bonusRoll, weaponMaximum,
                ArmorApplied: 0, DamageDealt: criticalDamage,
                SourceText: "Ch 6: Combat, \"Critical Success\" (p.146): maximum weapon damage " +
                    "plus the damage modifier, ignoring armor.");
        }

        // Normal and Special: identical dice arithmetic -- see the type remarks.
        var weaponRoll = weapon.Damage.Roll(entropy);
        var (dbValue, dbRoll) = RollDamageBonus(weapon, damageBonus, entropy);
        var rawTotal = weaponRoll.RawTotal + dbValue;
        var armorApplied = ignoreArmor ? 0 : Math.Min(armorValue, Math.Max(0, rawTotal));
        var damageDealt = ignoreArmor ? Math.Max(0, rawTotal) : Math.Max(0, rawTotal - armorValue);
        var sourceText = landedGrade == LandedGrade.Special
            ? "Ch 6: Combat, \"Special Success\" (p.146) and Attack and Defense Matrix footnote " +
              "** (p.147): normal weapon damage plus the damage modifier, armor subtracted " +
              "(the special-effect type is out of scope -- see docs/decisions/0017-damage.md)."
            : "Ch 6: Combat, \"Success\" (p.146): weapon damage plus the damage modifier, armor subtracted.";
        return new DamageRoll(landedGrade, weaponRoll, dbRoll, WeaponMaximum: null, armorApplied, damageDealt, sourceText);
    }

    /// <summary>
    /// Applies a rolled damage amount to <paramref name="target"/>'s hit points, tracking
    /// negative HP (Ch 2, p.13), recording the blow as a wound in
    /// <paramref name="wounds"/> for piece E, and returning the resulting condition. A Miss
    /// changes nothing and records no wound.
    /// </summary>
    public static DamageApplicationResult ApplyDamage(
        AbilitySet target,
        WoundTrack wounds,
        DamageRoll damage,
        DamageRuleset ruleset,
        string woundDescription)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(wounds);
        ArgumentNullException.ThrowIfNull(damage);
        ArgumentNullException.ThrowIfNull(ruleset);
        ArgumentException.ThrowIfNullOrWhiteSpace(woundDescription);

        if (damage.LandedGrade == LandedGrade.Miss)
        {
            return new DamageApplicationResult(0, target.CurrentHitPoints, ClassifyHitPoints(target.CurrentHitPoints, ruleset), Wound: null);
        }

        return Apply(target, wounds, damage.DamageDealt, ruleset, new Wound(woundDescription));
    }

    /// <summary>
    /// Resolves a declared knockout attack (Ch 7: Spot Rules, "Knockout Attacks", p.174). The
    /// caller is responsible for the parts of that spot rule outside this piece's scope: that
    /// the attack was declared at the start of the round, rolled as a Difficult attack against a
    /// target with a clearly defined head, and that <paramref name="outcome"/> already reflects
    /// that roll's success or failure -- this method only turns a landed
    /// <see cref="AttackDefenseOutcome"/> into the knockout branch it resolves to.
    /// </summary>
    /// <param name="outcome">Piece C's outcome for the (already Difficult) attack roll.</param>
    /// <param name="weapon">The attacker's weapon.</param>
    /// <param name="damageBonus">The attacker's damage bonus expression, or <see langword="null"/>.</param>
    /// <param name="armorValue">
    /// The applicable armor value. Ch 7, p.174: "Armor defends normally in all cases" -- this
    /// resolver applies <paramref name="outcome"/>'s ordinary per-grade armor treatment (still
    /// ignored on a Critical), not a knockout-specific rule.
    /// </param>
    /// <param name="target">The target, whose <see cref="AbilitySet.MajorWoundLevel"/> (Ch 2,
    /// p.14) supplies the half-total-hit-points threshold this rule compares against.</param>
    /// <param name="ruleset">Supplies the knockout duration dice.</param>
    /// <param name="entropy">The entropy source, per AGENTS.md invariant 5.</param>
    public static KnockoutOutcome ResolveKnockoutAttack(
        AttackDefenseOutcome outcome,
        WeaponDefinition weapon,
        DiceExpression? damageBonus,
        int armorValue,
        AbilitySet target,
        DamageRuleset ruleset,
        IEntropySource entropy)
    {
        ArgumentNullException.ThrowIfNull(outcome);
        ArgumentNullException.ThrowIfNull(weapon);
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(ruleset);
        ArgumentNullException.ThrowIfNull(entropy);

        var roll = RollDamage(outcome.LandedGrade, outcome.ArmorTreatment, weapon, damageBonus, armorValue, entropy);

        if (outcome.LandedGrade == LandedGrade.Miss)
        {
            return new KnockoutOutcome(KnockedOut: false, DamageDealt: 0, DurationRounds: null, DurationRoll: null, roll);
        }

        // Ch 6, p.156: "equal to or more than half the character's total hit points" is a major
        // wound -- reuse the already-tested Layer 1 figure rather than a second copy of the
        // fraction (see DamageRuleset's remarks).
        var isMajorWound = roll.DamageDealt >= target.MajorWoundLevel;
        if (!isMajorWound)
        {
            // Ch 7, p.174: "the original damage rolled is ignored and the target is dealt the
            // minimum damage for the weapon (after armor) but is not knocked out."
            var ignoreArmor = outcome.ArmorTreatment is ArmorTreatment.Bypassed or ArmorTreatment.DoesNotApply;
            var weaponMinimum = weapon.Damage.MinimumPossible();
            var minorDamage = ignoreArmor ? Math.Max(0, weaponMinimum) : Math.Max(0, weaponMinimum - armorValue);
            return new KnockoutOutcome(KnockedOut: false, minorDamage, DurationRounds: null, DurationRoll: null, roll);
        }

        // Ch 7, p.174: "the target takes 1 damage and is knocked out for 1D10+10 rounds."
        var durationRoll = ruleset.KnockoutDuration.Roll(entropy);
        return new KnockoutOutcome(KnockedOut: true, DamageDealt: 1, durationRoll.Total, durationRoll, roll);
    }

    /// <summary>
    /// Applies a knockout attack's resolved damage to <paramref name="target"/>'s hit points,
    /// mirroring <see cref="ApplyDamage"/> for the knockout-specific outcome shape. Records no
    /// wound if the attack missed.
    /// </summary>
    public static DamageApplicationResult ApplyKnockoutAttack(
        AbilitySet target,
        WoundTrack wounds,
        KnockoutOutcome knockout,
        DamageRuleset ruleset,
        string woundDescription)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(wounds);
        ArgumentNullException.ThrowIfNull(knockout);
        ArgumentNullException.ThrowIfNull(ruleset);
        ArgumentException.ThrowIfNullOrWhiteSpace(woundDescription);

        if (knockout.UnderlyingRoll.LandedGrade == LandedGrade.Miss)
        {
            return new DamageApplicationResult(0, target.CurrentHitPoints, ClassifyHitPoints(target.CurrentHitPoints, ruleset), Wound: null);
        }

        return Apply(target, wounds, knockout.DamageDealt, ruleset, new Wound(woundDescription));
    }

    /// <summary>
    /// The seam for the timing Ch 2 p.13 and Ch 6 p.156 both describe: "if their hit points
    /// reach 0, they die at the end of the following round." This resolver does not track combat
    /// rounds itself (that is piece B's <c>CombatRound</c>, and the First Aid window that can
    /// still save a fatally wounded character is piece E) -- a caller re-checks hit points at
    /// the actual end of the following round and calls this to decide whether death resolves.
    /// </summary>
    /// <param name="hitPointsAtEndOfFollowingRound">
    /// The target's hit points as of the end of the round following the one the fatal wound was
    /// suffered in -- after any First Aid or other intervention piece E might apply.
    /// </param>
    /// <param name="ruleset">Supplies <see cref="DamageRuleset.DeadHitPointLevel"/>.</param>
    public static bool ResolvesToDeath(int hitPointsAtEndOfFollowingRound, DamageRuleset ruleset)
    {
        ArgumentNullException.ThrowIfNull(ruleset);
        return hitPointsAtEndOfFollowingRound <= ruleset.DeadHitPointLevel;
    }

    private static DamageApplicationResult Apply(
        AbilitySet target, WoundTrack wounds, int damageDealt, DamageRuleset ruleset, Wound wound)
    {
        var resultingHitPoints = target.CurrentHitPoints - damageDealt;
        target.SetCurrentHitPoints(resultingHitPoints);
        wounds.Add(wound);
        return new DamageApplicationResult(damageDealt, resultingHitPoints, ClassifyHitPoints(resultingHitPoints, ruleset), wound);
    }

    private static HitPointCondition ClassifyHitPoints(int hitPoints, DamageRuleset ruleset)
    {
        if (hitPoints <= ruleset.DeadHitPointLevel)
        {
            return HitPointCondition.FatallyWounded;
        }

        return hitPoints <= ruleset.UnconsciousHitPointLevel
            ? HitPointCondition.Unconscious
            : HitPointCondition.Unaffected;
    }

    private static (int Value, DiceRoll? Roll) RollDamageBonus(
        WeaponDefinition weapon, DiceExpression? damageBonus, IEntropySource entropy)
    {
        if (!weapon.ApplyDamageBonus || damageBonus is null)
        {
            return (0, null);
        }

        // Thrown weapons take half db, rounded up (Ch 6, p.147 note on missile weapons carried
        // over from Ch 3, p.47) -- no thrown weapon exists in the gear ruleset yet (#42's
        // hand-picked subset), so WeaponDefinition.ApplyDamageBonus stays a boolean and this is
        // the seam a future thrown-weapon addition would extend, not a rule implemented here.
        var roll = damageBonus.Roll(entropy);
        return (roll.RawTotal, roll);
    }
}
