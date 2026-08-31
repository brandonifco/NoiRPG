namespace Brp.Rules.Gear;

/// <summary>
/// The special-success damage type a weapon inflicts, per Ch 6: Combat, "Special Successes and
/// Damage" (pp.148-149), which lists these five types and what causes each: "Different types of
/// weapons do different types of damage upon special successes." Which type a weapon has
/// determines both its special-success damage <em>number</em> (only <see cref="Impaling"/> and
/// <see cref="Crushing"/> change the arithmetic -- see <c>Brp.Rules.Combat.DamageResolver</c>)
/// and its special-success <em>effect</em> (bleeding, entangling, knockback -- deferred, see
/// <c>docs/decisions/0017-damage.md</c>).
/// </summary>
public enum SpecialDamageType
{
    /// <summary>
    /// Ch 6, p.148: "A wound resulting in a deep tissue cut into arteries or major organs.
    /// Weapons with a sharp edge inflict bleeding damage." No weapon in the hand-picked subset
    /// uses this type (no edged slashing weapon, e.g. a sword, is in scope); base damage is
    /// normal (weapon dice + db), the ongoing 1 HP/round bleeding effect (p.149) is deferred.
    /// </summary>
    Bleeding,

    /// <summary>
    /// Ch 6, p.148: "A wound involving a blunt trauma to the victim, often breaking bones and
    /// stunning the target. Clubs, unarmed strikes, and other blunt weapons can cause crushing
    /// damage." Doubles the damage modifier on a special success (p.149) -- see
    /// <c>Brp.Rules.Combat.DamageResolver</c>; the stunning effect is deferred.
    /// </summary>
    Crushing,

    /// <summary>
    /// Ch 6, p.148: "Pinning or otherwise ensnaring the target's limbs or body. Flexible
    /// weapons, nets, ropes, and those with short, jagged points inflict entangling attacks." No
    /// weapon in the hand-picked subset uses this type. Base damage is normal; the entangling
    /// effect (p.151) is deferred.
    /// </summary>
    Entangling,

    /// <summary>
    /// Ch 6, p.148: "A deep wound piercing vital organs or passing entirely through the body of
    /// the target. Firearms, arrows, and other pointed weapons inflict impaling damage." Doubles
    /// the weapon's whole damage expression (dice and any fixed modifier) on a special success
    /// (p.150) -- see <c>Brp.Rules.Combat.DamageResolver</c>; the lodged-weapon/extraction
    /// mechanics are deferred.
    /// </summary>
    Impaling,

    /// <summary>
    /// Ch 6, p.148: "A wound that unbalances and possibly sends the target sprawling backwards.
    /// Some forms of unarmed attacks and shield attacks cause knockback." No weapon in the
    /// hand-picked subset uses this type. Base damage is normal; the knockback effect (p.151) is
    /// deferred.
    /// </summary>
    Knockback,
}
