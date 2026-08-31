namespace Brp.Rules.Combat;

/// <summary>
/// One combatant's request to act in a combat round's Action phase (Ch 6, p.143), before the
/// effective DEX rank and weapon-type tiebreak have been applied. See <see cref="CombatRound.Create"/>.
/// </summary>
/// <param name="CombatantId">
/// Identifies whose action this is. Opaque to this layer -- a name, a character ID, anything the
/// caller finds convenient -- since <c>Brp.Rules</c> takes no dependency on a specific character
/// or engine representation.
/// </param>
/// <param name="BaseDexRank">
/// The combatant's DEX rank before this round's movement and penalties -- numerically the DEX
/// characteristic value itself (Ch 6, p.142; see <see cref="CombatRoundRuleset.DexRankSourceCharacteristic"/>).
/// </param>
/// <param name="MovementMeters">
/// How far the combatant moves this round, in meters, for <see cref="EffectiveDexRankCalculator.ApplyMovement"/>
/// to fraction the DEX rank against (Ch 6, "Move," p.144).
/// </param>
/// <param name="FlatDexRankPenalty">
/// The sum of any flat penalties applying after movement -- drawing a weapon combined with
/// another action, and/or this action's position in a multi-attack sequence (Ch 6, "Noncombat
/// Action" and "Attack," p.144). Establishing which penalties apply and why is the caller's
/// concern; see <see cref="EffectiveDexRankCalculator.Compute"/>.
/// </param>
/// <param name="WeaponTier">
/// The weapon-type tier used to break a tie with another combatant at the same effective DEX
/// rank (Ch 6, "Action," p.143). See <see cref="WeaponTypeTier"/>.
/// </param>
public readonly record struct CombatActionRequest(
    string CombatantId,
    int BaseDexRank,
    int MovementMeters,
    int FlatDexRankPenalty,
    WeaponTypeTier WeaponTier);
