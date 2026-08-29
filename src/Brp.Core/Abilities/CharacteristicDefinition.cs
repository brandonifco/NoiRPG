namespace Brp.Core.Abilities;

/// <summary>
/// A characteristic definition supplied by a ruleset. Bounds and roll names are data because
/// Ch 2: Characters, "Characteristics" and "Characteristic Rolls" (pp.10-12) define them.
/// </summary>
public sealed record CharacteristicDefinition(
    CharacteristicId Id,
    string DisplayName,
    int Minimum,
    int? Maximum,
    string? RollName)
{
    /// <summary>Whether Ch 2 associates a named characteristic roll with this value.</summary>
    public bool HasCharacteristicRoll => RollName is not null;
}
