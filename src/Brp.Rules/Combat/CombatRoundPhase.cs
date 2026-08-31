namespace Brp.Rules.Combat;

/// <summary>
/// A phase of a combat round, per Ch 6: Combat, "Combat Round Phases" (p.142).
/// <para>
/// The book prints <em>four</em> phases in this order: Statements, Powers, Action, Resolution
/// (p.142: "A combat round consists of four phases: Statements, Powers, Action, and Resolution.
/// These always occur in the same order."). NoiRPG implements only three -- <see cref="Statements"/>,
/// <see cref="Action"/>, <see cref="Resolution"/> -- a deliberate, owner-approved scope deviation:
/// the Powers phase (p.143) exists solely to sequence instantaneous-power activation by INT rank,
/// and NoiRPG cuts the entire powers/magic subsystem that phase serves
/// (<c>orc-scope-filter.md</c>, "Chapter 4: Powers -- cut in full"). With no powers to activate,
/// the phase has no content in this game and is omitted rather than modelled as a permanent no-op.
/// See <c>docs/decisions/0015-combat-round.md</c>.
/// </para>
/// </summary>
public enum CombatRoundPhase
{
    /// <summary>
    /// Ch 6, "Statements" (p.142): players and the gamemaster announce, in DEX-rank order
    /// (highest first), what their characters plan to do this round.
    /// </summary>
    Statements,

    /// <summary>
    /// Ch 6, "Action" (p.143): combatants act on their DEX ranks, attacks going in weapon-type
    /// order within a tied rank (missile, then long, then medium, then short/unarmed).
    /// </summary>
    Action,

    /// <summary>
    /// Ch 6, "Resolution" (p.145): attack, parry, and dodge rolls are compared on the Attack and
    /// Defense Matrix to determine the result of the round's actions. Resolving those rolls is
    /// piece C's concern (<see cref="AttackDefenseResolver"/>, #49); this phase is modelled here
    /// only as a named step a round passes through.
    /// </summary>
    Resolution,
}
