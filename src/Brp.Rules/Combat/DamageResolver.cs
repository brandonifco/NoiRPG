using Brp.Core.Abilities;
using Brp.Core.Dice;
using Brp.Core.Randomness;
using Brp.Rules.Characters;
using Brp.Rules.Gear;

namespace Brp.Rules.Combat;

/// <summary>
/// Turns a landed attack (piece C's <see cref="AttackDefenseOutcome"/>) into damage, applies it
/// to a target's hit points, and determines the resulting condition. Ch 6: Combat, "Levels of
/// Success and Failure" (pp.146-147), "Special Successes and Damage" (pp.148-151), and "Damage
/// &amp; Healing" (pp.154-156); Ch 7: Spot Rules, "Knockout Attacks" (p.174). This is Layer 4
/// piece D (#52). See <c>docs/decisions/0017-damage.md</c> for the two corrections made against
/// the initial ruleset transcription and the seams this leaves for piece E and the injury spot
/// rules.
/// <para>
/// <strong>Special-success damage is weapon-type-dependent</strong> (Ch 6, "Special Successes
/// and Damage", pp.148-151), keyed by <see cref="WeaponDefinition.SpecialDamageType"/>:
/// </para>
/// <list type="bullet">
/// <item><description>
/// <strong>Impaling</strong> (firearms, pointed knives -- p.150): doubles the weapon's whole
/// damage expression (a fresh, independent second roll of the same dice, summed with the first
/// -- mathematically identical to doubling the dice count and any fixed modifier, per the
/// greatsword-style worked example "1D6+1 ... twice that, or 2D6+2"). The damage bonus is added
/// once, undoubled.
/// </description></item>
/// <item><description>
/// <strong>Crushing</strong> (clubs, brass knuckles -- p.149): weapon dice roll normally, but the
/// damage bonus doubles (rolled twice and summed) -- unless the attacker has no damage bonus, in
/// which case a flat <c>+1D4</c> substitutes, or a negative damage bonus, which collapses to no
/// bonus at all rather than doubling the negative.
/// </description></item>
/// <item><description>
/// <strong>Bleeding / Entangling / Knockback</strong> (no shipped weapon uses these): the base
/// damage number is identical to a Normal hit; only the (deferred) special effect differs.
/// </description></item>
/// </list>
/// <para>
/// A Critical hit is unrelated to any of this -- it always uses the weapon's maximum instead of
/// rolling (Ch 6, p.146).
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
    /// <param name="weapon">
    /// The attacker's weapon. Its <see cref="WeaponDefinition.SpecialDamageType"/> selects which
    /// Special-success formula applies -- see the type remarks.
    /// </param>
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
    /// <param name="ruleset">
    /// Supplies the Crushing special success's no-damage-bonus fallback (<c>+1D4</c>, Ch 6,
    /// p.149).
    /// </param>
    /// <param name="entropy">The entropy source, per AGENTS.md invariant 5.</param>
    public static DamageRoll RollDamage(
        LandedGrade landedGrade,
        ArmorTreatment armorTreatment,
        WeaponDefinition weapon,
        DiceExpression? damageBonus,
        int armorValue,
        DamageRuleset ruleset,
        IEntropySource entropy)
    {
        ArgumentNullException.ThrowIfNull(weapon);
        ArgumentNullException.ThrowIfNull(ruleset);
        ArgumentNullException.ThrowIfNull(entropy);
        ArgumentOutOfRangeException.ThrowIfNegative(armorValue);

        if (landedGrade == LandedGrade.Miss)
        {
            return new DamageRoll(
                landedGrade, SpecialDamageTypeApplied: null, WeaponRolls: [], DamageBonusRolls: [],
                WeaponMaximum: null, ArmorApplied: 0, DamageDealt: 0,
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
            var (bonusValue, bonusRolls) = RollNormalDamageBonus(weapon, damageBonus, entropy);
            var criticalDamage = Math.Max(0, weaponMaximum + bonusValue);
            return new DamageRoll(
                landedGrade, SpecialDamageTypeApplied: null, WeaponRolls: [], bonusRolls, weaponMaximum,
                ArmorApplied: 0, DamageDealt: criticalDamage,
                SourceText: "Ch 6: Combat, \"Critical Success\" (p.146): maximum weapon damage " +
                    "plus the damage modifier, ignoring armor.");
        }

        if (landedGrade == LandedGrade.Normal)
        {
            var weaponRoll = weapon.Damage.Roll(entropy);
            var (bonusValue, bonusRolls) = RollNormalDamageBonus(weapon, damageBonus, entropy);
            var (armorApplied, damageDealt) = ApplyArmor(weaponRoll.RawTotal + bonusValue, ignoreArmor, armorValue);
            return new DamageRoll(
                landedGrade, SpecialDamageTypeApplied: null, [weaponRoll], bonusRolls, WeaponMaximum: null,
                armorApplied, damageDealt,
                SourceText: "Ch 6: Combat, \"Success\" (p.146): weapon damage plus the damage modifier, armor subtracted.");
        }

        // Special -- the weapon-type-dependent branch. See the type remarks.
        return RollSpecialDamage(weapon, damageBonus, ignoreArmor, armorValue, ruleset, entropy);
    }

    private static DamageRoll RollSpecialDamage(
        WeaponDefinition weapon,
        DiceExpression? damageBonus,
        bool ignoreArmor,
        int armorValue,
        DamageRuleset ruleset,
        IEntropySource entropy)
    {
        switch (weapon.SpecialDamageType)
        {
            case SpecialDamageType.Impaling:
                {
                    // Ch 6, p.150: "An impale doubles the dice and modifier for the weapon's
                    // normal rolled damage... a short sword ... 1D6+1 ... does twice that, or
                    // 2D6+2." Summing two independent rolls of the same weapon-damage expression
                    // has the identical distribution to doubling its dice count and constant
                    // (see docs/decisions/0017-damage.md's worked proof), and lets this reuse
                    // WeaponDefinition.Damage as-is rather than needing to double a parsed
                    // expression's terms. The damage bonus is added once, undoubled -- "the
                    // damage modifier is not doubled, but is instead rolled normally and added."
                    var first = weapon.Damage.Roll(entropy);
                    var second = weapon.Damage.Roll(entropy);
                    var (bonusValue, bonusRolls) = RollNormalDamageBonus(weapon, damageBonus, entropy);
                    var (armorApplied, damageDealt) = ApplyArmor(first.RawTotal + second.RawTotal + bonusValue, ignoreArmor, armorValue);
                    return new DamageRoll(
                        LandedGrade.Special, SpecialDamageType.Impaling, [first, second], bonusRolls, WeaponMaximum: null,
                        armorApplied, damageDealt,
                        SourceText: "Ch 6: Combat, \"Impaling\" (pp.149-150): the weapon's damage " +
                            "(dice and any fixed modifier) doubled, plus an undoubled damage modifier, armor subtracted.");
                }

            case SpecialDamageType.Crushing:
                {
                    // Ch 6, p.149: "A crushing special success doubles the damage modifier
                    // normally applied... The weapon's damage is rolled normally, but the damage
                    // modifier is increased."
                    var weaponRoll = weapon.Damage.Roll(entropy);
                    var (bonusValue, bonusRolls) = RollCrushingDamageBonus(weapon, damageBonus, ruleset, entropy);
                    var (armorApplied, damageDealt) = ApplyArmor(weaponRoll.RawTotal + bonusValue, ignoreArmor, armorValue);
                    return new DamageRoll(
                        LandedGrade.Special, SpecialDamageType.Crushing, [weaponRoll], bonusRolls, WeaponMaximum: null,
                        armorApplied, damageDealt,
                        SourceText: "Ch 6: Combat, \"Crushing\" (p.149): normal weapon damage plus " +
                            "a doubled (or, absent one, +1D4) damage modifier, armor subtracted.");
                }

            default:
                {
                    // Bleeding / Entangling / Knockback (Ch 6, pp.149-151): the special RESULT is
                    // a separable effect (deferred -- see docs/decisions/0017-damage.md); the
                    // base damage number is identical to a Normal hit. No shipped weapon uses
                    // any of these three types.
                    var weaponRoll = weapon.Damage.Roll(entropy);
                    var (bonusValue, bonusRolls) = RollNormalDamageBonus(weapon, damageBonus, entropy);
                    var (armorApplied, damageDealt) = ApplyArmor(weaponRoll.RawTotal + bonusValue, ignoreArmor, armorValue);
                    return new DamageRoll(
                        LandedGrade.Special, weapon.SpecialDamageType, [weaponRoll], bonusRolls, WeaponMaximum: null,
                        armorApplied, damageDealt,
                        SourceText: $"Ch 6: Combat, \"{weapon.SpecialDamageType}\" (pp.149-151): damage number " +
                            "unchanged from a Normal hit; the special effect is deferred (see docs/decisions/0017-damage.md).");
                }
        }
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
    /// Applies a flat, already-computed hit-point loss to <paramref name="target"/>, recording it
    /// as a <see cref="Wound"/> and returning the resulting condition -- the non-weapon damage
    /// entry point the injury spot rules (#96) use for falling and poison, where the loss comes
    /// from a distance/SIZ or POT calculation rather than a weapon
    /// <see cref="DamageRoll"/>. Mirrors the private <see cref="Apply"/> that the weapon-damage
    /// overload of <see cref="ApplyDamage(AbilitySet, WoundTrack, DamageRoll, DamageRuleset, string)"/>
    /// reaches, so hit-point tracking (Ch 2, p.13: HP may go negative) and condition classification
    /// (Ch 2, p.13; Ch 6, p.156) are identical -- there is no fabricated weapon <see cref="DamageRoll"/>.
    /// </summary>
    /// <param name="target">The character taking the damage.</param>
    /// <param name="wounds">The wound track the blow is recorded in.</param>
    /// <param name="hitPointDamage">
    /// The hit points to remove, already fully computed by the caller (falling armor/SIZ/force,
    /// poison POT full-or-half). Must be non-negative; the resolver does not re-clamp or re-mitigate.
    /// </param>
    /// <param name="ruleset">Supplies the unconscious/dead thresholds for condition classification.</param>
    /// <param name="woundDescription">A free-text note describing the injury, for piece E to heal.</param>
    public static DamageApplicationResult ApplyDamage(
        AbilitySet target,
        WoundTrack wounds,
        int hitPointDamage,
        DamageRuleset ruleset,
        string woundDescription)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(wounds);
        ArgumentNullException.ThrowIfNull(ruleset);
        ArgumentOutOfRangeException.ThrowIfNegative(hitPointDamage);
        ArgumentException.ThrowIfNullOrWhiteSpace(woundDescription);

        return Apply(target, wounds, hitPointDamage, ruleset, new Wound(woundDescription));
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
    /// ignored on a Critical), not a knockout-specific rule. Likewise, "the effects of special or
    /// critical successes... apply in all cases" means an Impaling special's doubled damage (or
    /// a Crushing special's doubled/substituted damage modifier) still applies to the damage
    /// rolled here to determine minor-vs-major wound equivalence -- this falls out of reusing
    /// <see cref="RollDamage"/> unchanged, not a separate rule.
    /// </param>
    /// <param name="target">The target, whose <see cref="AbilitySet.MajorWoundLevel"/> (Ch 2,
    /// p.14) supplies the half-total-hit-points threshold this rule compares against.</param>
    /// <param name="ruleset">Supplies the knockout duration dice and the Crushing fallback bonus.</param>
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

        var roll = RollDamage(outcome.LandedGrade, outcome.ArmorTreatment, weapon, damageBonus, armorValue, ruleset, entropy);

        if (outcome.LandedGrade == LandedGrade.Miss)
        {
            return new KnockoutOutcome(KnockedOut: false, DamageDealt: 0, DurationRounds: null, DurationRoll: null, roll);
        }

        // Ch 6, p.156: "equal to or more than half the character's total hit points" is a major
        // wound -- reuse the already-tested Layer 1 figure rather than a second copy of the
        // fraction (see DamageRuleset's remarks). roll.DamageDealt already reflects any
        // weapon-type-dependent Special formula (e.g. a doubled Impaling roll), per Ch 7 p.174.
        var isMajorWound = roll.DamageDealt >= target.MajorWoundLevel;
        if (!isMajorWound)
        {
            // Ch 7, p.174: "the original damage rolled is ignored and the target is dealt the
            // minimum damage for the weapon (after armor) but is not knocked out." This is a
            // flat baseline unrelated to the grade or special-damage type that landed.
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
    /// mirroring <see cref="ApplyDamage(AbilitySet, WoundTrack, DamageRoll, DamageRuleset, string)"/>
    /// for the knockout-specific outcome shape. Records no
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

    private static (int ArmorApplied, int DamageDealt) ApplyArmor(int rawTotal, bool ignoreArmor, int armorValue)
    {
        if (ignoreArmor)
        {
            return (0, Math.Max(0, rawTotal));
        }

        return (Math.Min(armorValue, Math.Max(0, rawTotal)), Math.Max(0, rawTotal - armorValue));
    }

    private static (int Value, IReadOnlyList<DiceRoll> Rolls) RollNormalDamageBonus(
        WeaponDefinition weapon, DiceExpression? damageBonus, IEntropySource entropy)
    {
        if (!weapon.ApplyDamageBonus || damageBonus is null)
        {
            return (0, []);
        }

        // Thrown weapons take half db, rounded up (Ch 6, p.147 note on missile weapons carried
        // over from Ch 3, p.47) -- no thrown weapon exists in the gear ruleset yet (#42's
        // hand-picked subset), so WeaponDefinition.ApplyDamageBonus stays a boolean and this is
        // the seam a future thrown-weapon addition would extend, not a rule implemented here.
        var roll = damageBonus.Roll(entropy);
        return (roll.RawTotal, [roll]);
    }

    private static (int Value, IReadOnlyList<DiceRoll> Rolls) RollCrushingDamageBonus(
        WeaponDefinition weapon, DiceExpression? damageBonus, DamageRuleset ruleset, IEntropySource entropy)
    {
        if (!weapon.ApplyDamageBonus)
        {
            return (0, []);
        }

        if (damageBonus is null)
        {
            // Ch 6, p.149: "if there is no damage modifier, it becomes +1D4."
            var fallback = ruleset.CrushingNoModifierBonus.Roll(entropy);
            return (fallback.RawTotal, [fallback]);
        }

        if (damageBonus.MaximumPossible() <= 0)
        {
            // Ch 6, p.149: "If the attacker has a negative damage modifier, this becomes no
            // damage modifier" -- not rolled at all, not doubled-then-floored.
            return (0, []);
        }

        // Ch 6, p.149: "doubles the damage modifier." Two independent rolls of the same
        // expression, summed, have the identical distribution to doubling its dice count (the
        // same technique RollSpecialDamage's Impaling branch uses for the weapon's own dice).
        var first = damageBonus.Roll(entropy);
        var second = damageBonus.Roll(entropy);
        return (first.RawTotal + second.RawTotal, [first, second]);
    }
}
