using Brp.Core.Abilities;

namespace Brp.Core.Skills;

/// <summary>
/// One term of a <see cref="CharacteristicFormulaBaseChance"/>: a characteristic's value
/// times a multiplier, e.g. the <c>DEX</c> in <c>DEX×2</c>.
/// </summary>
public sealed record CharacteristicTerm(CharacteristicId Characteristic, int Multiplier);
