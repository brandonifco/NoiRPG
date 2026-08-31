namespace Brp.Core.Contests;

/// <summary>
/// The named gamemaster-adjudication points the Ch 6: Combat, "Healing Naturally" / "Conditions of
/// Medical Care" rules (p.157) and the First Aid / Medicine skill descriptions (Ch 3, pp.39, 46)
/// leave open. The healing sibling of <see cref="IInjuryAdjudicator"/> (the Ch 7 injury spot rules,
/// #96) and <see cref="IMajorWoundAdjudicator"/> (Ch 6 Major Wounds, #111): each member is a call
/// the healing rules hand to the gamemaster -- which environmental care tier the patient is under,
/// and who provides the care (and therefore which skill they roll) -- rather than resolving
/// mechanically. Naming them as first-class ids keeps <c>HealingResolver</c> from silently
/// hardcoding these calls. The canonical kebab-case id for each is given in its summary and returned
/// by <see cref="HealingDecisionIds.CanonicalId"/>. See <c>docs/decisions/0023-healing-and-recovery.md</c>.
/// </summary>
public enum HealingDecisionId
{
    /// <summary>
    /// Canonical id <c>healing-conditions-tier</c>. Ch 6, "Conditions of Medical Care" (p.157): the
    /// table "offers guidelines for various conditions and the effect on the healing rate," but which
    /// of the three tiers (poor, decent, excellent) a patient's actual environment falls in is a
    /// gamemaster judgment of the fiction (sanitation, rest, exertion, quality of care). A pre-healing
    /// ruling: it selects the <see cref="Contests.MedicalCareTier"/> row whose effect the resolver
    /// applies.
    /// </summary>
    ConditionsTier,

    /// <summary>
    /// Canonical id <c>healing-caregiver</c>. Ch 6, "Conditions of Medical Care" (p.157): the poor-tier
    /// roll is made by the "Caregiver (doctor, nurse, healer, self, etc.)," and the caregiver "must
    /// succeed in a Difficult First Aid or Medicine roll." Who the caregiver is -- and, consequently,
    /// whether they apply the First Aid or the Medicine skill (each a legitimate reading of the printed
    /// "First Aid or Medicine roll") -- is a gamemaster/narrative call. A pre-roll ruling: it selects
    /// which skill's printed base chance the caregiver's roll resolves against.
    /// </summary>
    Caregiver,
}

/// <summary>Canonical kebab-case ids for the <see cref="HealingDecisionId"/> ports.</summary>
public static class HealingDecisionIds
{
    /// <summary>
    /// The canonical kebab-case id for <paramref name="decisionId"/> -- the stable string a GM tool,
    /// authored policy, or log keys on (e.g. <c>healing-conditions-tier</c>), matching the ids named
    /// in Issue #109 and ADR 0023.
    /// </summary>
    public static string CanonicalId(HealingDecisionId decisionId) => decisionId switch
    {
        HealingDecisionId.ConditionsTier => "healing-conditions-tier",
        HealingDecisionId.Caregiver => "healing-caregiver",
        _ => throw new ArgumentOutOfRangeException(nameof(decisionId), decisionId, "Unknown healing decision id."),
    };
}

/// <summary>
/// The environmental care tier of Ch 6, "Conditions of Medical Care" (p.157), selected by the
/// <see cref="HealingDecisionId.ConditionsTier"/> ruling. The three printed rows, worst to best.
/// </summary>
public enum MedicalCareTier
{
    /// <summary>
    /// Poorly equipped, unsanitary, or stressful conditions; a patient mobile and exerting heavily; or
    /// no medical care at all. Requires a Difficult First Aid or Medicine roll for any healing (p.157).
    /// </summary>
    Poor,

    /// <summary>Decent, sanitary, restful conditions with care and only moderate exertion: heals normally (p.157).</summary>
    Decent,

    /// <summary>
    /// Excellent conditions and equipment, full bedrest and therapy, full-time high-quality care: heals
    /// normally, and a further successful skill use allows possible additional healing (p.157).
    /// </summary>
    Excellent,
}

/// <summary>
/// Which skill a caregiver applies, for the <see cref="HealingDecisionId.Caregiver"/> ruling
/// (Ch 6, p.157: "a Difficult First Aid or Medicine roll"). The two skills have different printed
/// base chances, so the choice changes the 5%-floor behavior of the roll.
/// </summary>
public enum CaregiverSkill
{
    /// <summary>The caregiver applies the First Aid skill (Ch 3, p.39).</summary>
    FirstAid,

    /// <summary>The caregiver applies the Medicine skill (Ch 3, p.46).</summary>
    Medicine,
}

/// <summary>
/// The gamemaster's ruling on who provides care, for the <see cref="HealingDecisionId.Caregiver"/>
/// port (Ch 6, p.157).
/// </summary>
/// <param name="Skill">Which skill the caregiver applies to the healing roll.</param>
public readonly record struct CaregiverRuling(CaregiverSkill Skill);

/// <summary>
/// A gamemaster-discretion port for the Ch 6 healing rules, modeled -- like
/// <see cref="IInjuryAdjudicator"/> and <see cref="IMajorWoundAdjudicator"/> -- as a first-class
/// interface rather than a set of silent hardcoded choices. Each method answers one
/// <see cref="HealingDecisionId"/> the book leaves open. A GM tool can prompt a human; an unattended
/// simulation can supply an authored policy; tests supply a deterministic stub. The return types are
/// ordinary <c>Brp.Core</c> values so this port stays within <c>Brp.Core</c> and does not invert the
/// layer dependency (AGENTS.md invariant 6). See <c>docs/decisions/0023-healing-and-recovery.md</c>.
/// </summary>
public interface IHealingAdjudicator
{
    /// <summary>
    /// Decides which environmental care tier the patient is under
    /// (<see cref="HealingDecisionId.ConditionsTier"/>). Pre-healing.
    /// </summary>
    MedicalCareTier DecideConditionsTier();

    /// <summary>
    /// Decides who provides care and therefore which skill they roll
    /// (<see cref="HealingDecisionId.Caregiver"/>). Pre-roll.
    /// </summary>
    CaregiverRuling DecideCaregiver();
}

/// <summary>
/// The documented default policy for every <see cref="HealingDecisionId"/>: the most
/// minimal-assumption answer to a call the book leaves open, mirroring
/// <see cref="DefaultInjuryAdjudicator"/> and <see cref="DefaultMajorWoundAdjudicator"/>. A table with
/// a house rule or a human gamemaster should supply its own <see cref="IHealingAdjudicator"/> instead.
/// </summary>
public sealed class DefaultHealingAdjudicator : IHealingAdjudicator
{
    /// <summary>
    /// Defaults to <see cref="MedicalCareTier.Decent"/> -- the middle tier, which heals normally with
    /// no gating roll and asserts nothing about the environment beyond "ordinary care," the least
    /// assuming reading when the book does not say.
    /// </summary>
    public MedicalCareTier DecideConditionsTier() => MedicalCareTier.Decent;

    /// <summary>
    /// Defaults to <see cref="CaregiverSkill.FirstAid"/> -- the more broadly trained of the two skills
    /// (base chance 30% vs Medicine's 05%), the reading most tables mean by "someone renders aid."
    /// </summary>
    public CaregiverRuling DecideCaregiver() => new(CaregiverSkill.FirstAid);
}
