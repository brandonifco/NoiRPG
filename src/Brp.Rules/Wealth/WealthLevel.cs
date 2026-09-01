namespace Brp.Rules.Wealth;

/// <summary>
/// A character's Money and Wealth level (Ch 2: Characters, "Wealth", p.19; Ch 3: Skills, "Status
/// Skill, Social Status, &amp; Character Wealth", p.51): "Destitute → Wealthy", "a clean abstraction
/// ... that suits a game where the PI's finances are a story element" (<c>orc-scope-filter.md</c>
/// Ch 8, line 129). The five printed levels, in the book's own ascending order, so callers can
/// compare levels ordinally (e.g. Ch 8, "Charges or Limited-Use Equipment", p.190: a resource "two
/// or three Wealth levels lower than the equipment's cost").
/// </summary>
public enum WealthLevel
{
    /// <summary>Ch 2, p.19: "Penniless... homeless... must scavenge food and drink or rely on charity."</summary>
    Destitute = 0,

    /// <summary>Ch 2, p.19: "some money and does not want for a place to sleep or food to eat... without much luxury."</summary>
    Poor = 1,

    /// <summary>Ch 2, p.19: "a comfortable income... major purchases must be weighed carefully."</summary>
    Average = 2,

    /// <summary>Ch 2, p.19: "doing quite well... does not need to think twice about making major purchases."</summary>
    Affluent = 3,

    /// <summary>Ch 2, p.19: "vast material wealth from a near-inexhaustible source."</summary>
    Wealthy = 4,
}
