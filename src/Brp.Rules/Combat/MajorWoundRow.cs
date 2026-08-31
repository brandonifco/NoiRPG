using Brp.Core.Abilities;
using Brp.Core.Dice;

namespace Brp.Rules.Combat;

/// <summary>
/// One fixed characteristic loss on a <see cref="MajorWoundRow"/> (Ch 6: Combat, "Major Wounds
/// Table", pp.155-156): a characteristic and the dice removed from it on a failed Luck roll.
/// </summary>
/// <param name="Characteristic">The characteristic the row lowers.</param>
/// <param name="Dice">The points removed (e.g. 1D3, 1D6), rolled when the loss is applied.</param>
public sealed record MajorWoundLoss(CharacteristicId Characteristic, DiceExpression Dice);

/// <summary>
/// The gamemaster-chosen loss on the 00 row of the Major Wounds Table (Ch 6, p.156: "Remove 1D4
/// points each from four characteristics (gamemaster's discretion)"). The count and dice are fixed
/// by the table; which characteristics are struck is a
/// <see cref="Core.Contests.MajorWoundDecisionId.Characteristics"/> ruling.
/// </summary>
/// <param name="Count">How many distinct characteristics are struck (four, per the 00 row).</param>
/// <param name="Dice">The points removed from each chosen characteristic (1D4).</param>
public sealed record MajorWoundGamemasterChoice(int Count, DiceExpression Dice);

/// <summary>
/// One row of Ch 6: Combat, "Major Wounds Table" (pp.155-156), banded by the 1D100 result. Carries
/// the row's mechanical effect only -- the characteristic loss(es), whether MOV is reduced by that
/// loss, whether the row's limb is unspecified (needing a side ruling), and the still-able-to-fight
/// flag. The book's illustrative flavor text (multiple example causes per row) is not modeled: the
/// dice, MOV effect, and fight flag are what the printed grid makes normative. Mirrors
/// <see cref="IllnessSeverityBand"/> / <see cref="Core.Abilities.DamageModifierBand"/>.
/// </summary>
/// <param name="Minimum">The lowest 1D100 result this row covers (a printed 00 is 100).</param>
/// <param name="Maximum">The highest 1D100 result this row covers.</param>
/// <param name="Losses">
/// The fixed characteristic losses this row applies (one for most rows, three for the 99 row, none
/// for the 00 row, which uses <paramref name="GamemasterChoice"/> instead).
/// </param>
/// <param name="GamemasterChoice">
/// The gamemaster-chosen loss for the 00 row, or <see langword="null"/> for every other row.
/// </param>
/// <param name="ReducesMovement">
/// Whether the row reduces MOV "by the same amount" as the characteristic loss (Ch 6, pp.155-156:
/// the DEX-loss rows 01-10 and 51-60, and the CON-loss rows 31-40 and 81-90). MOV is a flat value
/// the engine does not derive from characteristics (<see cref="AbilitySet.Movement"/>), so the
/// reduction is reported as a structured outcome for the caller to apply, not baked here.
/// </param>
/// <param name="RequiresLimbSide">
/// Whether the row's wound is to an unspecified limb needing a
/// <see cref="Core.Contests.MajorWoundDecisionId.LimbSide"/> ruling (the "left or right arm" 95-96
/// row). Narrative only -- the loss does not depend on the side.
/// </param>
/// <param name="AbleToFight">Whether the character can still fight after this permanent injury.</param>
public sealed record MajorWoundRow(
    int Minimum,
    int Maximum,
    IReadOnlyList<MajorWoundLoss> Losses,
    MajorWoundGamemasterChoice? GamemasterChoice,
    bool ReducesMovement,
    bool RequiresLimbSide,
    bool AbleToFight)
{
    /// <summary>Whether this row covers the given 1D100 result.</summary>
    public bool Contains(int roll) => roll >= Minimum && roll <= Maximum;
}
