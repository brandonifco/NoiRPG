using Brp.Core.Contests;
using Brp.Core.Dice;

namespace Brp.Rules.Combat;

/// <summary>
/// One row of Ch 6: Combat, "Conditions of Medical Care" (p.157), keyed by its
/// <see cref="MedicalCareTier"/>. Carries the printed cell's mechanical effect on the weekly healing
/// rate: whether a gating caregiver roll is required (and its difficulty), the natural healing the
/// row grants, the extra damage a fumbled gating roll inflicts, and whether the row permits further
/// possible healing beyond the natural rate. The descriptive "Medical Conditions" cell is kept as
/// free text for the game layer to render; the mechanical fields are what the printed grid makes
/// normative. Mirrors <see cref="MajorWoundRow"/> / <see cref="IllnessSeverityBand"/>.
/// </summary>
/// <param name="Tier">The care tier this row describes (the table's key).</param>
/// <param name="Conditions">
/// The printed "Medical Conditions" cell, summarized for the game layer to render. Narrative only.
/// </param>
/// <param name="RequiresCaregiverRoll">
/// Whether any healing requires a successful caregiver roll first (true only for the poor tier).
/// </param>
/// <param name="CaregiverRollDifficulty">
/// The difficulty of the gating caregiver roll (<see cref="HealingRollDifficulty.Difficult"/> for the
/// poor tier, <see cref="HealingRollDifficulty.None"/> otherwise).
/// </param>
/// <param name="NaturalHealing">
/// The hit points healed naturally per week when the row's conditions are met (1D3 on every printed
/// row -- the same normal rate as "Healing Naturally", p.157).
/// </param>
/// <param name="FumbleAdditionalDamage">
/// The additional damage a fumbled gating roll inflicts (1D3 for the poor tier; <see langword="null"/>
/// for tiers with no gating roll).
/// </param>
/// <param name="AllowsAdditionalHealing">
/// Whether a further successful First Aid or Medicine use allows possible additional healing beyond
/// the natural rate (true only for the excellent tier). The additional amount is left to that separate
/// skill use -- the book does not print one, so this row does not invent it.
/// </param>
public sealed record MedicalCareRow(
    MedicalCareTier Tier,
    string Conditions,
    bool RequiresCaregiverRoll,
    HealingRollDifficulty CaregiverRollDifficulty,
    DiceExpression NaturalHealing,
    DiceExpression? FumbleAdditionalDamage,
    bool AllowsAdditionalHealing);
