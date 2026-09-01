using Brp.Core.Modifiers;
using Brp.Core.Primitives;

namespace Brp.Rules.Skills;

/// <summary>
/// The static half of Ch 3: Skills, "Augments and Complementary skills" (p.34) (Issue #114):
/// "your character may temporarily add 1/5 of your rating in a complementary skill to your
/// rating in another skill for skill rolls." Unlike <see cref="Augment"/>, a complementary
/// skill is never itself rolled -- it contributes a flat, permanent addition to the primary
/// skill's rating before the roll, computed straight from the helper's current rating.
/// <para>
/// <strong>Non-stacking (sourced):</strong> "Only one skill may be complementary to another
/// when used to assist any given roll... the benefits do not stack." Callers choose at most
/// one helper skill per roll; this type does not enforce that choice itself since it only
/// ever produces one modifier per call.
/// </para>
/// <para>
/// <strong>Rounding (house rule):</strong> the book's own example (Medicine 65% + Science
/// (Pharmacy) 40% -&gt; +8%, i.e. exactly 40/5) never exercises a remainder, and the book prints
/// no rounding rule for the general case. Rounds down, the same house choice already made for
/// the textually identical First Aid fraction in <c>docs/decisions/0023-healing-and-recovery.md</c>
/// ("the fractions round down (house choice -- the book prints no rounding rule)"), so the two
/// occurrences of "1/5 of a skill rating" in this engine round identically.
/// </para>
/// <para>
/// <strong>Experience (sourced):</strong> "If the main skill roll is a success, your character
/// receives an experience check only to the main skill, not to the complementary skill used."
/// A complementary skill is never rolled, so there is nothing for
/// <see cref="Advancement.ExperienceSystem"/> to gate here -- a caller that never calls
/// <see cref="Advancement.ExperienceSystem.RecordUse"/> for the helper skill already matches the
/// book exactly, on any outcome of the primary roll. Contrast <see cref="Augment"/>, whose
/// helper skill *is* rolled and does need an explicit experience gate.
/// </para>
/// </summary>
public static class ComplementarySkill
{
    /// <summary>
    /// Builds the permanent additive modifier a complementary skill contributes to a primary
    /// skill roll: <paramref name="ruleset"/>'s bonus fraction of <paramref name="helperRating"/>,
    /// rounded down. Returns a zero-delta modifier rather than <see langword="null"/> when the
    /// bonus rounds to zero (e.g. a helper rating under the denominator), so a caller can always
    /// add the result to its modifier list without a conditional.
    /// </summary>
    public static AdditiveModifier Bonus(string helperSkillName, int helperRating, ComplementarySkillRuleset ruleset)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(helperSkillName);
        ArgumentNullException.ThrowIfNull(ruleset);

        var bonus = Rounding.Divide(helperRating * ruleset.BonusNumerator, ruleset.BonusDenominator, RoundingMode.Down);
        return new AdditiveModifier(
            $"+{ruleset.BonusNumerator}/{ruleset.BonusDenominator} {helperSkillName} (complementary)",
            bonus,
            AdditiveKind.Permanent);
    }
}
