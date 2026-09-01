using Brp.Core.Modifiers;

namespace Brp.Rules.Combat;

/// <summary>
/// Resolves Ch 6: Combat, "Fighting Defensively" (p.151) -- an in-scope Ch 6 core mechanic never
/// built before #113: forgoing all attacks for a free Dodge substituted for the round's attack,
/// plus (only if the character can normally make multiple attacks per round -- e.g. a skill over
/// 100%) a second free defensive attempt, which may be either a Dodge or a Parry.
/// <para>
/// <strong>"Free" means "adds no new -30% increment," not "rolled at 0% penalty."</strong> p.151:
/// "If they have already made Dodge attempts and parries and are at a negative modifier, the
/// modifier does not increase." A free attempt is still rolled at whatever cumulative -30%
/// penalty the character has already accrued from earlier, non-free Dodge/parry attempts this
/// round -- it merely does not add a further -30% on top of that, and does not itself count
/// toward a later attempt's penalty. See <see cref="SuccessiveDefensePenaltyModifier"/>'s remarks
/// for exactly which count a caller passes to get this right.
/// </para>
/// <para>
/// <strong>The first free defense is Dodge-only, and the count is capped at two, not one per
/// forgone attack.</strong> p.151: "they can substitute one free Dodge attempt for their
/// attack... your character can substitute a Dodge skill attempt for an attack without incurring
/// the -30% penalty... Essentially, it is a free Dodge." Only the *second* free attempt --
/// gated on multi-attack capability, not on how many attacks were forgone -- may be "a second
/// free Dodge or parry" (p.151). A character who forgoes three attacks by having three actions
/// does not get three free defenses; the maximum is always two, and only the second is optional.
/// </para>
/// <para>
/// This piece also gives the successive Dodge/parry -30% cumulative penalty (Ch 6, "Parry"/
/// "Dodge", p.144; restated at "Fighting Defensively", p.151) its first implementation --
/// previously a documented-but-unbuilt seam (<c>attack-defense-matrix-ruleset.json</c>'s
/// <c>deferred</c> list, <c>docs/decisions/0016-attack-defense-matrix.md</c>) -- because Fighting
/// Defensively's entire point is exempting its free attempt(s) from that count.
/// </para>
/// </summary>
public static class FightingDefensivelyResolver
{
    /// <summary>
    /// Declares fighting defensively for the round, per p.151. The first free defense is always a
    /// Dodge (never a Parry) -- "one free Dodge attempt", "a free Dodge skill attempt",
    /// "Essentially, it is a free Dodge." A second free defense, which may be either a Dodge or a
    /// Parry, is granted only when <paramref name="canMakeMultipleAttacksPerRound"/> is
    /// <see langword="true"/> -- "If your character can normally make multiple attacks per round
    /// (such as having a skill over 100%), they can make a second free Dodge or parry without
    /// incurring the cumulative penalty." There is no third free defense and no scaling with how
    /// many attacks were forgone; the cap is always at most two. "Under no circumstances can
    /// fighting defensively be combined with any attack or offensive action, even such as the
    /// Desperate Action" and "your character cannot Dodge and parry within the same DEX rank" are
    /// both named as flags a caller enforces, not choices this method makes.
    /// </summary>
    /// <param name="canMakeMultipleAttacksPerRound">
    /// Whether the character can normally make more than one attack in a round (e.g. a combat
    /// skill rated over 100%) -- the sole gate on the second free defense, per p.151.
    /// </param>
    public static FightingDefensivelyDeclaration Declare(bool canMakeMultipleAttacksPerRound)
    {
        return new FightingDefensivelyDeclaration(
            FirstFreeDefenseType: DefenseType.Dodge,
            SecondFreeDefenseAvailable: canMakeMultipleAttacksPerRound,
            SecondFreeDefenseAllowedTypes: canMakeMultipleAttacksPerRound
                ? [DefenseType.Dodge, DefenseType.Parry]
                : [],
            ForfeitsAllAttacksThisRound: true,
            CannotCombineWithAnyOffensiveAction: true,
            CannotDodgeAndParryWithinTheSameDexRank: true);
    }

    /// <summary>
    /// Ch 6, p.144/p.151: "each subsequent Dodge or parry attempt is at a cumulative -30%
    /// modifier."
    /// <para>
    /// <paramref name="countedPriorAttempts"/> is the number of prior <em>non-free</em>
    /// Dodge/parry attempts this round (i.e. every ordinary attempt, in order, but never a free
    /// Fighting-Defensively attempt). For a <em>free</em> attempt, the caller passes the same
    /// count it would have passed for the next ordinary attempt at that point -- p.151: "If they
    /// have already made Dodge attempts and parries and are at a negative modifier, the modifier
    /// does not increase." That is, a free attempt is rolled at the character's <em>current</em>
    /// accumulated penalty (whatever <paramref name="countedPriorAttempts"/> non-free attempts
    /// already produced), not reset to 0% -- "free" here means "does not itself add a further
    /// -30% increment," not "rolled unpenalized." The caller then does <em>not</em> increment the
    /// running non-free count afterward, so the free attempt is excluded going forward too.
    /// </para>
    /// </summary>
    public static int SuccessiveDefensePenaltyPercent(int countedPriorAttempts, SpecialDamageEffectsRuleset ruleset)
    {
        ArgumentNullException.ThrowIfNull(ruleset);
        ArgumentOutOfRangeException.ThrowIfNegative(countedPriorAttempts);
        return -(countedPriorAttempts * ruleset.SuccessiveDefensePenaltyPercent);
    }

    /// <summary>
    /// Builds the additive percent modifier a caller adds to a Dodge or Parry roll's modifier
    /// list for the given attempt count, or <see langword="null"/> when no penalty applies (the
    /// first attempt of the round, whether free or not). See
    /// <see cref="SuccessiveDefensePenaltyPercent"/>'s remarks for exactly what
    /// <paramref name="countedPriorAttempts"/> means for a free Fighting-Defensively attempt --
    /// it is the character's <em>current</em> non-free-attempt count, not zero, so the free
    /// attempt is rolled at whatever penalty already applies rather than at 0%.
    /// </summary>
    public static Modifier? SuccessiveDefensePenaltyModifier(
        int countedPriorAttempts, SpecialDamageEffectsRuleset ruleset, string source)
    {
        var percent = SuccessiveDefensePenaltyPercent(countedPriorAttempts, ruleset);
        return percent == 0 ? null : new AdditiveModifier(source, percent, AdditiveKind.Permanent);
    }
}

/// <summary>
/// The declared effect of fighting defensively for a round (Ch 6, "Fighting Defensively", p.151).
/// "Free" below means the attempt does not itself add a further -30% successive-defense
/// increment (and is not counted toward one later) -- it is still rolled at whatever cumulative
/// penalty the character has already accrued from earlier attempts this round, not at 0%. See
/// <see cref="FightingDefensivelyResolver.SuccessiveDefensePenaltyPercent"/>'s remarks.
/// </summary>
/// <param name="FirstFreeDefenseType">
/// The type of the first free defensive attempt -- always <see cref="DefenseType.Dodge"/>
/// (p.151: "one free Dodge attempt"; never a Parry).
/// </param>
/// <param name="SecondFreeDefenseAvailable">
/// Whether a second free defensive attempt is available this round -- only true when the
/// character can normally make multiple attacks per round (p.151).
/// </param>
/// <param name="SecondFreeDefenseAllowedTypes">
/// The defense types the second free attempt may use -- <see cref="DefenseType.Dodge"/> or
/// <see cref="DefenseType.Parry"/> when <see cref="SecondFreeDefenseAvailable"/> is
/// <see langword="true"/> (p.151: "a second free Dodge or parry"), otherwise empty.
/// </param>
/// <param name="ForfeitsAllAttacksThisRound">The character makes no attacks at all this round.</param>
/// <param name="CannotCombineWithAnyOffensiveAction">
/// No attack or offensive action (including a Desperate Action) can be combined with fighting defensively.
/// </param>
/// <param name="CannotDodgeAndParryWithinTheSameDexRank">
/// The character cannot both Dodge and Parry within the same DEX rank.
/// </param>
public sealed record FightingDefensivelyDeclaration(
    DefenseType FirstFreeDefenseType,
    bool SecondFreeDefenseAvailable,
    IReadOnlyList<DefenseType> SecondFreeDefenseAllowedTypes,
    bool ForfeitsAllAttacksThisRound,
    bool CannotCombineWithAnyOffensiveAction,
    bool CannotDodgeAndParryWithinTheSameDexRank);
