using Brp.Core.Skills;

namespace Brp.Rules.Advancement;

/// <summary>
/// Tracks which skills have already earned an experience check during the current case, so
/// <see cref="ExperienceSystem.RecordUse"/> can enforce Ch 5: System, "Skill Improvement"
/// (p.138): "An experience check for a particular skill is made only once per adventure, no
/// matter how many times the skill is successfully used." One ledger is scoped to one case;
/// create a fresh one when a new case opens.
/// </summary>
public sealed class CaseExperienceLedger
{
    private readonly HashSet<SkillId> _ticked = [];

    /// <summary>Whether this skill has already recorded a tick this case.</summary>
    public bool HasTicked(SkillId id) => _ticked.Contains(id);

    /// <summary>
    /// Records a tick for this skill if it has not already ticked this case.
    /// Returns <see langword="true"/> if this call newly recorded the tick.
    /// </summary>
    public bool TryTick(SkillId id) => _ticked.Add(id);
}
