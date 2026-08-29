using Brp.Core.Dice;

namespace Brp.Core.Abilities;

/// <summary>One row of Ch 2: Characters, "Damage Modifier Table" (p.13).</summary>
public sealed record DamageModifierBand(
    int Minimum,
    int? Maximum,
    DiceExpression? Modifier,
    DamageModifierContinuation? Continuation = null)
{
    /// <summary>Whether this band contains a STR+SIZ total.</summary>
    public bool Contains(int total) => total >= Minimum && (Maximum is null || total <= Maximum.Value);
}

/// <summary>Data for the open-ended "Each +16: Additional +1D6" row on Ch 2 p.13.</summary>
public sealed record DamageModifierContinuation(int Step, int StartingDiceCount, int DiceSides, int DiceCountIncrease)
{
    /// <summary>Creates the expression for the given total within its owning band.</summary>
    public DiceExpression ExpressionAt(int total, int bandMinimum)
    {
        var steps = (total - bandMinimum) / Step;
        return DiceExpression.Parse($"{StartingDiceCount + (steps * DiceCountIncrease)}D{DiceSides}");
    }
}
