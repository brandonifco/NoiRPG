using Brp.Core.Skills;

namespace Brp.Rules.Gear;

/// <summary>
/// The Ch 8: Equipment, "Skills and Equipment" table (pp.185-186), hand-picked to the noir-
/// relevant subset (<c>orc-scope-filter.md</c>). Couples with <see cref="EquipmentQuality"/>: this
/// mapping answers "does gear help this skill roll at all", and <see cref="EquipmentQuality"/>
/// supplies the numeric answer once this mapping says yes -- see
/// <see cref="EquipmentQuality.ModifierForSkill"/>.
/// </summary>
public sealed class SkillEquipmentRuleset
{
    private readonly Dictionary<string, SkillEquipmentLink> _linksBySkillId;

    /// <summary>Creates a ruleset from data-defined links.</summary>
    public SkillEquipmentRuleset(IEnumerable<SkillEquipmentLink> links)
    {
        ArgumentNullException.ThrowIfNull(links);

        _linksBySkillId = links.ToDictionary(link => link.SkillId.Value, StringComparer.OrdinalIgnoreCase);
        if (_linksBySkillId.Count == 0)
        {
            throw new ArgumentException("At least one skill-equipment link is required.", nameof(links));
        }
    }

    /// <summary>Every skill the book lists potential equipment for, keyed by skill id.</summary>
    public IReadOnlyDictionary<string, SkillEquipmentLink> Links => _linksBySkillId;

    /// <summary>
    /// True when <paramref name="skillId"/> is one the book's Skills &amp; Equipment table lists
    /// potential equipment for. Per p.185, an unlisted skill "does not require any equipment, or
    /// it is obvious", so <see langword="false"/> here is the expected, non-error outcome for a
    /// skill such as Brawl or Persuade.
    /// </summary>
    public bool UsesEquipment(SkillId skillId) => _linksBySkillId.ContainsKey(skillId.Value);

    /// <summary>Looks up the equipment link for a skill, or <see langword="null"/> if it is not listed.</summary>
    public SkillEquipmentLink? TryGetLink(SkillId skillId) =>
        _linksBySkillId.TryGetValue(skillId.Value, out var link) ? link : null;
}
