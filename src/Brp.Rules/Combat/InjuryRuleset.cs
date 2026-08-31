namespace Brp.Rules.Combat;

/// <summary>
/// The aggregate ruleset for the Ch 7: Spot Rules injury/effect rules (#96) -- Falling (p.171),
/// Poison (pp.175-176), and Disease (pp.169-170) -- grouped so a single
/// <c>Brp.Data.NoirInjuryRuleset.Load()</c> supplies them all while each rule's resolver takes only
/// the sub-ruleset it needs. The sibling of <see cref="SpotRuleRuleset"/> (the situational-modifier
/// spot rules, #50). All values are data (AGENTS.md invariant 7); see
/// <c>docs/decisions/0019-injury-spot-rules.md</c>.
/// </summary>
public sealed class InjuryRuleset
{
    /// <summary>Creates an injury ruleset from its three grouped sub-rulesets.</summary>
    public InjuryRuleset(FallingRuleset falling, PoisonRuleset poison, DiseaseRuleset disease)
    {
        ArgumentNullException.ThrowIfNull(falling);
        ArgumentNullException.ThrowIfNull(poison);
        ArgumentNullException.ThrowIfNull(disease);

        Falling = falling;
        Poison = poison;
        Disease = disease;
    }

    /// <summary>Ch 7, "Falling" (p.171) values.</summary>
    public FallingRuleset Falling { get; }

    /// <summary>Ch 7, "Poison" / "Poison Antidotes" (pp.175-176) values.</summary>
    public PoisonRuleset Poison { get; }

    /// <summary>Ch 7, "Disease" (pp.169-170) values, including the Illness Severity Table.</summary>
    public DiseaseRuleset Disease { get; }
}
