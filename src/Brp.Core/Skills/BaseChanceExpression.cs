using Brp.Core.Abilities;
using Brp.Core.Primitives;

namespace Brp.Core.Skills;

/// <summary>
/// A skill's printed base chance, kept as a small expression rather than a bare number.
/// Ch 3: Skills, "Base Chances" (p.31): "Every skill... has a base chance associated with
/// it... A skill's base chance depends greatly upon the era of a campaign... Each skill
/// description lists several base chances for different eras, as appropriate." The book's
/// per-skill entries realize that in four distinct shapes, which is why this is a closed
/// hierarchy rather than a single formula string: a constant (<c>Spot 25%</c>), a
/// characteristic formula (<c>Dodge DEX×2</c>), an either/or pair resolved by era or
/// familiarity (<c>Drive 20% or 01%</c>), and a value the skill itself cannot supply
/// (<c>Firearm: as per weapon specialty</c>). See ADR 0011 for the design record.
/// </summary>
public abstract record BaseChanceExpression
{
    /// <summary>Evaluates this expression to a concrete base chance against a character's abilities.</summary>
    public abstract Percent Evaluate(AbilitySet abilities);
}
