using Brp.Core.Abilities;
using Brp.Core.Primitives;

namespace Brp.Core.Skills;

/// <summary>
/// A base chance the skill definition cannot supply on its own. Ch 3: Skills, "Firearm
/// (various)" (p.39) and "Melee Weapon (various)" (p.46): "Base Chance: As per weapon
/// specialty" / "As per weapon". The printed value lives on the weapon, not the skill, and
/// weapon data is out of scope here -- it is Layer 4 (#21). This type is a placeholder that
/// records the shape (a weapon-derived skill exists and needs an externally-supplied base
/// chance) without inventing the weapon data that would fill it in.
/// </summary>
public sealed record WeaponDerivedBaseChance : BaseChanceExpression
{
    /// <summary>
    /// Always throws. A weapon-derived base chance has no value until a weapon supplies one;
    /// that composition is Layer 4's concern, not this type's.
    /// </summary>
    public override Percent Evaluate(AbilitySet abilities) =>
        throw new InvalidOperationException(
            "This skill's base chance is weapon-derived (Ch 3: Skills, \"as per weapon specialty\") " +
            "and must be supplied externally by weapon data (Layer 4, #21); it has no standalone value.");
}
