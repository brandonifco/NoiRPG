namespace Brp.Rules.Advancement;

/// <summary>
/// Ruleset data <see cref="ExperienceSystem"/> reads for advancement parameters the book
/// treats as campaign-tunable, loaded the same way <c>Creation.CharacterCreationRuleset</c>
/// is (AGENTS.md invariant 7: rules values are data, not constants).
/// </summary>
public sealed class ExperienceRuleset
{
    /// <summary>Creates an experience ruleset from data-defined values.</summary>
    public ExperienceRuleset(int trainingCapPercent)
    {
        TrainingCapPercent = trainingCapPercent;
    }

    /// <summary>
    /// Ch 5: System, "Skill Training" (p.139): "No skill can be trained above 75%, no matter
    /// how good the instructor. Any increase above this must come through successful use of
    /// the skill." Read by <see cref="ExperienceSystem.Teach"/> to cap a training gain.
    /// <para>
    /// Deliberately a separate field from
    /// <c>Creation.CharacterCreationRuleset.StartingSkillCapPercent</c> even though the book
    /// prints 75% for both at Normal power level: that field gates what a skill may start at
    /// during character creation (Ch 2 p.8); this one gates what teaching alone may raise an
    /// existing skill to during play (Ch 5 p.139). They are two different rules that happen
    /// to share a number, not one rule read from two places -- a campaign that changes power
    /// level, for instance, can move one without moving the other.
    /// </para>
    /// </summary>
    public int TrainingCapPercent { get; }
}
