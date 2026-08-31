namespace Brp.Rules.Combat;

/// <summary>
/// One combatant's place in the Action phase's ordered sequence, after effective DEX rank and the
/// weapon-type tiebreak have been resolved. This is the seam piece C (<see cref="AttackDefenseResolver"/>)
/// consumes: a <see cref="CombatantTurn"/> says <em>when</em> a combatant acts, not what happens
/// when they do -- resolving an attack, parry, or dodge on this turn is out of scope for this
/// piece. See <c>docs/decisions/0015-combat-round.md</c> and <c>docs/decisions/0016-attack-defense-matrix.md</c>.
/// </summary>
/// <param name="CombatantId">Identifies whose turn this is; see <see cref="CombatActionRequest.CombatantId"/>.</param>
/// <param name="EffectiveDexRank">
/// The DEX rank this turn occurs on, after movement and flat penalties (Ch 6, pp.143-144).
/// </param>
/// <param name="WeaponTier">The weapon-type tier that placed this turn among others tied on DEX rank.</param>
public readonly record struct CombatantTurn(string CombatantId, int EffectiveDexRank, WeaponTypeTier WeaponTier);
