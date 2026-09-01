namespace Brp.Rules.Gear;

/// <summary>
/// The three quality tiers Ch 8: Equipment, "Equipment Quality Modifiers" (p.185) defines for
/// task-relevant gear -- lockpicks, a research library, a medical lab, or "anything else that is
/// useful and appropriate." Average is the default ("Most equipment your character uses is by
/// default of average quality"); a character opts into Inferior or Superior gear as a deliberate
/// choice.
/// </summary>
public enum EquipmentQualityTier
{
    /// <summary>"Inferior ... -20% ... Subtract one to three value levels." (p.185)</summary>
    Inferior,

    /// <summary>"Average ... None ... As normal." (p.185) The default for unspecified gear.</summary>
    Average,

    /// <summary>"Superior ... +20% ... Add one to three value levels." (p.185)</summary>
    Superior,
}
