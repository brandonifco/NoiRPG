using System.Text.Json;
using Brp.Core.Resolution;
using Brp.Rules.Combat;

namespace Brp.Data;

/// <summary>
/// Loads NoiRPG's attack/defense matrix from embedded JSON. Sourced: Ch 6: Combat, "Attack and
/// Defense Matrix" (p.147) -- transcribed row-for-row, every cell. Recorded in
/// <c>docs/decisions/0016-attack-defense-matrix.md</c>.
/// </summary>
public static class NoirAttackDefenseMatrixRuleset
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>Loads a new, immutable attack/defense matrix ruleset from the shipped data.</summary>
    public static AttackDefenseMatrixRuleset Load()
    {
        var assembly = typeof(NoirAttackDefenseMatrixRuleset).Assembly;
        using var stream = assembly.GetManifestResourceStream("Brp.Data.attack-defense-matrix-ruleset.json")
            ?? throw new InvalidOperationException("The attack/defense matrix ruleset data resource is missing.");
        var data = JsonSerializer.Deserialize<AttackDefenseMatrixRulesetData>(stream, SerializerOptions)
            ?? throw new InvalidOperationException("The attack/defense matrix ruleset data is empty.");

        var cells = data.MatrixCells
            .Select(ParseCell)
            .ToList();

        var undefendedOutcomes = data.UndefendedOutcomes
            .ToDictionary(
                entry => ParseSuccessLevel(entry.AttackerGrade),
                entry => new AttackDefenseOutcome(
                    LandedGrade: ParseLandedGrade(entry.LandedGrade),
                    ArmorTreatment: ParseArmorTreatment(entry.ArmorTreatment),
                    ParryWeaponDamage: null,
                    DefenderRollsOnFumbleTable: false,
                    AttackerRollsOnFumbleTable: false,
                    SourceText: entry.SourceNote));

        return new AttackDefenseMatrixRuleset(cells, undefendedOutcomes);
    }

    private static AttackDefenseMatrixCell ParseCell(MatrixCellData cell) => new(
        AttackerGrade: ParseSuccessLevel(cell.AttackerGrade),
        DefenderGrade: cell.DefenderGrade is null ? null : ParseSuccessLevel(cell.DefenderGrade),
        Outcome: new AttackDefenseOutcome(
            LandedGrade: ParseLandedGrade(cell.LandedGrade),
            ArmorTreatment: ParseArmorTreatment(cell.ArmorTreatment),
            ParryWeaponDamage: cell.ParryWeaponDamage is null
                ? null
                : new ParryWeaponDamage(ParseDamagedParty(cell.ParryWeaponDamage.DamagedParty), cell.ParryWeaponDamage.Points),
            DefenderRollsOnFumbleTable: cell.DefenderRollsOnFumbleTable,
            AttackerRollsOnFumbleTable: cell.AttackerRollsOnFumbleTable,
            SourceText: cell.Result));

    private static SuccessLevel ParseSuccessLevel(string value) => value switch
    {
        "critical" => SuccessLevel.Critical,
        "special" => SuccessLevel.Special,
        "success" => SuccessLevel.Success,
        "failure" => SuccessLevel.Failure,
        "fumble" => SuccessLevel.Fumble,
        _ => throw new InvalidOperationException($"Unknown success grade '{value}'."),
    };

    private static LandedGrade ParseLandedGrade(string value) => value switch
    {
        "miss" => LandedGrade.Miss,
        "normal" => LandedGrade.Normal,
        "special" => LandedGrade.Special,
        "critical" => LandedGrade.Critical,
        _ => throw new InvalidOperationException($"Unknown landed grade '{value}'."),
    };

    private static ArmorTreatment ParseArmorTreatment(string value) => value switch
    {
        "notApplicable" => ArmorTreatment.NotApplicable,
        "subtracted" => ArmorTreatment.Subtracted,
        "bypassed" => ArmorTreatment.Bypassed,
        "doesNotApply" => ArmorTreatment.DoesNotApply,
        _ => throw new InvalidOperationException($"Unknown armor treatment '{value}'."),
    };

    private static DamagedParty ParseDamagedParty(string value) => value switch
    {
        "attacker" => DamagedParty.Attacker,
        "defender" => DamagedParty.Defender,
        _ => throw new InvalidOperationException($"Unknown damaged party '{value}'."),
    };

    private sealed class AttackDefenseMatrixRulesetData
    {
        public required IReadOnlyList<MatrixCellData> MatrixCells { get; init; }

        public required IReadOnlyList<UndefendedOutcomeData> UndefendedOutcomes { get; init; }
    }

    private sealed class MatrixCellData
    {
        public required string AttackerGrade { get; init; }

        public string? DefenderGrade { get; init; }

        public required string LandedGrade { get; init; }

        public required string ArmorTreatment { get; init; }

        public ParryWeaponDamageData? ParryWeaponDamage { get; init; }

        public required bool DefenderRollsOnFumbleTable { get; init; }

        public required bool AttackerRollsOnFumbleTable { get; init; }

        public required string Result { get; init; }
    }

    private sealed class ParryWeaponDamageData
    {
        public required string DamagedParty { get; init; }

        public required int Points { get; init; }
    }

    private sealed class UndefendedOutcomeData
    {
        public required string AttackerGrade { get; init; }

        public required string LandedGrade { get; init; }

        public required string ArmorTreatment { get; init; }

        public required string SourceNote { get; init; }
    }
}
