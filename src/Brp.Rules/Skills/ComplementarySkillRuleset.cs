namespace Brp.Rules.Skills;

/// <summary>
/// The data-defined fraction of Ch 3: Skills, "Augments and Complementary skills" (p.34): "your
/// character may temporarily add 1/5 of your rating in a complementary skill to your rating in
/// another skill for skill rolls." AGENTS.md invariant 7 (rules values are data, not constants):
/// the numerator, denominator, and rounding mode are loaded from <c>complementary-skills-ruleset.json</c>
/// by <c>Brp.Data.NoirComplementarySkillsRuleset.Load()</c> rather than hardcoded, so the fraction is
/// never confused with the unrelated 1/5 special-success or long-range ratios that already exist
/// elsewhere in the engine.
/// </summary>
public sealed class ComplementarySkillRuleset
{
    /// <summary>Creates a ruleset from data-defined values.</summary>
    public ComplementarySkillRuleset(int bonusNumerator, int bonusDenominator)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(bonusDenominator);
        ArgumentOutOfRangeException.ThrowIfNegative(bonusNumerator);

        BonusNumerator = bonusNumerator;
        BonusDenominator = bonusDenominator;
    }

    /// <summary>Ch 3, "Augments and Complementary skills" (p.34): "1/5 of your rating." Numerator.</summary>
    public int BonusNumerator { get; }

    /// <summary>Ch 3, "Augments and Complementary skills" (p.34): "1/5 of your rating." Denominator.</summary>
    public int BonusDenominator { get; }
}
