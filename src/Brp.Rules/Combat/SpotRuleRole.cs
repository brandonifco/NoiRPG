namespace Brp.Rules.Combat;

/// <summary>
/// Which side of an interaction a spot-rule modifier is being produced for. Several Ch 7 spot
/// rules modify the attacker's attack roll and the defender's defense roll differently in the same
/// situation (an ambush makes the attack Easy while forbidding or hampering the defense), so the
/// producer needs to know whose roll it is asked to modify. See <see cref="SpotRuleResolver"/>.
/// </summary>
public enum SpotRuleRole
{
    /// <summary>The character making the attack whose roll the modifier applies to.</summary>
    Attacker,

    /// <summary>The character defending (dodging or parrying) whose roll the modifier applies to.</summary>
    Defender,
}
