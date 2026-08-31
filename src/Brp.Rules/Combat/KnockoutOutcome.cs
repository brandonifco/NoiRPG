using Brp.Core.Dice;

namespace Brp.Rules.Combat;

/// <summary>
/// The result of a declared knockout attack (Ch 7: Spot Rules, "Knockout Attacks", p.174) --
/// <see cref="DamageResolver.ResolveKnockoutAttack"/>'s output.
/// </summary>
/// <param name="KnockedOut">
/// <see langword="true"/> only on the major-wound branch: the rolled damage (after armor) was
/// equivalent to a major wound, so the target takes 1 damage and is knocked out.
/// </param>
/// <param name="DamageDealt">
/// The damage actually applied: 1 on the knockout branch; the weapon's minimum damage (after
/// armor) on the non-knockout "equivalent to a minor wound" branch; 0 if the attack missed.
/// </param>
/// <param name="DurationRounds">
/// The rolled knockout duration in rounds, present only when <see cref="KnockedOut"/> is
/// <see langword="true"/>.
/// </param>
/// <param name="DurationRoll">The underlying dice roll behind <see cref="DurationRounds"/>.</param>
/// <param name="UnderlyingRoll">
/// The damage roll used to determine minor-vs-major wound equivalence, kept for citation --
/// this is the roll Ch 7 p.174 means by "damage is rolled to determine the potential for a
/// knockout," not damage that was itself applied verbatim.
/// </param>
public sealed record KnockoutOutcome(
    bool KnockedOut,
    int DamageDealt,
    int? DurationRounds,
    DiceRoll? DurationRoll,
    DamageRoll UnderlyingRoll);
