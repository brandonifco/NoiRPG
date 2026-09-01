namespace Brp.Rules.Wealth;

/// <summary>
/// One row of Ch 3: Skills, "Status Skill, Social Status, &amp; Character Wealth" -- the
/// "Victorian/Western/Pulp/Modern Status" table (p.51), the era table NoiRPG uses (AGENTS.md
/// invariant 4: modern-era baselines, not historical; see <c>docs/decisions/0030-money-and-wealth-levels.md</c>).
/// Banded by the character's <c>Status</c> skill rating (Ch 3, p.51, "Base Chance: 15%"). Mirrors
/// <see cref="Combat.MajorWoundRow"/> / <see cref="Combat.IllnessSeverityBand"/> in shape.
/// </summary>
/// <param name="MinimumStatus">The lowest Status rating this row covers.</param>
/// <param name="MaximumStatus">The highest Status rating this row covers (a printed 00 is 100).</param>
/// <param name="SocialRank">The printed social rank for this row (e.g. "Lower Class", "Nobility").</param>
/// <param name="WealthRating">The typical <see cref="WealthLevel"/> for a character with this Status.</param>
/// <param name="MaximumWealth">
/// The printed "Wealth Cap" -- the highest <see cref="WealthLevel"/> a character with this Status can
/// hold.
/// </param>
public sealed record WealthBand(
    int MinimumStatus,
    int MaximumStatus,
    string SocialRank,
    WealthLevel WealthRating,
    WealthLevel MaximumWealth)
{
    /// <summary>Whether this row covers the given Status rating.</summary>
    public bool Contains(int status) => status >= MinimumStatus && status <= MaximumStatus;
}
