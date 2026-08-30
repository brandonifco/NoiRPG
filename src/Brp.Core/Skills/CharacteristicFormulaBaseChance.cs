using Brp.Core.Abilities;
using Brp.Core.Primitives;

namespace Brp.Core.Skills;

/// <summary>
/// A base chance computed from one or more characteristics, summed as printed percentage
/// points. A single term reproduces a multiple, e.g. Ch 3: Skills, "Dodge" (p.37), "Base
/// Chance: DEX×2". Two terms with multiplier 1 reproduce a sum, e.g. "Gaming" (p.40), "Base
/// Chance: INT+POW%". Every in-scope formula is a sum of straight multiples -- none divide --
/// so no <see cref="Rounding"/> mode applies here; that only becomes relevant for out-of-scope
/// skills such as "Fly" (½ DEX).
/// </summary>
public sealed record CharacteristicFormulaBaseChance(IReadOnlyList<CharacteristicTerm> Terms) : BaseChanceExpression
{
    /// <summary>Creates a formula from an inline list of terms.</summary>
    public CharacteristicFormulaBaseChance(params CharacteristicTerm[] terms)
        : this((IReadOnlyList<CharacteristicTerm>)terms)
    {
    }

    /// <inheritdoc />
    public override Percent Evaluate(AbilitySet abilities)
    {
        ArgumentNullException.ThrowIfNull(abilities);
        var total = Terms.Sum(term => abilities.ValueOf(term.Characteristic) * term.Multiplier);
        return Percent.Of(total);
    }
}
