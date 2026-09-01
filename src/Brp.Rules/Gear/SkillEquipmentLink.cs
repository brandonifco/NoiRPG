using Brp.Core.Skills;

namespace Brp.Rules.Gear;

/// <summary>
/// One row of Ch 8: Equipment, "Skills and Equipment" (pp.185-186): "The Skills &amp; Equipment
/// table describes potential specialized or general equipment to use with skills. If the skill
/// is not listed, it does not require any equipment, or it is obvious (such as weapon skills)."
/// A skill's absence from <see cref="SkillEquipmentRuleset"/> is therefore the book's own "no
/// equipment relevant here" case, not an omission.
/// </summary>
/// <param name="SkillId">The engine's skill id (docs/decisions/0011-skill-definition.md).</param>
/// <param name="PotentialEquipment">
/// The book's "Potential Equipment" text for this skill, hand-picked to the modern noir subset
/// per <c>orc-scope-filter.md</c>, Ch 8 ("hand-pick the entries a noir detective could plausibly
/// encounter"): pre-modern items (astrolabes, herbalist kits) are dropped in favor of a modern
/// phrasing, per AGENTS.md invariant 4 (modern era baselines).
/// </param>
public sealed record SkillEquipmentLink(SkillId SkillId, string PotentialEquipment);
