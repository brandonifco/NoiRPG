using Brp.Core.Abilities;
using Brp.Core.Primitives;
using Brp.Core.Skills;
using Brp.Rules.Characters;

namespace Brp.Rules.Creation;

/// <summary>
/// Builds a <see cref="Character"/> by point-buy: characteristic point-buy (Ch 2 p.9-10),
/// profession and personal skill-point allocation with Increased Personal Skill Points
/// (Ch 2 p.8), and the 75% starting soft cap. Every numeric parameter comes from a
/// <see cref="CharacterCreationRuleset"/> loaded as ruleset data (AGENTS.md invariant 7) --
/// nothing here is a hardcoded book number.
/// <para>
/// This type builds only the point-buy path. BRP's rolled-characteristics path (Step One
/// with 3D6/2D6+6 dice) is out of scope for this issue and is not built; nothing here
/// prevents a future <c>RolledCharacterBuilder</c> (or an overload here) from producing the
/// same <see cref="Character"/> shape by a different route.
/// </para>
/// </summary>
public sealed class CharacterBuilder
{
    private readonly CharacterCreationRuleset _ruleset;
    private readonly AbilityRuleset _abilityRuleset;
    private readonly SkillRegistry _skillRegistry;

    /// <summary>Creates a builder bound to a specific set of rulesets.</summary>
    public CharacterBuilder(CharacterCreationRuleset ruleset, AbilityRuleset abilityRuleset, SkillRegistry skillRegistry)
    {
        ArgumentNullException.ThrowIfNull(ruleset);
        ArgumentNullException.ThrowIfNull(abilityRuleset);
        ArgumentNullException.ThrowIfNull(skillRegistry);
        _ruleset = ruleset;
        _abilityRuleset = abilityRuleset;
        _skillRegistry = skillRegistry;
    }

    /// <summary>Builds a complete, valid <see cref="Character"/> from a creation request.</summary>
    public Character Build(CharacterCreationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var characteristics = CharacteristicPointBuy.Allocate(_ruleset, _abilityRuleset, request.CharacteristicDeltas);
        if (request.CharacteristicShift.Count > 0)
        {
            characteristics = CharacteristicPointBuy.ApplyShift(_ruleset, _abilityRuleset, characteristics, request.CharacteristicShift);
        }

        var education = request.Education ?? _ruleset.StartingCharacteristicValue;
        var eduId = new CharacteristicId("EDU");
        if (_abilityRuleset.Characteristics.ContainsKey(eduId))
        {
            var withEdu = new Dictionary<CharacteristicId, int>(characteristics) { [eduId] = education };
            characteristics = withEdu;
        }

        var abilities = new AbilitySet(_abilityRuleset, characteristics);

        var professionalPoints = CombineSkillPoints(request.Package?.ProfessionalSkillPoints, request.AdditionalProfessionalSkillPoints);
        ValidatePool(professionalPoints, _ruleset.ProfessionalSkillPoints, "professional");

        var personalCap = abilities.ValueOf(new CharacteristicId("INT")) * (request.UseIncreasedPersonalSkillPoints
            ? _ruleset.IncreasedPersonalSkillPointsIntMultiplier
            : _ruleset.PersonalSkillPointsIntMultiplier);
        ValidatePool(request.PersonalSkillPoints, personalCap, "personal");

        var skills = new Dictionary<SkillId, CharacterSkill>();
        foreach (var (id, definition) in _skillRegistry.Skills)
        {
            var baseChance = SafeBaseChance(definition, abilities);
            var professional = professionalPoints.GetValueOrDefault(id);
            var personal = request.PersonalSkillPoints.GetValueOrDefault(id);
            var addedPoints = professional + personal;

            var effectiveCap = Math.Max(_ruleset.StartingSkillCapPercent, baseChance.Value);
            var total = baseChance.Value + addedPoints;
            if (total > effectiveCap)
            {
                throw new ArgumentException(
                    $"Skill '{definition.Name}' would start at {total}%, above the {_ruleset.StartingSkillCapPercent}% "
                    + "starting soft cap (Ch 2 p.8) -- spend those points on another skill instead.",
                    nameof(request));
            }

            skills[id] = new CharacterSkill(definition, total);
        }

        return new Character(request.Id, request.Name, abilities, skills);
    }

    private static Percent SafeBaseChance(SkillDefinition definition, AbilitySet abilities)
    {
        try
        {
            return definition.BaseChanceFor(abilities);
        }
        catch (InvalidOperationException)
        {
            // Weapon-derived base chances (Ch 3, "as per weapon specialty") have no
            // standalone value until Layer 4 supplies weapon data (#21); treat as 0 rather
            // than fail character creation over a skill this layer cannot resolve.
            return Percent.Zero;
        }
    }

    private static Dictionary<SkillId, int> CombineSkillPoints(
        IReadOnlyDictionary<SkillId, int>? package, IReadOnlyDictionary<SkillId, int> additional)
    {
        var combined = new Dictionary<SkillId, int>();
        if (package is not null)
        {
            foreach (var (id, points) in package)
            {
                combined[id] = combined.GetValueOrDefault(id) + points;
            }
        }

        foreach (var (id, points) in additional)
        {
            combined[id] = combined.GetValueOrDefault(id) + points;
        }

        return combined;
    }

    private static void ValidatePool(IReadOnlyDictionary<SkillId, int> spend, int pool, string poolName)
    {
        var total = spend.Values.Sum();
        if (total > pool)
        {
            throw new ArgumentException(
                $"{poolName} skill points spent ({total}) exceed the pool of {pool}.", nameof(spend));
        }
    }
}
