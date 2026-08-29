using Brp.Core.Dice;
using Brp.Core.Primitives;

namespace Brp.Core.Abilities;

/// <summary>
/// Mutable characteristic values and their live derived attributes. Ch 2: Characters,
/// "Derived Characteristics" (p.13) requires each derived attribute to change immediately
/// after a characteristic changes, so values here are calculated on read rather than cached.
/// </summary>
public sealed class AbilitySet
{
    private readonly Dictionary<CharacteristicId, int> _values;
    private readonly CharacteristicId _constitution;
    private readonly CharacteristicId _size;
    private readonly CharacteristicId _intelligence;
    private readonly CharacteristicId _strength;
    private int _currentHitPoints;

    /// <summary>Creates a complete characteristic set using its ruleset definitions.</summary>
    public AbilitySet(AbilityRuleset ruleset, IReadOnlyDictionary<CharacteristicId, int> values)
    {
        ArgumentNullException.ThrowIfNull(ruleset);
        ArgumentNullException.ThrowIfNull(values);
        Ruleset = ruleset;
        _values = values.ToDictionary(pair => pair.Key, pair => pair.Value);

        if (!_values.Keys.OrderBy(id => id.Value).SequenceEqual(ruleset.Characteristics.Keys.OrderBy(id => id.Value)))
        {
            throw new ArgumentException("Values must contain exactly the characteristics defined by the ruleset.", nameof(values));
        }

        foreach (var (id, value) in _values)
        {
            Validate(id, value);
        }

        _constitution = RequiredId("CON");
        _size = RequiredId("SIZ");
        _intelligence = RequiredId("INT");
        _strength = RequiredId("STR");
        _currentHitPoints = MaximumHitPoints;
    }

    /// <summary>The ruleset that defines these abilities.</summary>
    public AbilityRuleset Ruleset { get; }

    /// <summary>Ch 2: Characters, "Hit Points" (p.14), rounded up.</summary>
    public int MaximumHitPoints => Rounding.Divide(
        ValueOf(_constitution) + ValueOf(_size), Ruleset.HitPointDivisor, RoundingMode.Up);

    /// <summary>Ch 2: Characters, "Major Wounds" (p.14), rounded up from current maximum HP.</summary>
    public int MajorWoundLevel => Rounding.Divide(MaximumHitPoints, Ruleset.MajorWoundDivisor, RoundingMode.Up);

    /// <summary>Ch 2: Characters, "Experience Bonus" (p.13), rounded up from INT.</summary>
    public int ExperienceBonus => Rounding.Divide(ValueOf(_intelligence), Ruleset.ExperienceBonusDivisor, RoundingMode.Up);

    /// <summary>Ch 2: Characters, "Damage Modifier Table" (p.13), including negative dice.</summary>
    public DiceExpression? DamageModifier => Ruleset.DamageModifierTable.ForTotal(ValueOf(_strength) + ValueOf(_size));

    /// <summary>Current HP. It may be negative; Ch 2 p.14 permits damage below zero.</summary>
    public int CurrentHitPoints => _currentHitPoints;

    /// <summary>Ch 2: Characters, "Movement (MOV)" (p.15), a flat value and not a formula.</summary>
    public int Movement => Ruleset.StartingMovement;

    /// <summary>Reads a characteristic's current value.</summary>
    public int ValueOf(CharacteristicId characteristic) => _values.TryGetValue(characteristic, out var value)
        ? value
        : throw new KeyNotFoundException($"Unknown characteristic '{characteristic}'.");

    /// <summary>
    /// Changes one characteristic and immediately enforces Ch 2 p.13's reduced maximum-HP cap.
    /// The cap is applied at mutation time so a later restoration cannot resurrect clamped HP.
    /// </summary>
    public void Set(CharacteristicId characteristic, int value)
    {
        Validate(characteristic, value);
        _values[characteristic] = value;
        _currentHitPoints = Math.Min(_currentHitPoints, MaximumHitPoints);
    }

    /// <summary>Sets current HP without adding a zero floor absent from Ch 2's damage rules.</summary>
    public void SetCurrentHitPoints(int value) => _currentHitPoints = Math.Min(value, MaximumHitPoints);

    private CharacteristicId RequiredId(string value)
    {
        var id = new CharacteristicId(value);
        return Ruleset.Characteristics.ContainsKey(id)
            ? id
            : throw new ArgumentException($"Ruleset must define {value}.");
    }

    private void Validate(CharacteristicId id, int value)
    {
        if (!Ruleset.Characteristics.TryGetValue(id, out var definition))
        {
            throw new KeyNotFoundException($"Unknown characteristic '{id}'.");
        }

        if (value < definition.Minimum || (definition.Maximum is int maximum && value > maximum))
        {
            throw new ArgumentOutOfRangeException(nameof(value), value, $"{definition.DisplayName} must be within its ruleset bounds.");
        }
    }
}
