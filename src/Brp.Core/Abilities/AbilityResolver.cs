using Brp.Core.Modifiers;
using Brp.Core.Primitives;
using Brp.Core.Randomness;
using Brp.Core.Resolution;

namespace Brp.Core.Abilities;

/// <summary>
/// Resolves characteristic rolls through the normal modifier and action-roll paths. Ch 2,
/// "Characteristic Rolls" (pp.11-12), and Ch 5, "Evaluating Success or Failure" (p.127),
/// apply the five degrees and 96+ failure rule. Ch 5, "Skill Rolls" (p.128) limits the 01-05
/// floor to skills, so this adapter passes the documented zero floor-only sentinel to
/// <see cref="SkillResolver"/> (ADR 0008).
/// </summary>
public static class AbilityResolver
{
    /// <summary>Resolves a characteristic roll, or null when a gate makes the action impossible.</summary>
    public static RollOutcome? Resolve(
        AbilitySet abilities,
        CharacteristicRoll roll,
        IEnumerable<Modifier> modifiers,
        IEntropySource entropy,
        ResolutionPolicy? policy = null)
    {
        ArgumentNullException.ThrowIfNull(abilities);
        ArgumentNullException.ThrowIfNull(modifiers);
        ArgumentNullException.ThrowIfNull(entropy);

        var baseChance = roll.ChanceFor(abilities.ValueOf(roll.Characteristic));
        var chain = ModifierPipeline.Evaluate(baseChance, modifiers);
        return chain.IsGated
            ? null
            : SkillResolver.Resolve(Percent.Zero, chain.EffectiveChance!.Value, entropy, policy);
    }
}
