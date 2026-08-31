namespace Brp.Core.Contests;

/// <summary>
/// The named gamemaster-adjudication points the Ch 7 situational combat spot rules leave open.
/// Each member is a decision the source book explicitly hands to the gamemaster ("at the
/// gamemaster's discretion", "the gamemaster should determine", "the gamemaster may allow")
/// rather than resolving mechanically. Naming them as first-class ids is the same discipline
/// <see cref="OpposedRollDecisionId"/> follows: a rules engine that hardcodes these calls
/// silently is lying about what the book actually settles. The canonical kebab-case id for each
/// (the string a GM tool, log, or authored policy keys on) is given in its summary and returned
/// by <see cref="SpotRuleDecisionIds.CanonicalId"/>.
/// </summary>
public enum SpotRuleDecisionId
{
    /// <summary>
    /// Canonical id <c>cover-penetration</c>. Ch 7, "Cover" (p.169): when an attack strikes the
    /// covering obstacle rather than the target, "damage should be rolled to see if it bypasses
    /// the cover and goes through to the intended target (see Damage to Inanimate Objects). Roll
    /// damage only when it makes sense." Whether the shot's damage penetrates the cover to reach
    /// the target is a gamemaster call weighed against the obstacle's armor value and hit points.
    /// A post-roll ruling: it is consulted only once the attack has landed on the cover, and the
    /// damage arithmetic it feeds is out of scope for the spot-rule modifier producer (piece D /
    /// a deferred inanimate-object damage rule owns that). This port names the decision so the
    /// spot-rule layer does not silently assert "the cover always stops it" or "always lets it
    /// through."
    /// </summary>
    CoverPenetration,

    /// <summary>
    /// Canonical id <c>cover-extent</c>. Ch 7, "Cover" (p.169): "If hit locations are used, you
    /// should announce what portions of your character's body are behind cover before the
    /// gamemaster rolls for an attack, with the gamemaster deciding how much cover the obstacle
    /// allows." How much of the target the obstacle screens -- and, with hit locations, which
    /// regions are protected -- is a gamemaster call. Hit locations are a deferred piece and out
    /// of scope here, so this port carries only the coarse extent (<see cref="CoverExtentRuling"/>)
    /// rather than a per-location map; it exists so the discretion is named now and can be
    /// enriched when hit locations land, instead of being silently fixed.
    /// </summary>
    CoverExtent,

    /// <summary>
    /// Canonical id <c>darkness-severity</c>. Ch 7, "Darkness" (p.169): "If your character is
    /// fighting in darkness, whether semi-darkness or pitch black, see Situational Modifiers for
    /// modifiers." Which tier of the Ch 5 Situational Modifiers "Environment" row (p.133) applies
    /// -- <see cref="DarknessSeverity.SemiDarkness"/> (darkness, -20%) or
    /// <see cref="DarknessSeverity.PitchBlack"/> (pitch black, -50%) -- is the gamemaster's read
    /// of the scene. This is a pre-roll ruling: its result selects the situational penalty the
    /// spot-rule resolver then produces.
    /// </summary>
    DarknessSeverity,

    /// <summary>
    /// Canonical id <c>backstab-helpless-reprieve</c>. Ch 7, "Backstabs and Helpless Opponents"
    /// (p.164): against a helpless target "the gamemaster may allow the target a POW×1 roll to
    /// determine if some lucky incident occurs that stays the attacker's hand for the duration of
    /// the combat round." Whether the helpless target gets that reprieve -- and thus whether the
    /// otherwise-automatic attack happens at all this round -- is a gamemaster call. A pre-action
    /// ruling: the caller checks it before the attack resolves; when it grants a reprieve, no
    /// attack roll is made this round.
    /// </summary>
    BackstabHelplessReprieve,

    /// <summary>
    /// Canonical id <c>firing-into-combat-stray-target</c>. Ch 7, "Firing Into Combat" (p.173):
    /// when a shot into a melee "rolls a number between their skill rating and their modified
    /// chance (-20%...), the gamemaster should randomly determine which of the other potential
    /// targets was struck, by having all potential targets make a Luck roll and choosing the
    /// biggest failure (or most marginal success) as the unlucky target." Which bystander (if
    /// any) takes the stray shot is a gamemaster call, resolved via Luck rolls the spot-rule layer
    /// does not own. A post-roll ruling, consulted only when the attack roll lands in that risk
    /// band; "the attacker is not eligible for an experience check" on such a shot.
    /// </summary>
    FiringIntoCombatStrayTarget,
}

/// <summary>Canonical kebab-case ids for the <see cref="SpotRuleDecisionId"/> ports.</summary>
public static class SpotRuleDecisionIds
{
    /// <summary>
    /// The canonical kebab-case id for <paramref name="decisionId"/> -- the stable string a GM
    /// tool, authored policy, or log keys on (e.g. <c>cover-penetration</c>), matching the ids
    /// named in Issue #50 and ADR 0018.
    /// </summary>
    public static string CanonicalId(SpotRuleDecisionId decisionId) => decisionId switch
    {
        SpotRuleDecisionId.CoverPenetration => "cover-penetration",
        SpotRuleDecisionId.CoverExtent => "cover-extent",
        SpotRuleDecisionId.DarknessSeverity => "darkness-severity",
        SpotRuleDecisionId.BackstabHelplessReprieve => "backstab-helpless-reprieve",
        SpotRuleDecisionId.FiringIntoCombatStrayTarget => "firing-into-combat-stray-target",
        _ => throw new ArgumentOutOfRangeException(nameof(decisionId), decisionId, "Unknown spot-rule decision id."),
    };
}

/// <summary>
/// The severity of darkness a fight takes place in, mapping to a tier of the Ch 5 Situational
/// Modifiers "Environment" row (p.133) as directed by Ch 7, "Darkness" (p.169). Lives in
/// <c>Brp.Core.Contests</c> rather than the combat-rules layer because the
/// <see cref="SpotRuleDecisionId.DarknessSeverity"/> port both produces and consumes it, and a
/// <c>Brp.Core</c> port cannot depend on <c>Brp.Rules</c> (AGENTS.md invariant 6).
/// </summary>
public enum DarknessSeverity
{
    /// <summary>
    /// Semi-darkness -- the Ch 5 Environment tier "Unpleasant or unsanitary conditions, unsteady
    /// footing, darkness, bad weather, etc." at -20% (p.133).
    /// </summary>
    SemiDarkness,

    /// <summary>
    /// Pitch black -- the Ch 5 Environment tier "Distracting environment, highly unstable ground,
    /// pitch black, stormy, etc." at -50% (p.133).
    /// </summary>
    PitchBlack,
}

/// <summary>
/// Whether damage penetrates cover to reach the target, for the
/// <see cref="SpotRuleDecisionId.CoverPenetration"/> ruling (Ch 7, p.169). Deliberately a coarse
/// yes/no rather than a damage figure: the damage arithmetic (obstacle armor and hit points, the
/// deferred "Damage to Inanimate Objects" rule) is out of scope for the spot-rule producer.
/// </summary>
public enum CoverPenetrationRuling
{
    /// <summary>The obstacle absorbs the shot; nothing reaches the target this attack.</summary>
    StoppedByCover,

    /// <summary>The shot's damage passes through the cover and reaches the intended target.</summary>
    PenetratesToTarget,
}

/// <summary>
/// How much of the target the obstacle screens, for the
/// <see cref="SpotRuleDecisionId.CoverExtent"/> ruling (Ch 7, p.169). Coarse rather than a
/// per-hit-location map because hit locations are a deferred piece and out of scope here.
/// </summary>
public enum CoverExtentRuling
{
    /// <summary>No effective cover; the target is fully exposed (no Cover spot rule applies).</summary>
    Exposed,

    /// <summary>The target is partially covered -- the premise of the Ch 7 Cover rule.</summary>
    PartiallyProtected,

    /// <summary>The obstacle screens the target entirely from this line of attack.</summary>
    FullyProtected,
}

/// <summary>
/// Whether a helpless target receives the Ch 7 (p.164) POW×1 reprieve, for the
/// <see cref="SpotRuleDecisionId.BackstabHelplessReprieve"/> ruling.
/// </summary>
public enum HelplessReprieveRuling
{
    /// <summary>No reprieve; the attack against the helpless target proceeds this round.</summary>
    NoReprieve,

    /// <summary>
    /// A lucky incident stays the attacker's hand for the duration of the combat round; no attack
    /// is made this round.
    /// </summary>
    ReprievedThisRound,
}

/// <summary>
/// Which bystander takes a stray shot fired into a melee, for the
/// <see cref="SpotRuleDecisionId.FiringIntoCombatStrayTarget"/> ruling (Ch 7, p.173).
/// </summary>
/// <param name="StruckBystanderIndex">
/// The zero-based index of the struck bystander among the potential other targets, or
/// <see langword="null"/> when no bystander is struck. The book resolves the selection with Luck
/// rolls the spot-rule layer does not own; this ruling only reports the outcome.
/// </param>
public readonly record struct StrayTargetRuling(int? StruckBystanderIndex);

/// <summary>
/// A gamemaster-discretion port for the Ch 7 situational combat spot rules, modeled -- like
/// <see cref="IAdjudicator"/> for opposed rolls -- as a first-class interface rather than a set of
/// silent hardcoded choices. Each method answers one <see cref="SpotRuleDecisionId"/> the book
/// leaves open. A GM tool can prompt a human; an unattended simulation can supply an authored
/// policy; tests supply a deterministic stub. See <c>docs/decisions/0018-spot-rules.md</c>.
/// <para>
/// The return types are ordinary <c>Brp.Core.Contests</c> values, not <c>Brp.Rules.Combat</c>
/// types, so this port stays within <c>Brp.Core</c> and does not invert the layer dependency
/// (AGENTS.md invariant 6). The methods carry minimal context today; richer context (the actual
/// obstacle, the bystanders' characteristics for the Luck rolls) is a future concern for whichever
/// piece wires these ports into a running encounter.
/// </para>
/// </summary>
public interface ISpotRuleAdjudicator
{
    /// <summary>
    /// Decides which darkness tier applies to a fight in the dark
    /// (<see cref="SpotRuleDecisionId.DarknessSeverity"/>). Pre-roll: the result selects the
    /// situational penalty the Darkness spot rule then produces.
    /// </summary>
    DarknessSeverity DecideDarknessSeverity();

    /// <summary>
    /// Decides whether a shot that struck cover penetrates to the target
    /// (<see cref="SpotRuleDecisionId.CoverPenetration"/>). Post-roll; the damage it feeds is out
    /// of scope for the spot-rule producer.
    /// </summary>
    CoverPenetrationRuling DecideCoverPenetration();

    /// <summary>
    /// Decides how much of the target the obstacle screens
    /// (<see cref="SpotRuleDecisionId.CoverExtent"/>).
    /// </summary>
    CoverExtentRuling DecideCoverExtent();

    /// <summary>
    /// Decides whether a helpless target receives the POW×1 reprieve that stays the attacker's
    /// hand this round (<see cref="SpotRuleDecisionId.BackstabHelplessReprieve"/>). Pre-action.
    /// </summary>
    HelplessReprieveRuling DecideBackstabHelplessReprieve();

    /// <summary>
    /// Decides which bystander (if any) takes a stray shot fired into a melee
    /// (<see cref="SpotRuleDecisionId.FiringIntoCombatStrayTarget"/>). Post-roll.
    /// </summary>
    /// <param name="bystanderCount">
    /// The number of other potential targets in or around the melee. A returned index must be in
    /// <c>[0, bystanderCount)</c>, or the ruling may report no bystander struck.
    /// </param>
    StrayTargetRuling DecideFiringIntoCombatStrayTarget(int bystanderCount);
}

/// <summary>
/// The documented default policy for every <see cref="SpotRuleDecisionId"/>: the most
/// minimal-assumption answer to "the book does not say," mirroring how
/// <see cref="DefaultAdjudicator"/> asserts nothing beyond the book for opposed rolls. Each
/// default is the conservative reading that adds no fact the book did not state, so an unattended
/// run is deterministic and neutral; a table with a house rule or a human gamemaster should supply
/// its own <see cref="ISpotRuleAdjudicator"/> instead.
/// </summary>
public sealed class DefaultSpotRuleAdjudicator : ISpotRuleAdjudicator
{
    /// <summary>
    /// Defaults to <see cref="DarknessSeverity.SemiDarkness"/> -- the lesser (-20%) of the two
    /// tiers. Choosing the milder condition asserts the least: it does not silently treat every
    /// dark scene as pitch black.
    /// </summary>
    public DarknessSeverity DecideDarknessSeverity() => DarknessSeverity.SemiDarkness;

    /// <summary>
    /// Defaults to <see cref="CoverPenetrationRuling.StoppedByCover"/> -- the cover did its job.
    /// This matches the base Cover rule's own outcome for a shot that lands on the obstacle (Ch 7,
    /// p.169: "the attack has hit the obstacle or cover rather than the target"), and adds no
    /// assumption about the obstacle's armor or the weapon's damage that the spot-rule layer does
    /// not model.
    /// </summary>
    public CoverPenetrationRuling DecideCoverPenetration() => CoverPenetrationRuling.StoppedByCover;

    /// <summary>
    /// Defaults to <see cref="CoverExtentRuling.PartiallyProtected"/> -- the premise of the Ch 7
    /// Cover rule itself ("If a target is partially covered...").
    /// </summary>
    public CoverExtentRuling DecideCoverExtent() => CoverExtentRuling.PartiallyProtected;

    /// <summary>
    /// Defaults to <see cref="HelplessReprieveRuling.NoReprieve"/> -- the mechanical baseline. The
    /// book frames the reprieve as something the gamemaster "may allow," so the default is to grant
    /// nothing beyond the printed rule (the attack proceeds).
    /// </summary>
    public HelplessReprieveRuling DecideBackstabHelplessReprieve() => HelplessReprieveRuling.NoReprieve;

    /// <summary>
    /// Defaults to no bystander struck (a <see langword="null"/> index). Resolving which ally is
    /// hit requires the bystanders' Luck rolls, which this default cannot perform; asserting "no
    /// stray hit" adds no unmodeled fact.
    /// </summary>
    public StrayTargetRuling DecideFiringIntoCombatStrayTarget(int bystanderCount) => new(null);
}
