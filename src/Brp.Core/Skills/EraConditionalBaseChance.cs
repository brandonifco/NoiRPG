using Brp.Core.Abilities;
using Brp.Core.Primitives;

namespace Brp.Core.Skills;

/// <summary>
/// A base chance printed as an either/or pair, one value used and the other set aside.
/// Ch 3: Skills, "Drive (various)" (p.37): "Base Chance: 20% or 01% (see below)... For
/// common vehicles, the base chance is 20%, for unknown/uncommon vehicles, it's 01%."
/// <para>
/// NoiRPG always evaluates to <see cref="Modern"/>. This is a house rule generalizing the
/// project's era policy (<c>AGENTS.md</c> invariant 4, "Modern era baselines, not
/// historical") to this printed common/uncommon split: cars are the modern-era common
/// case, so Drive's 20% is what "modern" means here even though the book's own axis for
/// this particular skill is vehicle familiarity rather than a historical/modern pair.
/// <see cref="Historical"/> is retained for provenance and is never evaluated by this type.
/// </para>
/// </summary>
public sealed record EraConditionalBaseChance(BaseChanceExpression Modern, BaseChanceExpression Historical) : BaseChanceExpression
{
    /// <inheritdoc />
    public override Percent Evaluate(AbilitySet abilities)
    {
        ArgumentNullException.ThrowIfNull(abilities);
        return Modern.Evaluate(abilities);
    }
}
