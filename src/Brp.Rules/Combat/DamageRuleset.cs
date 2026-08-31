using Brp.Core.Dice;

namespace Brp.Rules.Combat;

/// <summary>
/// The data-defined thresholds and parameters <see cref="DamageResolver"/> reads (AGENTS.md
/// invariant 7: rules values are data, not constants). Loaded from
/// <c>damage-ruleset.json</c> by <c>Brp.Data.NoirDamageRuleset.Load()</c>. See
/// <c>docs/decisions/0017-damage.md</c>.
/// <para>
/// Deliberately does <em>not</em> carry a "half of maximum HP" fraction for major-wound
/// classification: that figure already exists, tested, at Layer 1 as
/// <see cref="Core.Abilities.AbilitySet.MajorWoundLevel"/> (Ch 2, p.14, rounded up), which this
/// resolver reuses directly rather than re-deriving the same figure a second time from a second
/// piece of ruleset data.
/// </para>
/// </summary>
public sealed class DamageRuleset
{
    /// <summary>Creates a damage ruleset from data-defined values.</summary>
    public DamageRuleset(
        int unconsciousHitPointLevel,
        int deadHitPointLevel,
        DiceExpression knockoutDuration,
        DiceExpression crushingNoModifierBonus)
    {
        ArgumentNullException.ThrowIfNull(knockoutDuration);
        ArgumentNullException.ThrowIfNull(crushingNoModifierBonus);
        if (unconsciousHitPointLevel < deadHitPointLevel)
        {
            throw new ArgumentException(
                "The unconscious threshold must not be below the dead threshold.", nameof(unconsciousHitPointLevel));
        }

        UnconsciousHitPointLevel = unconsciousHitPointLevel;
        DeadHitPointLevel = deadHitPointLevel;
        KnockoutDuration = knockoutDuration;
        CrushingNoModifierBonus = crushingNoModifierBonus;
    }

    /// <summary>
    /// Ch 2: Characters, "Hit Points" (p.13): "Your character loses consciousness when their
    /// hit points are reduced to 2 or less." A character at or below this level (but above
    /// <see cref="DeadHitPointLevel"/>) is unconscious.
    /// </summary>
    public int UnconsciousHitPointLevel { get; }

    /// <summary>
    /// Ch 2: Characters, "Hit Points" (p.13) / Ch 6: Combat, "Fatal Wound" (p.156): a character
    /// at or below this level has suffered a fatal wound and dies at the end of the following
    /// round unless restored above it in time -- see
    /// <see cref="DamageResolver.ResolvesToDeath"/> for the timing seam this resolver models.
    /// </summary>
    public int DeadHitPointLevel { get; }

    /// <summary>
    /// Ch 7: Spot Rules, "Knockout Attacks" (p.174): "knocked out for 1D10+10 rounds."
    /// </summary>
    public DiceExpression KnockoutDuration { get; }

    /// <summary>
    /// Ch 6: Combat, "Crushing" (p.149): "if there is no damage modifier, it becomes +1D4" --
    /// the flat bonus a Crushing special success uses in place of a doubled damage modifier when
    /// the attacker has none.
    /// </summary>
    public DiceExpression CrushingNoModifierBonus { get; }
}
