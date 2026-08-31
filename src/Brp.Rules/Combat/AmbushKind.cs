namespace Brp.Rules.Combat;

/// <summary>
/// The four ambush cases of Ch 7: Spot Rules, "Ambushes" (p.162). An ambush requires a successful
/// Stealth roll opposed by the target's Listen, Sense, or Spot; the resulting case determines both
/// the attacker's attack modifier and the target's defense. See <see cref="SpotRuleResolver.Ambush"/>.
/// </summary>
public enum AmbushKind
{
    /// <summary>
    /// Attackers using missile weapons and <em>not</em> seen (p.162): "the attackers get a free
    /// round of Easy attacks. The target(s) cannot dodge or parry this initial round of attacks."
    /// Attack Easy; defense forbidden.
    /// </summary>
    MissileUnseen,

    /// <summary>
    /// Attackers using missile weapons and seen (p.162): "the attackers get a free round of Easy
    /// attacks, but the targets can dodge or parry this initial round." Attack Easy; defense
    /// normal.
    /// </summary>
    MissileSeen,

    /// <summary>
    /// Attackers using hand-to-hand weapons who failed their Stealth rolls, and the target fails
    /// the Easy Listen/Sense/Spot roll to notice them (p.162): "attacks against them are Easy and
    /// any parries or dodges they make are Difficult." Attack Easy; defense Difficult.
    /// </summary>
    HandToHandTargetUnaware,

    /// <summary>
    /// Attackers using hand-to-hand weapons whom the target notices with a successful
    /// Listen/Sense/Spot roll (p.162): "the attackers' skill ratings are unmodified and the targets
    /// can parry or Dodge normally but cannot retaliate or move until the next combat round."
    /// Attack unmodified; defense normal. The "cannot retaliate or move" clause is a turn-economy
    /// effect on the target's next action, not a modifier on any single roll, and is not modeled as
    /// a <see cref="Brp.Core.Modifiers.Modifier"/> here (see <see cref="SpotRuleResolver.Ambush"/>).
    /// </summary>
    HandToHandTargetAware,
}
