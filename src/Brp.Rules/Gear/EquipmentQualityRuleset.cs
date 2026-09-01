namespace Brp.Rules.Gear;

/// <summary>
/// Data-defined percentages for Ch 8: Equipment, "Equipment Quality Modifiers" (p.185).
/// AGENTS.md invariant 7 (rules values are data, not constants): the two deltas are loaded from
/// <c>equipment-quality-ruleset.json</c> by <c>Brp.Data.NoirEquipmentQualityRuleset.Load()</c>
/// rather than hardcoded in <see cref="EquipmentQuality"/>.
/// </summary>
public sealed class EquipmentQualityRuleset
{
    /// <summary>Creates a ruleset from data-defined values.</summary>
    public EquipmentQualityRuleset(int inferiorDelta, int superiorDelta)
    {
        InferiorDelta = inferiorDelta;
        SuperiorDelta = superiorDelta;
    }

    /// <summary>Ch 8, "Equipment Quality Modifiers" (p.185): "Inferior ... -20%."</summary>
    public int InferiorDelta { get; }

    /// <summary>Ch 8, "Equipment Quality Modifiers" (p.185): "Superior ... +20%."</summary>
    public int SuperiorDelta { get; }
}
