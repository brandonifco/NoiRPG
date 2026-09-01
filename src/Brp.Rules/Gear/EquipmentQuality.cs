using Brp.Core.Modifiers;
using Brp.Core.Skills;

namespace Brp.Rules.Gear;

/// <summary>
/// Ch 8: Equipment, "Equipment Quality Modifiers" (p.185): "The quality of equipment can provide
/// a modifier to a skill roll, as described in ** Situational Modifiers**. This modifier can
/// range from inferior equipment penalizing your character's skill rating by -20%, to superior
/// quality equipment offering a +20% bonus." The book names this as a situational modifier
/// itself, so it is produced as an <see cref="AdditiveKind.Situational"/> <see cref="AdditiveModifier"/>
/// and flows through the existing <see cref="ModifierPipeline"/> exactly like every other
/// situational source (ADR 0007) -- no parallel modifier path. p.185 restates the Ch 5 ordering
/// rule explicitly: "Remember that situational modifiers to a skill rating are applied after an
/// Easy modifier doubles it or Difficult divides it in half."
/// <para>
/// <strong>Coupling with the Skills-and-Equipment mapping (sourced, p.185-186):</strong> "Many
/// skills require equipment to be used successfully, or are greatly enhanced with equipment...
/// If the skill is not listed, it does not require any equipment, or it is obvious." A quality
/// modifier is therefore only meaningful for a skill the mapping actually lists --
/// <see cref="ModifierForSkill"/> is the gated entry point that enforces this; <see cref="Modifier"/>
/// is the ungated primitive it is built from.
/// </para>
/// </summary>
public static class EquipmentQuality
{
    /// <summary>
    /// Builds the situational modifier a piece of equipment's quality tier contributes to a
    /// skill roll. Returns a zero-delta modifier for <see cref="EquipmentQualityTier.Average"/>
    /// rather than <see langword="null"/> -- the book's "Average ... None" is a defined, present
    /// answer, not an absent one -- so a caller can always add the result to its modifier list
    /// without a conditional, matching the precedent set for zero-delta additive modifiers
    /// elsewhere in this engine.
    /// </summary>
    public static AdditiveModifier Modifier(EquipmentQualityTier tier, string equipmentLabel, EquipmentQualityRuleset ruleset)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(equipmentLabel);
        ArgumentNullException.ThrowIfNull(ruleset);

        var delta = tier switch
        {
            EquipmentQualityTier.Inferior => ruleset.InferiorDelta,
            EquipmentQualityTier.Average => 0,
            EquipmentQualityTier.Superior => ruleset.SuperiorDelta,
            _ => throw new ArgumentOutOfRangeException(nameof(tier), tier, "Unknown equipment quality tier."),
        };

        var label = tier switch
        {
            EquipmentQualityTier.Inferior => "inferior",
            EquipmentQualityTier.Average => "average",
            EquipmentQualityTier.Superior => "superior",
            _ => throw new ArgumentOutOfRangeException(nameof(tier), tier, "Unknown equipment quality tier."),
        };

        return new AdditiveModifier($"{equipmentLabel} ({label} quality)", delta, AdditiveKind.Situational);
    }

    /// <summary>
    /// Builds the quality modifier for a skill roll, gated by the Skills-and-Equipment mapping
    /// (p.185-186). Throws if <paramref name="skillId"/> is not a skill the mapping lists
    /// equipment for -- the book's own rule for such a skill is that equipment "does not [...]
    /// or it is obvious", so a caller asking for a quality modifier there is a caller bug, not a
    /// silent zero-modifier no-op.
    /// </summary>
    public static AdditiveModifier ModifierForSkill(
        SkillId skillId,
        EquipmentQualityTier tier,
        string equipmentLabel,
        EquipmentQualityRuleset qualityRuleset,
        SkillEquipmentRuleset skillEquipmentRuleset)
    {
        ArgumentNullException.ThrowIfNull(skillEquipmentRuleset);
        if (!skillEquipmentRuleset.UsesEquipment(skillId))
        {
            throw new ArgumentException(
                $"'{skillId}' is not a skill the Skills-and-Equipment mapping (Ch 8, p.185-186) lists equipment for.",
                nameof(skillId));
        }

        return Modifier(tier, equipmentLabel, qualityRuleset);
    }
}
