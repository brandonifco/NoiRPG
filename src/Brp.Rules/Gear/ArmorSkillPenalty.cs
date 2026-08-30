using Brp.Core.Skills;

namespace Brp.Rules.Gear;

/// <summary>
/// The skill penalty an <see cref="ArmorDefinition"/> imposes while worn. Sourced: Ch 8:
/// Equipment, Modern Armor table (p.207), "Skill Modifier" column -- every entry in the
/// hand-picked subset penalizes Physical skills, matching Ch 3's category list
/// (<see cref="SkillCategory"/>).
/// </summary>
/// <param name="Category">The skill category the penalty applies to.</param>
/// <param name="PercentPenalty">The penalty, as a negative percentage (e.g. <c>-10</c> for "-10% to Physical skills").</param>
public sealed record ArmorSkillPenalty(SkillCategory Category, int PercentPenalty);
