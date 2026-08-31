namespace Brp.Rules.Combat;

/// <summary>
/// The two cases of Ch 7: Spot Rules, "Backstabs and Helpless Opponents" (p.164). Both make the
/// attack Easy; they differ in whether, and how, the target may defend. See
/// <see cref="SpotRuleResolver.Backstab"/>.
/// </summary>
public enum BackstabKind
{
    /// <summary>
    /// An attack on the unprotected back of a target in hand-to-hand combat (p.164): "that one
    /// attack is Easy. If the target succeeds in a Difficult Listen or Sense roll, they can make a
    /// Difficult Dodge or parry attempt, but only if they have any remaining opportunities for
    /// defense. No additional damage is done by such an attack." Attack Easy; defense Difficult
    /// only if the target detected the attacker (and has a defense left), otherwise none.
    /// </summary>
    UnprotectedBack,

    /// <summary>
    /// An attack on a helpless target -- "unconscious, asleep, or restrained entirely" (p.164):
    /// "the attack is Easy and they cannot make a dodge or parry attempt against the attack."
    /// Attack Easy; defense forbidden. Subject to the gamemaster's optional POW×1 reprieve
    /// (<see cref="Brp.Core.Contests.SpotRuleDecisionId.BackstabHelplessReprieve"/>).
    /// </summary>
    Helpless,
}
