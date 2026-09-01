namespace Brp.Rules.Advancement;

/// <summary>
/// Ruleset data <see cref="ExperienceSystem"/> reads for advancement parameters the book
/// treats as campaign-tunable, loaded the same way <c>Creation.CharacterCreationRuleset</c>
/// is (AGENTS.md invariant 7: rules values are data, not constants).
/// </summary>
public sealed class ExperienceRuleset
{
    /// <summary>Creates an experience ruleset from data-defined values.</summary>
    public ExperienceRuleset(
        int trainingCapPercent,
        int researchGainDieSides = 6,
        int researchGainOffset = -2,
        int researchDefaultGain = 2)
    {
        TrainingCapPercent = trainingCapPercent;
        ResearchGainDieSides = researchGainDieSides;
        ResearchGainOffset = researchGainOffset;
        ResearchDefaultGain = researchDefaultGain;
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

    /// <summary>
    /// Ch 5: System, "Researching" (p.139): the gain die research rolls after a successful
    /// experience roll, "1D6-2 points." Unlike <see cref="ExperienceSystem.ImprovementRoll"/>'s
    /// <c>gainDieSides</c> (which the book explicitly scales to 1D8/1D10 for epic/superhuman
    /// campaigns), research's die is printed as a fixed 1D6 with no such scaling clause -- so
    /// this is ruleset data a campaign could still override (AGENTS.md invariant 7), but
    /// <see cref="ExperienceSystem.Research"/> itself takes no caller-supplied override the way
    /// <see cref="ExperienceSystem.Teach"/> does for its own dice.
    /// </summary>
    public int ResearchGainDieSides { get; }

    /// <summary>
    /// Ch 5: System, "Researching" (p.139): the fixed offset subtracted from the research gain
    /// die -- "1D6-2 points." Read by <see cref="ExperienceSystem.Research"/> together with
    /// <see cref="ResearchGainDieSides"/>.
    /// </summary>
    public int ResearchGainOffset { get; }

    /// <summary>
    /// Ch 5: System, "Researching" (p.139): the flat alternative a researcher may announce
    /// before rolling instead of drawing the gain die -- "or choose to add 2 to the current
    /// skill rating." Distinct from <see cref="ExperienceSystem.DefaultGain"/> (the general
    /// "+3 instead of rolling" option for the ordinary experience-roll gain, p.138), which is
    /// half the gain die's maximum and does not apply here -- research's default is this
    /// separately book-printed flat 2, not a formula.
    /// </summary>
    public int ResearchDefaultGain { get; }
}
