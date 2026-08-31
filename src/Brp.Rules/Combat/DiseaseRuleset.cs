using Brp.Core.Dice;

namespace Brp.Rules.Combat;

/// <summary>
/// The data-defined values of Ch 7: Spot Rules, "Disease" (p.170) (AGENTS.md invariant 7:
/// rules values are data, not constants), including the "Illness Severity Table" as a banded
/// lookup. Loaded from <c>injury-ruleset.json</c> by <c>Brp.Data.NoirInjuryRuleset.Load()</c>.
/// See <c>docs/decisions/0019-injury-spot-rules.md</c>.
/// </summary>
public sealed class DiseaseRuleset
{
    /// <summary>Creates a disease ruleset from data-defined values.</summary>
    public DiseaseRuleset(
        DiceExpression minorDiseaseHitPointLoss,
        DiceExpression minorDiseaseFatigueLoss,
        int recoveryLadderStartingMultiplier,
        int recoveryLadderMultiplierIncrementPerDay,
        int recoveryLadderFumbleMultiplierPenalty,
        int recoveryLadderStrenuousConditionPenalty,
        IllnessSeverityTable illnessSeverityTable)
    {
        ArgumentNullException.ThrowIfNull(minorDiseaseHitPointLoss);
        ArgumentNullException.ThrowIfNull(minorDiseaseFatigueLoss);
        ArgumentNullException.ThrowIfNull(illnessSeverityTable);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(recoveryLadderStartingMultiplier);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(recoveryLadderMultiplierIncrementPerDay);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(recoveryLadderFumbleMultiplierPenalty);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(recoveryLadderStrenuousConditionPenalty);

        MinorDiseaseHitPointLoss = minorDiseaseHitPointLoss;
        MinorDiseaseFatigueLoss = minorDiseaseFatigueLoss;
        RecoveryLadderStartingMultiplier = recoveryLadderStartingMultiplier;
        RecoveryLadderMultiplierIncrementPerDay = recoveryLadderMultiplierIncrementPerDay;
        RecoveryLadderFumbleMultiplierPenalty = recoveryLadderFumbleMultiplierPenalty;
        RecoveryLadderStrenuousConditionPenalty = recoveryLadderStrenuousConditionPenalty;
        IllnessSeverityTable = illnessSeverityTable;
    }

    /// <summary>
    /// Ch 7, "Disease" (p.170): a minor disease should "merely cost 1 or 2 hit points... over a few
    /// days." The hit-point loss a minor disease inflicts.
    /// </summary>
    public DiceExpression MinorDiseaseHitPointLoss { get; }

    /// <summary>
    /// Ch 7, "Disease" (p.170): "...and 1D6 fatigue points over a few days." The fatigue-point loss
    /// a minor disease inflicts. (No fatigue-point subsystem is built yet -- see the resolver.)
    /// </summary>
    public DiceExpression MinorDiseaseFatigueLoss { get; }

    /// <summary>
    /// Ch 7, "Disease" (p.170): "On the morning of the second day... roll CON×2." The multiplier the
    /// recovery ladder begins at (the second day).
    /// </summary>
    public int RecoveryLadderStartingMultiplier { get; }

    /// <summary>
    /// Ch 7, "Disease" (p.170): "On the morning of the third day, roll CON×3, continuing by
    /// increasing the multiplier... until the disease is finally overcome." The amount the recovery
    /// multiplier rises each successive day.
    /// </summary>
    public int RecoveryLadderMultiplierIncrementPerDay { get; }

    /// <summary>
    /// Ch 7, "Disease" (p.170): "A fumble reduces the multiplier by ×1." The amount a fumbled
    /// recovery roll subtracts from subsequent recovery multipliers.
    /// </summary>
    public int RecoveryLadderFumbleMultiplierPenalty { get; }

    /// <summary>
    /// Ch 7, "Disease" (p.170): "Strenuous conditions (adventuring, combat, hard travel, etc.)
    /// reduce this characteristic roll by ×1 per outstanding condition." The multiplier reduction
    /// per outstanding strenuous condition.
    /// </summary>
    public int RecoveryLadderStrenuousConditionPenalty { get; }

    /// <summary>Ch 7, "Illness Severity Table" (p.170), as a banded lookup by failed-roll count.</summary>
    public IllnessSeverityTable IllnessSeverityTable { get; }
}
