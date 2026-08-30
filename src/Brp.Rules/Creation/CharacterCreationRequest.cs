using Brp.Core.Abilities;
using Brp.Core.Skills;
using Brp.Rules.Characters;

namespace Brp.Rules.Creation;

/// <summary>
/// Everything <see cref="CharacterBuilder"/> needs to build one <see cref="Character"/> by
/// point-buy. A plain data holder -- the builder does all the validation and derivation.
/// </summary>
public sealed class CharacterCreationRequest
{
    /// <summary>This character's stable identifier.</summary>
    public required CharacterId Id { get; init; }

    /// <summary>The character's name.</summary>
    public required string Name { get; init; }

    /// <summary>
    /// Signed point-buy deltas from the ruleset's starting value, keyed by characteristic --
    /// exactly the seven point-buy characteristics (STR, CON, SIZ, INT, POW, DEX, CHA).
    /// </summary>
    public required IReadOnlyDictionary<CharacteristicId, int> CharacteristicDeltas { get; init; }

    /// <summary>
    /// Optional zero-sum redistribution applied after <see cref="CharacteristicDeltas"/> --
    /// see <see cref="CharacterCreationRuleset.FreeShiftPoints"/>. Empty by default.
    /// </summary>
    public IReadOnlyDictionary<CharacteristicId, int> CharacteristicShift { get; init; } =
        new Dictionary<CharacteristicId, int>();

    /// <summary>
    /// EDU is assigned by the gamemaster from age and background (Ch 2 p.9, "Education
    /// (Option)"), not spent from the 24-point pool; that assignment is out of this issue's
    /// scope (see `docs/decisions/0006-skill-bonus-system.md`). Defaults to the ruleset's
    /// starting value (10) as a neutral placeholder.
    /// </summary>
    public int? Education { get; init; }

    /// <summary>The Freeform Profession package to apply, if any. See <see cref="BackgroundPackage"/>.</summary>
    public BackgroundPackage? Package { get; init; }

    /// <summary>
    /// Professional skill points spent beyond the package's own allocation, keyed by skill.
    /// Combined with the package's points, the total must not exceed
    /// <see cref="CharacterCreationRuleset.ProfessionalSkillPoints"/>.
    /// </summary>
    public IReadOnlyDictionary<SkillId, int> AdditionalProfessionalSkillPoints { get; init; } =
        new Dictionary<SkillId, int>();

    /// <summary>
    /// Personal skill points spent, keyed by skill. Total must not exceed INT times the
    /// ruleset's personal skill-point multiplier (see <see cref="UseIncreasedPersonalSkillPoints"/>).
    /// </summary>
    public IReadOnlyDictionary<SkillId, int> PersonalSkillPoints { get; init; } =
        new Dictionary<SkillId, int>();

    /// <summary>
    /// Whether to use the Increased Personal Skill Points option (`orc-scope-filter.md`: ON).
    /// True by default -- NoiRPG's recommended Noir configuration.
    /// </summary>
    public bool UseIncreasedPersonalSkillPoints { get; init; } = true;
}
