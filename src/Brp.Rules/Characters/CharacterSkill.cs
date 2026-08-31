using Brp.Core.Abilities;
using Brp.Core.Primitives;
using Brp.Core.Skills;

namespace Brp.Rules.Characters;

/// <summary>
/// One skill instance belonging to a <see cref="Character"/>: a reference to its Layer 2
/// <see cref="SkillDefinition"/>, its own mutable current rating, and a per-skill experience
/// flag. This is Layer 2's "two-number contract" (<c>SkillRoll</c>) made concrete for a
/// single character -- <see cref="Definition"/> supplies the printed base chance
/// (<see cref="PrintedBaseChance"/>), while <see cref="CurrentRating"/> is the character's own,
/// distinct number set by <see cref="Creation.CharacterBuilder"/> and moved afterward only by
/// <see cref="Advancement.ExperienceSystem"/>.
/// </summary>
public sealed class CharacterSkill
{
    /// <summary>Creates a skill instance at a starting rating.</summary>
    public CharacterSkill(SkillDefinition definition, int currentRating)
    {
        ArgumentNullException.ThrowIfNull(definition);
        Definition = definition;
        CurrentRating = currentRating;
    }

    /// <summary>
    /// Builds a skill from an <em>authored, already-final effective rating</em>, storing its base
    /// rating by subtraction: base = effective - category bonus (ADR 0006, "applied by
    /// subtraction"). Reading <see cref="EffectiveRating"/> against the same abilities then
    /// reproduces <paramref name="effectiveRating"/> exactly -- the category bonus is not added a
    /// second time. This is the seam Layer 5 authored packages use so that adding the engine's
    /// category bonus does not perturb ratings that were authored as final (ADR 0022). Contrast
    /// with the ordinary constructor, whose argument is a <em>base</em> rating the bonus is added
    /// to.
    /// </summary>
    public static CharacterSkill FromEffectiveRating(
        SkillDefinition definition, int effectiveRating, AbilitySet abilities, SkillCategoryBonusRuleset bonuses)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(bonuses);
        var baseRating = effectiveRating - bonuses.BonusFor(definition.Category, abilities);
        return new CharacterSkill(definition, baseRating);
    }

    /// <summary>The Layer 2 skill this instance resolves against.</summary>
    public SkillDefinition Definition { get; }

    /// <summary>
    /// The character's current rating -- distinct from <see cref="PrintedBaseChance"/>.
    /// Set by creation, and moved only by an <see cref="Advancement.ExperienceSystem"/>
    /// improvement roll or teaching, never directly.
    /// </summary>
    public int CurrentRating { get; private set; }

    /// <summary>
    /// Ch 5: System, "Skill Improvement" (p.138): whether this skill carries an experience
    /// check awaiting an improvement roll at case close. Set by
    /// <see cref="Advancement.ExperienceSystem"/>, mechanically gated -- there is no
    /// gamemaster to award or withhold it by hand.
    /// </summary>
    public bool HasExperienceCheck { get; private set; }

    /// <summary>This skill's printed base chance, evaluated against a character's abilities.</summary>
    public Percent PrintedBaseChance(AbilitySet abilities) => Definition.BaseChanceFor(abilities);

    /// <summary>
    /// This skill's category bonus (Ch 2: Characters, "Skill Category Bonuses (Option)",
    /// pp.18-19), read live from <paramref name="abilities"/> through
    /// <paramref name="bonuses"/> using this skill's <see cref="SkillDefinition.Category"/>.
    /// Recomputes on every call, so a characteristic change is reflected immediately.
    /// </summary>
    public int CategoryBonus(AbilitySet abilities, SkillCategoryBonusRuleset bonuses)
    {
        ArgumentNullException.ThrowIfNull(bonuses);
        return bonuses.BonusFor(Definition.Category, abilities);
    }

    /// <summary>
    /// This skill's effective rating = <see cref="CurrentRating"/> (the base rating) + its
    /// category bonus. This is the number a skill roll resolves against; ADR 0006 mandates
    /// effective = base + category bonus, and ADR 0022 applies it here. Because the bonus is a
    /// live read of <paramref name="abilities"/>, the effective rating "recomputes whenever a
    /// characteristic changes" (ADR 0006) rather than being baked in at creation.
    /// <para>
    /// The category bonus is <em>not</em> applied on top of an already-final authored rating:
    /// an authored skill built through <see cref="FromEffectiveRating"/> stored its base by
    /// subtraction, so base + bonus reproduces the authored number exactly (no double-apply).
    /// </para>
    /// </summary>
    public int EffectiveRating(AbilitySet abilities, SkillCategoryBonusRuleset bonuses) =>
        CurrentRating + CategoryBonus(abilities, bonuses);

    /// <summary>Sets the current rating directly. Creation-time only; advancement uses <see cref="Improve"/>.</summary>
    internal void SetRating(int rating) => CurrentRating = rating;

    /// <summary>Marks this skill as carrying an unresolved experience check.</summary>
    internal void MarkExperienceCheck() => HasExperienceCheck = true;

    /// <summary>Clears the experience check, whether or not the improvement roll succeeded.</summary>
    internal void ClearExperienceCheck() => HasExperienceCheck = false;

    /// <summary>Raises the current rating by a gain from a successful improvement roll or teaching.</summary>
    internal void Improve(int gain) => CurrentRating += Math.Max(0, gain);

    /// <summary>
    /// Lowers the current rating -- Ch 5: System, "Skill Training" (p.138): a teaching
    /// fumble "reduc[es] the skill by -1D3." Floors at zero rather than letting a rating go
    /// negative, which the book never contemplates.
    /// </summary>
    internal void Degrade(int amount) => CurrentRating = Math.Max(0, CurrentRating - Math.Max(0, amount));
}
