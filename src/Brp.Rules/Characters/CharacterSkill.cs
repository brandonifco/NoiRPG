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
