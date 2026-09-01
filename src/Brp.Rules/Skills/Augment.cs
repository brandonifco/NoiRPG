using Brp.Core.Modifiers;

namespace Brp.Rules.Skills;

/// <summary>
/// The roll-based half of Ch 3: Skills, "Augments and Complementary skills" (p.34) (Issue #114):
/// "An augment to a skill is similar [to a complementary skill bonus] but works in a slightly
/// different fashion. If your gamemaster permits it, you can attempt a roll of one complementary
/// skill to support, or augment, another primary skill roll." Unlike <see cref="ComplementarySkill"/>,
/// an augment requires an actual roll of the helper skill, and shifts the primary roll's
/// <em>difficulty grade</em> by one step rather than adding a percentage:
/// <list type="bullet">
/// <item>"If the augmenting skill roll is successful, you may adjust the difficulty of the primary
/// skill by one step, such as turning a Difficult roll into an Average one, or an Average task
/// Easy."</item>
/// <item>"If the augment fails, the primary skill is adjusted by one step [toward Difficult] due to
/// confusion or conflicting information."</item>
/// <item>"[O]nly one degree of adjustment is possible" and "[y]ou cannot augment a skill and use a
/// complementary skill bonus simultaneously for the same skill roll" -- a caller supplies at most
/// one <see cref="DifficultyModifier"/> from either <see cref="ComplementarySkill"/> or this type,
/// never both, for the same roll.
/// </item>
/// </list>
/// The returned <see cref="DifficultyModifier"/> composes through the existing, unmodified
/// <see cref="ModifierPipeline"/>: Ch 3's "one step" ladder is exactly ADR 0007's non-stacking
/// difficulty state (any number of Easier/Harder sources collapse to one net step, and the two
/// cancel pairwise), so an augment needs no bespoke difficulty arithmetic of its own.
/// <para>
/// The experience interaction -- "If successful with the augmenting skill roll, you may check it
/// for experience as normal, as well as with the primary skill. If the primary roll fails, the
/// augmenting skill does not receive an experience check" -- is the caller's responsibility via
/// <see cref="Advancement.ExperienceSystem.RecordAugmentUse"/>, not this type: this type only ever
/// produces the difficulty-shift modifier, and knows nothing about experience ledgers.
/// </para>
/// </summary>
public static class Augment
{
    /// <summary>
    /// Builds the difficulty-shift modifier an augmenting skill's own roll contributes to the
    /// primary skill roll: <see cref="DifficultyModifier.Easy"/> on a successful augment,
    /// <see cref="DifficultyModifier.Difficult"/> on a failed one.
    /// </summary>
    public static DifficultyModifier DifficultyShift(string augmentingSkillName, bool augmentSucceeded)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(augmentingSkillName);

        var source = augmentSucceeded
            ? $"augmented by {augmentingSkillName}"
            : $"failed augment by {augmentingSkillName}";

        return augmentSucceeded ? DifficultyModifier.Easy(source) : DifficultyModifier.Difficult(source);
    }
}
