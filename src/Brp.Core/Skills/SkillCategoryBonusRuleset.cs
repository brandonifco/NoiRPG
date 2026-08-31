using Brp.Core.Abilities;

namespace Brp.Core.Skills;

/// <summary>
/// The ruleset-configurable skill-category-bonus policy mandated by
/// <c>docs/decisions/0006-skill-bonus-system.md</c> and applied to the engine by
/// <c>docs/decisions/0022-skill-category-bonus-application.md</c>. It computes a category bonus
/// for any <see cref="SkillCategory"/> from an <see cref="AbilitySet"/>, per Ch 2: Characters,
/// "Skill Category Bonuses (Option)" -- the "Skill Category Modifiers" and "Skill Bonus Table"
/// (pp.18-19):
/// <list type="bullet">
///   <item>Primary characteristic: +/-1% for every point away from the neutral value.</item>
///   <item>Secondary characteristic: +/-1% for every 2 points away, magnitude rounded down.</item>
///   <item>Negative characteristic: an inverted primary -- the primary rule, subtracted.</item>
/// </list>
/// Every value (the neutral pivot, the two divisors, and the category-to-characteristic map) is
/// supplied as data by <c>Brp.Data.NoirSkillCategoryBonusRuleset.Load()</c> (AGENTS.md invariant
/// 7); nothing here is a hardcoded book number. This is the "full" option; ADR 0006's "neither"
/// option is expressible by configuring every category with no secondaries and no negatives
/// (leaving the bonus at the primary's deviation) or, more directly, a zero map.
/// <para>
/// This computes the <em>bonus</em> only. Applying it to a character -- effective rating = base
/// rating + category bonus, recomputed whenever a characteristic changes, and <em>not</em>
/// double-applied to authored final-effective ratings -- lives in
/// <c>Brp.Rules.Characters.CharacterSkill</c> (see ADR 0022).
/// </para>
/// </summary>
public sealed class SkillCategoryBonusRuleset
{
    private readonly Dictionary<SkillCategory, SkillCategoryModifierSpec> _specs;

    /// <summary>Creates a bonus policy from data-defined values.</summary>
    /// <param name="neutralCharacteristicValue">The value a characteristic contributes nothing at (10 in the book).</param>
    /// <param name="primaryPointsPerModifier">Points of deviation per +/-1% for a primary or negative characteristic (1 in the book).</param>
    /// <param name="secondaryPointsPerModifier">Points of deviation per +/-1% for a secondary characteristic (2 in the book).</param>
    /// <param name="categories">One <see cref="SkillCategoryModifierSpec"/> per skill category; all six are required.</param>
    public SkillCategoryBonusRuleset(
        int neutralCharacteristicValue,
        int primaryPointsPerModifier,
        int secondaryPointsPerModifier,
        IEnumerable<SkillCategoryModifierSpec> categories)
    {
        ArgumentNullException.ThrowIfNull(categories);
        if (primaryPointsPerModifier <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(primaryPointsPerModifier), primaryPointsPerModifier, "Points per modifier must be positive.");
        }

        if (secondaryPointsPerModifier <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(secondaryPointsPerModifier), secondaryPointsPerModifier, "Points per modifier must be positive.");
        }

        NeutralCharacteristicValue = neutralCharacteristicValue;
        PrimaryPointsPerModifier = primaryPointsPerModifier;
        SecondaryPointsPerModifier = secondaryPointsPerModifier;

        _specs = new Dictionary<SkillCategory, SkillCategoryModifierSpec>();
        foreach (var spec in categories)
        {
            ArgumentNullException.ThrowIfNull(spec);
            if (!_specs.TryAdd(spec.Category, spec))
            {
                throw new ArgumentException($"Duplicate configuration for skill category '{spec.Category}'.", nameof(categories));
            }
        }

        foreach (var category in Enum.GetValues<SkillCategory>())
        {
            if (!_specs.ContainsKey(category))
            {
                throw new ArgumentException($"Skill category '{category}' has no bonus configuration.", nameof(categories));
            }
        }
    }

    /// <summary>The characteristic value that contributes a zero bonus (Ch 2 p.18: 10).</summary>
    public int NeutralCharacteristicValue { get; }

    /// <summary>Points of deviation from neutral per +/-1% for a primary/negative characteristic.</summary>
    public int PrimaryPointsPerModifier { get; }

    /// <summary>Points of deviation from neutral per +/-1% for a secondary characteristic.</summary>
    public int SecondaryPointsPerModifier { get; }

    /// <summary>The category-to-characteristic map, keyed by category.</summary>
    public IReadOnlyDictionary<SkillCategory, SkillCategoryModifierSpec> Categories => _specs;

    /// <summary>
    /// Computes the skill category bonus for <paramref name="category"/> from a character's
    /// characteristics. May be negative; the book's example gives Communication -3% for a
    /// low-INT character (Ch 2 p.18). The result is a live read -- calling it again after a
    /// characteristic changes yields the new bonus, which is what makes an effective rating
    /// "recompute whenever a characteristic changes" (ADR 0006).
    /// </summary>
    public int BonusFor(SkillCategory category, AbilitySet abilities)
    {
        ArgumentNullException.ThrowIfNull(abilities);
        if (!_specs.TryGetValue(category, out var spec))
        {
            throw new KeyNotFoundException($"No bonus configuration for skill category '{category}'.");
        }

        var bonus = Contribution(spec.Primary, abilities, PrimaryPointsPerModifier);
        foreach (var secondary in spec.Secondary)
        {
            bonus += Contribution(secondary, abilities, SecondaryPointsPerModifier);
        }

        foreach (var negative in spec.Negative)
        {
            bonus -= Contribution(negative, abilities, PrimaryPointsPerModifier);
        }

        return bonus;
    }

    private int Contribution(CharacteristicId characteristic, AbilitySet abilities, int pointsPerModifier) =>
        SignedQuotient(abilities.ValueOf(characteristic) - NeutralCharacteristicValue, pointsPerModifier);

    /// <summary>
    /// Deviation divided by the points-per-modifier, with the magnitude rounded down (toward
    /// zero) and the sign preserved -- the book's "+1% for every 2 points ... round down" and
    /// the reference <c>tools/skill_bonus.py</c>'s <c>signed_half</c>. Integer division alone
    /// would round -3/2 toward zero to -1, which is correct here; splitting sign and magnitude
    /// makes the rounding rule explicit and divisor-agnostic.
    /// </summary>
    private static int SignedQuotient(int deviation, int pointsPerModifier) =>
        Math.Sign(deviation) * (Math.Abs(deviation) / pointsPerModifier);
}
