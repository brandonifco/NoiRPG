using Brp.Core.Abilities;

namespace Brp.Rules.Combat;

/// <summary>
/// Shared helper for the injury spot rules (#96) that drain a characteristic. Applies the loss
/// through <see cref="AbilitySet.Set"/> -- never by baking hit points -- so the Layer 1 derived
/// characteristics (hit points, major-wound level, skill category modifiers) recompute live, per
/// Ch 2: Characters, "Derived Characteristics" (p.13). The new value is floored at the
/// characteristic's ruleset minimum, since <see cref="AbilitySet.Set"/> rejects a value below it
/// (Ch 7, "Disease" (p.170): reaching 0 "usually means death or permanent debilitation," a state
/// the caller resolves rather than a negative characteristic).
/// </summary>
internal static class InjuryDrain
{
    /// <summary>
    /// Lowers <paramref name="characteristic"/> by <paramref name="points"/>, floored at its ruleset
    /// minimum, and returns the resulting value.
    /// </summary>
    public static int Apply(AbilitySet abilities, CharacteristicId characteristic, int points)
    {
        ArgumentNullException.ThrowIfNull(abilities);
        ArgumentOutOfRangeException.ThrowIfNegative(points);

        var minimum = abilities.Ruleset.Characteristics.TryGetValue(characteristic, out var definition)
            ? definition.Minimum
            : throw new KeyNotFoundException($"Unknown characteristic '{characteristic}'.");

        var newValue = Math.Max(minimum, abilities.ValueOf(characteristic) - points);
        abilities.Set(characteristic, newValue);
        return newValue;
    }
}
