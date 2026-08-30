namespace Brp.Rules.Advancement;

/// <summary>
/// What a skill check was made under, for <see cref="ExperienceSystem"/>'s mechanical
/// no-stakes gate. Ch 5: System, "Skill Improvement" (p.138): "If a skill roll was Easy, no
/// experience check is allowed" and "[the gamemaster] should almost always allow experience
/// checks whenever skills are successfully used in stressful situations. An attack against a
/// helpless target is not a stressful situation and does not deserve an experience check."
/// The book leaves that second judgment to a gamemaster; NoiRPG has none at runtime, so the
/// scenario/resolution layer (not this type) is responsible for classifying every check
/// before it reaches <see cref="ExperienceSystem"/> -- this enum only records the result of
/// that classification.
/// </summary>
public enum CheckStakes
{
    /// <summary>Nothing consequential rode on this check -- never eligible for a tick.</summary>
    NoStakes,

    /// <summary>Ch 5 p.138's Easy exemption -- never eligible for a tick.</summary>
    Easy,

    /// <summary>A genuine, consequential check -- eligible for a tick per <see cref="ExperiencePolicy"/>.</summary>
    RealStakes,
}
