namespace Brp.Core.Abilities;

/// <summary>
/// Ruleset data consumed by <see cref="AbilitySet"/>. All book values are supplied by a data
/// project, per AGENTS.md; Ch 2: Characters supplies the characteristic definitions, damage
/// modifier table, and MOV starting value (pp.10-15).
/// </summary>
public sealed class AbilityRuleset
{
    private readonly Dictionary<CharacteristicId, CharacteristicDefinition> _characteristics;

    /// <summary>Creates an ability ruleset from data-defined values.</summary>
    public AbilityRuleset(
        IEnumerable<CharacteristicDefinition> characteristics,
        DamageModifierTable damageModifierTable,
        int startingMovement,
        int minimumCharacteristicRollMultiplier,
        int maximumCharacteristicRollMultiplier,
        int standardCharacteristicRollMultiplier,
        int hitPointDivisor,
        int majorWoundDivisor,
        int experienceBonusDivisor)
    {
        ArgumentNullException.ThrowIfNull(characteristics);
        ArgumentNullException.ThrowIfNull(damageModifierTable);
        _characteristics = characteristics.ToDictionary(c => c.Id);
        if (_characteristics.Count == 0)
        {
            throw new ArgumentException("At least one characteristic definition is required.", nameof(characteristics));
        }

        DamageModifierTable = damageModifierTable;
        StartingMovement = startingMovement;
        MinimumCharacteristicRollMultiplier = minimumCharacteristicRollMultiplier;
        MaximumCharacteristicRollMultiplier = maximumCharacteristicRollMultiplier;
        StandardCharacteristicRollMultiplier = standardCharacteristicRollMultiplier;
        HitPointDivisor = hitPointDivisor;
        MajorWoundDivisor = majorWoundDivisor;
        ExperienceBonusDivisor = experienceBonusDivisor;
    }

    /// <summary>The loaded characteristic definitions.</summary>
    public IReadOnlyDictionary<CharacteristicId, CharacteristicDefinition> Characteristics => _characteristics;

    /// <summary>Ch 2's data-backed damage modifier table.</summary>
    public DamageModifierTable DamageModifierTable { get; }

    /// <summary>Ch 2: Characters, "Movement (MOV)" (p.15) flat human starting value.</summary>
    public int StartingMovement { get; }

    /// <summary>Ch 2 p.12's lowest supported characteristic-roll multiplier.</summary>
    public int MinimumCharacteristicRollMultiplier { get; }

    /// <summary>Ch 5, "Difficulty Modifiers" (p.132)'s Easy x10 ceiling.</summary>
    public int MaximumCharacteristicRollMultiplier { get; }

    /// <summary>Ch 2 p.12's standard characteristic-roll multiplier.</summary>
    public int StandardCharacteristicRollMultiplier { get; }

    /// <summary>Ch 2 p.14 divisor for maximum hit points.</summary>
    public int HitPointDivisor { get; }

    /// <summary>Ch 2 p.14 divisor for major wounds.</summary>
    public int MajorWoundDivisor { get; }

    /// <summary>Ch 2 p.13 divisor for experience bonus.</summary>
    public int ExperienceBonusDivisor { get; }

    /// <summary>Creates a supported roll for a characteristic defined by this ruleset.</summary>
    public CharacteristicRoll CharacteristicRoll(CharacteristicId characteristic, int multiplier)
    {
        if (!Characteristics.TryGetValue(characteristic, out var definition))
        {
            throw new KeyNotFoundException($"Unknown characteristic '{characteristic}'.");
        }

        if (!definition.HasCharacteristicRoll)
        {
            throw new InvalidOperationException($"{definition.DisplayName} has no associated characteristic roll.");
        }

        if (multiplier < MinimumCharacteristicRollMultiplier || multiplier > MaximumCharacteristicRollMultiplier)
        {
            throw new ArgumentOutOfRangeException(nameof(multiplier));
        }

        return new CharacteristicRoll(characteristic, multiplier);
    }

    /// <summary>Creates Ch 2 pp.11-12's named standard x5 characteristic roll.</summary>
    public CharacteristicRoll StandardCharacteristicRoll(CharacteristicId characteristic) =>
        CharacteristicRoll(characteristic, StandardCharacteristicRollMultiplier);
}
