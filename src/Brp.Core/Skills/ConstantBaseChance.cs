using Brp.Core.Abilities;
using Brp.Core.Primitives;

namespace Brp.Core.Skills;

/// <summary>
/// A flat base chance that does not depend on any characteristic, e.g. Ch 3: Skills,
/// "Spot" (p.50), "Base Chance: 25%".
/// </summary>
public sealed record ConstantBaseChance(Percent Value) : BaseChanceExpression
{
    /// <inheritdoc />
    public override Percent Evaluate(AbilitySet abilities) => Value;

    /// <inheritdoc />
    public override bool TryEvaluateWithoutAbilities(out Percent value)
    {
        value = Value;
        return true;
    }
}
