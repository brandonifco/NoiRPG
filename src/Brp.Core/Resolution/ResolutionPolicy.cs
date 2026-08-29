using Brp.Core.Primitives;

namespace Brp.Core.Resolution;

/// <summary>
/// The constants that define how an action roll is graded.
/// <para>
/// These live here rather than as literals scattered through the resolver so that every
/// value taken from the book is declared in one named place, per the rules-values-as-data
/// invariant in <c>AGENTS.md</c>. They are deliberately <em>not</em> in ruleset JSON
/// alongside skill and weapon data: those describe a setting, whereas these describe the
/// resolution system itself. Changing <see cref="SpecialDivisor"/> from 5 to 4 does not
/// produce a different setting, it produces a different game.
/// </para>
/// <para>
/// Treat <see cref="Standard"/> as the only supported configuration. The type exists to
/// make the values explicit and testable, not to invite tuning.
/// </para>
/// </summary>
/// <param name="CriticalDivisor">
/// A critical succeeds on a roll at or under this fraction of the chance, rounded up
/// (Ch 5: System, "Critical Success", p.128 -- one twentieth).
/// </param>
/// <param name="SpecialDivisor">
/// A special succeeds on a roll at or under this fraction of the chance, rounded up
/// (Ch 5, "Special Success", p.128 -- one fifth).
/// </param>
/// <param name="AlwaysFailsAtOrAbove">
/// Rolls at or above this value always fail regardless of chance (Ch 5, "Failure", p.127).
/// Note this rule is stated for action and skill rolls; resistance rolls are explicitly
/// exempted -- see the remarks on <see cref="SkillResolver"/>.
/// </param>
/// <param name="FumbleBandAnchor">
/// The fumble band starts at this value plus the (clamped) critical threshold, and always
/// ends at 100. Derived from the printed Skill Results Table rather than from the prose,
/// which is off by one at multiples of 20 -- see <see cref="SkillResolver"/>.
/// </param>
/// <param name="MinimumSuccessFloorChance">
/// A skill whose <em>base</em> chance is at least this value always succeeds on a roll at
/// or under <see cref="MinimumSuccessFloorRoll"/>, however far modifiers have pushed the
/// effective chance down (Ch 5, "Skill Rolls", p.128).
/// </param>
/// <param name="MinimumSuccessFloorRoll">The highest roll the 5% floor rescues.</param>
public sealed record ResolutionPolicy(
    int CriticalDivisor,
    int SpecialDivisor,
    int AlwaysFailsAtOrAbove,
    int FumbleBandAnchor,
    int MinimumSuccessFloorChance,
    int MinimumSuccessFloorRoll)
{
    /// <summary>The values printed in the source book. The only supported configuration.</summary>
    public static ResolutionPolicy Standard { get; } = new(
        CriticalDivisor: 20,
        SpecialDivisor: 5,
        AlwaysFailsAtOrAbove: 96,
        FumbleBandAnchor: 95,
        MinimumSuccessFloorChance: 5,
        MinimumSuccessFloorRoll: 5);

    /// <summary>Highest possible percentile result. A roll of 00 is read as 100.</summary>
    public const int MaximumRoll = 100;

    /// <summary>The critical threshold for a given effective chance.</summary>
    public int CriticalThreshold(Percent effectiveChance) =>
        Rounding.Divide(effectiveChance.Value, CriticalDivisor, RoundingMode.Up);

    /// <summary>The special threshold for a given effective chance.</summary>
    public int SpecialThreshold(Percent effectiveChance) =>
        Rounding.Divide(effectiveChance.Value, SpecialDivisor, RoundingMode.Up);

    /// <summary>
    /// The lowest roll that fumbles. The band always ends at 100 and narrows by one for each
    /// step the critical threshold takes, collapsing to 100 alone once critical reaches 5.
    /// Clamping critical to at least 1 anchors the start at 96 even at chance 0, matching the
    /// table's lowest printed row rather than drifting below it.
    /// </summary>
    public int FumbleThreshold(Percent effectiveChance) =>
        Math.Min(MaximumRoll, FumbleBandAnchor + Math.Max(1, CriticalThreshold(effectiveChance)));
}
