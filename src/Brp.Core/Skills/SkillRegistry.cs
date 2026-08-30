namespace Brp.Core.Skills;

/// <summary>
/// The set of resolvable skills for a ruleset, keyed by the framework's canonical
/// <see cref="SkillId"/> (`orc-scope-filter.md`, "Skill naming: the framework's names
/// win"). Loaded from data by <c>Brp.Data.NoirSkillRuleset.Load()</c> -- the skill list
/// itself is not hardcoded here, per AGENTS.md invariant 7.
/// </summary>
public sealed class SkillRegistry
{
    private readonly Dictionary<SkillId, SkillDefinition> _skills;

    /// <summary>Creates a registry from a data-defined skill list.</summary>
    public SkillRegistry(IEnumerable<SkillDefinition> skills)
    {
        ArgumentNullException.ThrowIfNull(skills);
        _skills = skills.ToDictionary(skill => skill.Id);
        if (_skills.Count == 0)
        {
            throw new ArgumentException("At least one skill definition is required.", nameof(skills));
        }
    }

    /// <summary>Every resolvable skill in this registry, by canonical id.</summary>
    public IReadOnlyDictionary<SkillId, SkillDefinition> Skills => _skills;

    /// <summary>Looks up a skill by its canonical id, throwing if it is not defined.</summary>
    public SkillDefinition this[SkillId id] => _skills.TryGetValue(id, out var definition)
        ? definition
        : throw new KeyNotFoundException($"Unknown skill '{id}'.");

    /// <summary>Looks up a skill by its canonical id without throwing.</summary>
    public bool TryGetSkill(SkillId id, out SkillDefinition? definition) => _skills.TryGetValue(id, out definition);
}
