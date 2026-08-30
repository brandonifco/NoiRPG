using Brp.Core.Abilities;
using Brp.Core.Modifiers;
using Brp.Core.Primitives;
using Brp.Core.Randomness;
using Brp.Core.Resolution;

namespace Brp.Core.Skills;

/// <summary>
/// Resolves a skill roll by supplying <see cref="SkillResolver"/>'s two-number contract
/// from the two places each half belongs: the printed base chance from a
/// <see cref="SkillDefinition"/>, and the character's current effective rating supplied
/// separately by the caller. This is the concept #27 was blocked on: prior to this,
/// nothing supplied <see cref="SkillResolver"/>'s first argument from a skill's identity,
/// so callers (including <c>tools/Brp.Cli</c>) had no principled source for it. This type
/// does not change <see cref="ModifierChain"/> or the CLI -- unwinding those is #27's
/// remaining work once this concept exists.
/// </summary>
public static class SkillRoll
{
    /// <summary>Resolves a skill roll against an already-drawn percentile result.</summary>
    public static RollOutcome Resolve(
        SkillDefinition skill, AbilitySet abilities, Percent effectiveChance, int roll, ResolutionPolicy? policy = null)
    {
        ArgumentNullException.ThrowIfNull(skill);
        var baseChance = skill.BaseChanceFor(abilities);
        return SkillResolver.Resolve(baseChance, effectiveChance, roll, policy);
    }

    /// <summary>Resolves a skill roll by drawing a fresh percentile result from <paramref name="entropy"/>.</summary>
    public static RollOutcome Resolve(
        SkillDefinition skill, AbilitySet abilities, Percent effectiveChance, IEntropySource entropy, ResolutionPolicy? policy = null)
    {
        ArgumentNullException.ThrowIfNull(skill);
        var baseChance = skill.BaseChanceFor(abilities);
        return SkillResolver.Resolve(baseChance, effectiveChance, entropy, policy);
    }
}
