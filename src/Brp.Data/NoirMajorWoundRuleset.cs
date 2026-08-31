using System.Text.Json;
using Brp.Core.Abilities;
using Brp.Core.Dice;
using Brp.Rules.Combat;

namespace Brp.Data;

/// <summary>
/// Loads NoiRPG's Major Wounds values from embedded JSON. Sourced: Ch 6: Combat, "Major Wounds" and
/// "Fatal Wounds" (pp.155-156) -- the Major Wounds Table, the shock collapse duration, and the
/// fatal-wound rescue window. Recorded in <c>docs/decisions/0021-major-wounds.md</c>.
/// </summary>
public static class NoirMajorWoundRuleset
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>Loads a new, immutable major wound ruleset from the shipped data.</summary>
    public static MajorWoundRuleset Load()
    {
        var assembly = typeof(NoirMajorWoundRuleset).Assembly;
        using var stream = assembly.GetManifestResourceStream("Brp.Data.major-wound-ruleset.json")
            ?? throw new InvalidOperationException("The major wound ruleset data resource is missing.");
        var data = JsonSerializer.Deserialize<MajorWoundRulesetData>(stream, SerializerOptions)
            ?? throw new InvalidOperationException("The major wound ruleset data is empty.");

        var rows = data.MajorWoundTable.Select(BuildRow);
        return new MajorWoundRuleset(
            table: new MajorWoundTable(rows),
            fatalWoundRescueWindowRounds: data.FatalWoundRescueWindowRounds,
            collapseUnconsciousHours: data.CollapseUnconsciousHours);
    }

    private static MajorWoundRow BuildRow(MajorWoundRowData row)
    {
        var losses = row.Losses.Select(loss => new MajorWoundLoss(
            new CharacteristicId(loss.Characteristic), DiceExpression.Parse(loss.DiceFormula))).ToList();

        var choice = row.GamemasterChoice is null
            ? null
            : new MajorWoundGamemasterChoice(row.GamemasterChoice.Count, DiceExpression.Parse(row.GamemasterChoice.DiceFormula));

        return new MajorWoundRow(
            row.Minimum, row.Maximum, losses, choice, row.ReducesMovement, row.RequiresLimbSide, row.AbleToFight);
    }

    private sealed class MajorWoundRulesetData
    {
        public required int FatalWoundRescueWindowRounds { get; init; }

        public required int CollapseUnconsciousHours { get; init; }

        public required IReadOnlyList<MajorWoundRowData> MajorWoundTable { get; init; }
    }

    private sealed class MajorWoundRowData
    {
        public required int Minimum { get; init; }

        public required int Maximum { get; init; }

        public required IReadOnlyList<MajorWoundLossData> Losses { get; init; }

        public MajorWoundGamemasterChoiceData? GamemasterChoice { get; init; }

        public required bool ReducesMovement { get; init; }

        public required bool RequiresLimbSide { get; init; }

        public required bool AbleToFight { get; init; }
    }

    private sealed class MajorWoundLossData
    {
        public required string Characteristic { get; init; }

        public required string DiceFormula { get; init; }
    }

    private sealed class MajorWoundGamemasterChoiceData
    {
        public required int Count { get; init; }

        public required string DiceFormula { get; init; }
    }
}
