using Brp.Core.Abilities;
using Brp.Core.Skills;

namespace Brp.Rules.Characters;

/// <summary>
/// A whole character: identity, a Layer 1 <see cref="AbilitySet"/>, a set of Layer 2
/// skill instances with their own current ratings and experience flags, a wound list, and
/// equipment. Ch 2: Characters, "Derived Characteristics" (p.13) requires hit points to
/// change immediately after a characteristic changes, so this type never caches HP -- it
/// reads <see cref="AbilitySet.CurrentHitPoints"/> and <see cref="AbilitySet.MaximumHitPoints"/>
/// live from <see cref="Abilities"/> on every access.
/// <para>
/// Deliberately absent: any spendable power-point pool, Fate Points, or PP reservoir.
/// Ch 4: Powers is cut in full (<c>orc-scope-filter.md</c>); POW remains a characteristic
/// (it still drives the Luck roll and POW-vs-POW resistance) but nothing here spends it.
/// </para>
/// </summary>
public sealed class Character
{
    /// <summary>Creates a character from an already-built ability set and skill set.</summary>
    public Character(
        CharacterId id,
        string name,
        AbilitySet abilities,
        IReadOnlyDictionary<SkillId, CharacterSkill> skills)
    {
        ArgumentNullException.ThrowIfNull(abilities);
        ArgumentNullException.ThrowIfNull(skills);
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Character name must not be empty.", nameof(name));
        }

        Id = id;
        Name = name;
        Abilities = abilities;
        Skills = skills;
        Wounds = new WoundTrack();
        Equipment = new EquipmentList();
    }

    /// <summary>This character's stable identifier.</summary>
    public CharacterId Id { get; }

    /// <summary>The character's name.</summary>
    public string Name { get; }

    /// <summary>The Layer 1 characteristic set this character's skills and HP are computed from.</summary>
    public AbilitySet Abilities { get; }

    /// <summary>Every skill instance this character has, keyed by its canonical <see cref="SkillId"/>.</summary>
    public IReadOnlyDictionary<SkillId, CharacterSkill> Skills { get; }

    /// <summary>Structural container for wounds (Layer 4 supplies the mechanics).</summary>
    public WoundTrack Wounds { get; }

    /// <summary>Reference-only container for carried equipment (Layer 4/8 supplies gear stats).</summary>
    public EquipmentList Equipment { get; }

    /// <summary>
    /// Live current hit points, read from <see cref="Abilities"/> on every access so a
    /// characteristic change (e.g. CON lost to poison) is reflected without this type doing
    /// anything -- Ch 2 p.13's "changes immediately" requirement.
    /// </summary>
    public int CurrentHitPoints => Abilities.CurrentHitPoints;

    /// <summary>Live maximum hit points, recomputed from <see cref="Abilities"/> on every access.</summary>
    public int MaximumHitPoints => Abilities.MaximumHitPoints;

    /// <summary>Looks up a skill instance by its canonical id, throwing if this character does not have it.</summary>
    public CharacterSkill Skill(SkillId id) => Skills.TryGetValue(id, out var skill)
        ? skill
        : throw new KeyNotFoundException($"Character '{Name}' has no skill '{id}'.");

    /// <summary>Whether this character has a given skill at all -- the "has / does not have" question (#6).</summary>
    public bool HasSkill(SkillId id) => Skills.ContainsKey(id);
}
