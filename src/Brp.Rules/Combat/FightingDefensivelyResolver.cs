using Brp.Core.Modifiers;

namespace Brp.Rules.Combat;

/// <summary>
/// Resolves Ch 6: Combat, "Fighting Defensively" (p.151) -- an in-scope Ch 6 core mechanic never
/// built before #113: forgoing all attacks for one (or, with multiple attacks per round, more
/// than one) free, unpenalized Dodge substituted for the attack(s) that round.
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
    /// Declares fighting defensively for the round, per p.151: "your character can substitute one
    /// free Dodge attempt for their attack and can continue to make dodge or parry attempts... If
    /// your character can normally make multiple attacks per round (such as having a skill over
    /// 100%), they can make a second free Dodge or parry without incurring the cumulative
    /// penalty." <paramref name="attacksForgone"/> is the number of attacks the character would
    /// otherwise have made this round, each substituted 1-for-1 with a free defensive attempt.
    /// "Under no circumstances can fighting defensively be combined with any attack or offensive
    /// action, even such as the Desperate Action" and "your character cannot Dodge and parry
    /// within the same DEX rank" are both named as flags a caller enforces, not choices this
    /// method makes.
    /// </summary>
    public static FightingDefensivelyDeclaration Declare(int attacksForgone)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(attacksForgone);
        return new FightingDefensivelyDeclaration(
            FreeDefenseAttempts: attacksForgone,
            ForfeitsAllAttacksThisRound: true,
            CannotCombineWithAnyOffensiveAction: true,
            CannotDodgeAndParryWithinTheSameDexRank: true);
    }

    /// <summary>
    /// Ch 6, p.144/p.151: "each subsequent Dodge or parry attempt is at a cumulative -30%
    /// modifier." <paramref name="countedPriorAttempts"/> excludes any free Fighting-Defensively
    /// attempts, which "do not incur the cumulative penalty" and are not counted toward it (p.151:
    /// "If they have already made Dodge attempts and parries and are at a negative modifier, the
    /// modifier does not increase" -- i.e. a free attempt neither raises nor is raised by the
    /// count).
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
    /// first counted attempt, or any free Fighting-Defensively attempt, which the caller simply
    /// never passes into <paramref name="countedPriorAttempts"/>).
    /// </summary>
    public static Modifier? SuccessiveDefensePenaltyModifier(
        int countedPriorAttempts, SpecialDamageEffectsRuleset ruleset, string source)
    {
        var percent = SuccessiveDefensePenaltyPercent(countedPriorAttempts, ruleset);
        return percent == 0 ? null : new AdditiveModifier(source, percent, AdditiveKind.Permanent);
    }
}

/// <summary>The declared effect of fighting defensively for a round (Ch 6, "Fighting Defensively", p.151).</summary>
/// <param name="FreeDefenseAttempts">
/// The number of free, unpenalized Dodge/parry attempts substituted for forgone attacks this round.
/// </param>
/// <param name="ForfeitsAllAttacksThisRound">The character makes no attacks at all this round.</param>
/// <param name="CannotCombineWithAnyOffensiveAction">
/// No attack or offensive action (including a Desperate Action) can be combined with fighting defensively.
/// </param>
/// <param name="CannotDodgeAndParryWithinTheSameDexRank">
/// The character cannot both Dodge and Parry within the same DEX rank.
/// </param>
public sealed record FightingDefensivelyDeclaration(
    int FreeDefenseAttempts,
    bool ForfeitsAllAttacksThisRound,
    bool CannotCombineWithAnyOffensiveAction,
    bool CannotDodgeAndParryWithinTheSameDexRank);
