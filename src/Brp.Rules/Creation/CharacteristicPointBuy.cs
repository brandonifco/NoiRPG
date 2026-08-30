using Brp.Core.Abilities;

namespace Brp.Rules.Creation;

/// <summary>
/// Ch 2: Characters, "Point-Based Character Creation (option)" (pp.9-10): spends a fixed
/// point pool across the seven point-buy characteristics (STR, CON, SIZ, INT, POW, DEX, CHA),
/// then applies NoiRPG's optional ±<see cref="CharacterCreationRuleset.FreeShiftPoints"/>
/// redistribution -- see that property's remarks for why this second step is a house-rule
/// extension rather than a printed part of this option. EDU is out of scope here: the book
/// assigns it separately from age and background (Ch 2 p.9, "Education (Option)"), which is
/// deferred past this issue (see `docs/decisions/0006-skill-bonus-system.md`).
/// </summary>
public static class CharacteristicPointBuy
{
    /// <summary>
    /// Spends <paramref name="deltas"/> (signed point-buy deltas from the ruleset's starting
    /// value, keyed by characteristic) against the pool, validating cost, bounds, and the
    /// ruleset's creation-time maximum. Returns the resulting absolute characteristic values.
    /// </summary>
    public static IReadOnlyDictionary<CharacteristicId, int> Allocate(
        CharacterCreationRuleset ruleset,
        AbilityRuleset abilityRuleset,
        IReadOnlyDictionary<CharacteristicId, int> deltas)
    {
        ArgumentNullException.ThrowIfNull(ruleset);
        ArgumentNullException.ThrowIfNull(abilityRuleset);
        ArgumentNullException.ThrowIfNull(deltas);

        if (!deltas.Keys.OrderBy(id => id.Value).SequenceEqual(ruleset.CharacteristicCosts.Keys.OrderBy(id => id.Value)))
        {
            throw new ArgumentException(
                "Deltas must be supplied for exactly the point-buy characteristics.", nameof(deltas));
        }

        var netSpend = 0;
        var values = new Dictionary<CharacteristicId, int>();
        foreach (var (id, delta) in deltas)
        {
            var cost = ruleset.CharacteristicCosts[id];
            netSpend += cost * delta;
            values[id] = ValidateBounds(ruleset, abilityRuleset, id, ruleset.StartingCharacteristicValue + delta);
        }

        if (netSpend > ruleset.CharacteristicPointPool)
        {
            throw new ArgumentException(
                $"Point-buy spend of {netSpend} exceeds the {ruleset.CharacteristicPointPool}-point pool.",
                nameof(deltas));
        }

        return values;
    }

    /// <summary>
    /// Applies a zero-sum redistribution of up to <see cref="CharacterCreationRuleset.FreeShiftPoints"/>
    /// total points on top of an already-allocated set of characteristics -- the house-rule
    /// extension documented on <see cref="CharacterCreationRuleset.FreeShiftPoints"/>.
    /// <paramref name="shift"/> must sum to zero (points only move between characteristics,
    /// none are created or destroyed) and its total moved (the sum of its positive entries)
    /// must not exceed the ruleset's allowance.
    /// </summary>
    public static IReadOnlyDictionary<CharacteristicId, int> ApplyShift(
        CharacterCreationRuleset ruleset,
        AbilityRuleset abilityRuleset,
        IReadOnlyDictionary<CharacteristicId, int> allocated,
        IReadOnlyDictionary<CharacteristicId, int> shift)
    {
        ArgumentNullException.ThrowIfNull(ruleset);
        ArgumentNullException.ThrowIfNull(abilityRuleset);
        ArgumentNullException.ThrowIfNull(allocated);
        ArgumentNullException.ThrowIfNull(shift);

        if (shift.Count == 0)
        {
            return allocated;
        }

        if (shift.Keys.Any(id => !allocated.ContainsKey(id)))
        {
            throw new ArgumentException("Shift keys must be a subset of the allocated characteristics.", nameof(shift));
        }

        var net = shift.Values.Sum();
        if (net != 0)
        {
            throw new ArgumentException(
                $"A characteristic shift must be zero-sum (points only move between characteristics); net was {net}.",
                nameof(shift));
        }

        var moved = shift.Values.Where(v => v > 0).Sum();
        if (moved > ruleset.FreeShiftPoints)
        {
            throw new ArgumentException(
                $"Shift of {moved} points exceeds the {ruleset.FreeShiftPoints}-point allowance.", nameof(shift));
        }

        var result = new Dictionary<CharacteristicId, int>(allocated);
        foreach (var (id, delta) in shift)
        {
            result[id] = ValidateBounds(ruleset, abilityRuleset, id, allocated[id] + delta);
        }

        return result;
    }

    private static int ValidateBounds(
        CharacterCreationRuleset ruleset, AbilityRuleset abilityRuleset, CharacteristicId id, int value)
    {
        if (!abilityRuleset.Characteristics.TryGetValue(id, out var definition))
        {
            throw new KeyNotFoundException($"Ability ruleset does not define characteristic '{id}'.");
        }

        var effectiveMaximum = definition.Maximum is int max
            ? Math.Min(max, ruleset.CharacteristicCreationMaximum)
            : ruleset.CharacteristicCreationMaximum;

        if (value < definition.Minimum || value > effectiveMaximum)
        {
            throw new ArgumentOutOfRangeException(
                nameof(value), value,
                $"{definition.DisplayName} must be between {definition.Minimum} and {effectiveMaximum} at creation.");
        }

        return value;
    }
}
