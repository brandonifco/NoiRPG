using Brp.Core.Abilities;
using Brp.Data;

namespace Brp.Core.Tests.Skills;

/// <summary>Shared factory for a plain average <see cref="AbilitySet"/>, for tests that don't care about its values.</summary>
internal static class AbilityHelper
{
    public static AbilitySet Default()
    {
        var ruleset = NoirAbilityRuleset.Load();
        var values = ruleset.Characteristics.Keys.ToDictionary(id => id, _ => 12);
        return new AbilitySet(ruleset, values);
    }
}
