using Brp.Core.Primitives;

namespace Brp.Core.Abilities;

/// <summary>
/// A roll against one characteristic at a variable multiplier, per Ch 2: Characters,
/// "Characteristic Rolls" (pp.11-12). Ch 7: Spot Rules, "Disease" (p.170) also varies a
/// CON roll from x1 upward, so this type represents x1 through x10 rather than only x5.
/// </summary>
public readonly record struct CharacteristicRoll
{
    /// <summary>Creates a characteristic roll.</summary>
    internal CharacteristicRoll(CharacteristicId characteristic, int multiplier)
    {
        Characteristic = characteristic;
        Multiplier = multiplier;
    }

    /// <summary>The characteristic being tested.</summary>
    public CharacteristicId Characteristic { get; }

    /// <summary>The multiplier selected for this action.</summary>
    public int Multiplier { get; }

    /// <summary>Calculates the unmodified roll chance.</summary>
    public Percent ChanceFor(int characteristicValue) => Percent.Of(checked(characteristicValue * Multiplier));
}
