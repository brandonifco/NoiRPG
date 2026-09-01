using Brp.Core.Randomness;

namespace Brp.Rules.Combat;

/// <summary>
/// Rolls where a landed attack strikes, per Ch 6: Combat, "Melee Hit Location Table (Option)"
/// (p.145): "When an attack is successful, roll a D20 and use the result to consult the appropriate
/// hit location table."
/// </summary>
public static class HitLocationResolver
{
    /// <summary>Rolls a D20 against <paramref name="ruleset"/>'s hit-location table.</summary>
    /// <param name="ruleset">Supplies the D20 hit-location table.</param>
    /// <param name="entropy">The entropy source, per AGENTS.md invariant 5.</param>
    public static HitLocationRoll RollLocation(HitLocationRuleset ruleset, IEntropySource entropy)
    {
        ArgumentNullException.ThrowIfNull(ruleset);
        ArgumentNullException.ThrowIfNull(entropy);

        var roll = entropy.NextDie(20);
        var row = ruleset.Table.ForRoll(roll);
        return new HitLocationRoll(roll, row.Location, row.Description);
    }
}
