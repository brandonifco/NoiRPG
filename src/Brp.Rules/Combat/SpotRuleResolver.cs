using Brp.Core.Contests;
using Brp.Core.Modifiers;
using Brp.Core.Primitives;

namespace Brp.Rules.Combat;

/// <summary>
/// Produces the <see cref="Modifier"/> contributions of the five in-scope situational combat spot
/// rules of Ch 7: Spot Rules -- Ambushes (p.162), Backstabs and Helpless Opponents (p.164), Cover
/// (p.169), Darkness (p.169), and Firing Into Combat (p.173). Each rule is a <em>modifier
/// producer</em> feeding ADR 0007's <see cref="ModifierPipeline"/>, not a new resolution path: a
/// rule that makes an action Easy or Difficult emits a <see cref="DifficultyModifier"/> so it takes
/// part in ADR 0007's non-stacking collapse (a spot-rule Difficult and a range-band Difficult halve
/// once, not twice; a point-blank Easy and a firing-while-engaged Difficult cancel); a rule that
/// applies a flat percentage emits a situational <see cref="AdditiveModifier"/> so its stated weight
/// survives the difficulty stage (Ch 5, "Modifying Action Rolls", p.132); and a rule that forbids an
/// action emits a <see cref="GateModifier"/>. The book's percentage values live in
/// <see cref="SpotRuleRuleset"/> (AGENTS.md invariant 7); the grade semantics live on
/// <see cref="ModifierPolicy"/>.
/// <para>
/// Mirrors <see cref="RangeBandResolver"/> in shape -- a static resolver whose per-case methods
/// return the contributions, plus an <see cref="Evaluate"/> that concatenates them with a roll's
/// other modifiers and runs the pipeline. Unlike range bands, no spot rule needs an exclusive
/// override, so the contributions are plain composable modifiers. The gamemaster-discretion points
/// each rule leaves open (Ch 7's "at the gamemaster's discretion" clauses) are named ports on
/// <see cref="ISpotRuleAdjudicator"/>; see <c>docs/decisions/0018-spot-rules.md</c>.
/// </para>
/// </summary>
public static class SpotRuleResolver
{
    /// <summary>
    /// The modifier(s) an ambush (Ch 7, "Ambushes", p.162) contributes to <paramref name="role"/>'s
    /// roll for the given <paramref name="kind"/>. The attacker's attack is Easy in every case
    /// except <see cref="AmbushKind.HandToHandTargetAware"/> (unmodified); the target's defense is
    /// forbidden outright (<see cref="AmbushKind.MissileUnseen"/>), Difficult
    /// (<see cref="AmbushKind.HandToHandTargetUnaware"/>), or unmodified (the other two).
    /// <para>
    /// Only the initial ambush round carries these modifiers: "In most cases, the target's armor
    /// defends normally" and, after the surprise round, normal combat with no surprise modifiers
    /// resumes (p.162). The <see cref="AmbushKind.HandToHandTargetAware"/> target's inability to
    /// "retaliate or move until the next combat round" is a turn-economy effect on its next action,
    /// not a modifier on this roll, and is not emitted here.
    /// </para>
    /// </summary>
    public static IReadOnlyList<Modifier> Ambush(AmbushKind kind, SpotRuleRole role, string source = "ambush")
    {
        ArgumentNullException.ThrowIfNull(source);

        var label = $"{source} (Ch 7, Ambushes)";
        return role switch
        {
            SpotRuleRole.Attacker => kind switch
            {
                AmbushKind.MissileUnseen or AmbushKind.MissileSeen or AmbushKind.HandToHandTargetUnaware =>
                    [DifficultyModifier.Easy(label)],
                AmbushKind.HandToHandTargetAware => [],
                _ => throw UnknownKind(kind),
            },
            SpotRuleRole.Defender => kind switch
            {
                AmbushKind.MissileUnseen => [new GateModifier($"{label}: cannot dodge or parry", GateKind.Impossible)],
                AmbushKind.HandToHandTargetUnaware => [DifficultyModifier.Difficult(label)],
                AmbushKind.MissileSeen or AmbushKind.HandToHandTargetAware => [],
                _ => throw UnknownKind(kind),
            },
            _ => throw UnknownRole(role),
        };
    }

    /// <summary>
    /// The modifier(s) a backstab or helpless-opponent attack (Ch 7, "Backstabs and Helpless
    /// Opponents", p.164) contributes to <paramref name="role"/>'s roll. Both kinds make the
    /// attacker's attack Easy and do <em>no additional damage</em> (the Easy grade is the whole
    /// benefit). The defender's options differ:
    /// <list type="bullet">
    /// <item><see cref="BackstabKind.UnprotectedBack"/>: a Difficult Dodge/parry only if the target
    /// detected the attacker (a successful Difficult Listen/Sense) and has a defense left --
    /// <paramref name="defenderDetectedAttacker"/> models that detection; otherwise the target gets
    /// no defense this attack.</item>
    /// <item><see cref="BackstabKind.Helpless"/>: the target "cannot make a dodge or parry attempt"
    /// regardless -- <paramref name="defenderDetectedAttacker"/> does not apply. The optional POW×1
    /// reprieve is the <see cref="SpotRuleDecisionId.BackstabHelplessReprieve"/> port's concern, not
    /// a modifier, and gates the whole attack rather than adjusting this roll.</item>
    /// </list>
    /// </summary>
    /// <param name="kind">Which backstab case applies.</param>
    /// <param name="role">Whose roll to produce the modifier for.</param>
    /// <param name="defenderDetectedAttacker">
    /// For <see cref="BackstabKind.UnprotectedBack"/> only: whether the target made the Difficult
    /// Listen/Sense roll to notice the attacker (and so may attempt a Difficult defense). Ignored
    /// for <see cref="BackstabKind.Helpless"/>.
    /// </param>
    /// <param name="source">A label prefix identifying the attack, used in the rendered chain.</param>
    public static IReadOnlyList<Modifier> Backstab(
        BackstabKind kind, SpotRuleRole role, bool defenderDetectedAttacker = false, string source = "backstab")
    {
        ArgumentNullException.ThrowIfNull(source);

        var label = $"{source} (Ch 7, Backstabs and Helpless Opponents)";
        return role switch
        {
            SpotRuleRole.Attacker => [DifficultyModifier.Easy(label)],
            SpotRuleRole.Defender => kind switch
            {
                BackstabKind.UnprotectedBack when defenderDetectedAttacker => [DifficultyModifier.Difficult(label)],
                BackstabKind.UnprotectedBack => [new GateModifier($"{label}: undetected, no defense", GateKind.Impossible)],
                BackstabKind.Helpless => [new GateModifier($"{label}: helpless, cannot dodge or parry", GateKind.Impossible)],
                _ => throw UnknownKind(kind),
            },
            _ => throw UnknownRole(role),
        };
    }

    /// <summary>
    /// The modifier a partial-cover situation (Ch 7, "Cover", p.169) contributes: "any attacks on
    /// that target are Difficult." Only the attacker's attack roll is modified; cover is not a
    /// defensive action the defender rolls for, so <see cref="SpotRuleRole.Defender"/> contributes
    /// nothing here.
    /// <para>
    /// The rest of the Cover rule is post-roll and out of this producer's scope: a roll "over the
    /// adjusted amount to hit (but less than the normal skill rating)" hits the obstacle rather than
    /// the target, and whether the shot's damage then penetrates the cover
    /// (<see cref="SpotRuleDecisionId.CoverPenetration"/>) or which regions the obstacle screens
    /// with hit locations (<see cref="SpotRuleDecisionId.CoverExtent"/>) are gamemaster rulings, not
    /// modifiers. The "attack hits the cover" band is derivable from the resolved
    /// <see cref="ModifierChain"/> -- the interval between the Difficult effective chance and the
    /// unmodified base chance -- by a caller that needs it.
    /// </para>
    /// </summary>
    public static IReadOnlyList<Modifier> Cover(SpotRuleRole role, string source = "cover")
    {
        ArgumentNullException.ThrowIfNull(source);

        var label = $"{source} (Ch 7, Cover)";
        return role switch
        {
            SpotRuleRole.Attacker => [DifficultyModifier.Difficult(label)],
            SpotRuleRole.Defender => [],
            _ => throw UnknownRole(role),
        };
    }

    /// <summary>
    /// The situational modifier fighting in darkness (Ch 7, "Darkness", p.169) contributes. The rule
    /// directs the reader to the Ch 5 Situational Modifiers "Environment" row (p.133):
    /// <see cref="DarknessSeverity.SemiDarkness"/> is -20% and <see cref="DarknessSeverity.PitchBlack"/>
    /// is -50% (both data on <paramref name="ruleset"/>). When <paramref name="opponentDetected"/>
    /// is true -- the character made the Difficult Sense or Listen roll to detect the opponent -- the
    /// penalty is reduced by half (p.169), scaled by the ruleset's halving fraction and rounded
    /// toward zero so the reduction favors the roller. Emitted as a <em>situational</em>
    /// <see cref="AdditiveModifier"/> so the stated penalty is applied after any difficulty grade and
    /// is not itself doubled or halved (Ch 5, p.132; ADR 0007). Role-agnostic: darkness modifies
    /// whichever roll is made in the dark.
    /// </summary>
    /// <param name="severity">The darkness tier -- typically from
    /// <see cref="ISpotRuleAdjudicator.DecideDarknessSeverity"/>.</param>
    /// <param name="opponentDetected">Whether the Difficult Sense/Listen detection roll succeeded.</param>
    /// <param name="ruleset">The data-defined darkness penalties and halving fraction.</param>
    /// <param name="source">A label prefix used in the rendered chain.</param>
    public static IReadOnlyList<Modifier> Darkness(
        DarknessSeverity severity, bool opponentDetected, SpotRuleRuleset ruleset, string source = "darkness")
    {
        ArgumentNullException.ThrowIfNull(ruleset);
        ArgumentNullException.ThrowIfNull(source);

        var delta = severity switch
        {
            DarknessSeverity.SemiDarkness => ruleset.DarknessSemiDarknessModifier,
            DarknessSeverity.PitchBlack => ruleset.DarknessPitchBlackModifier,
            _ => throw new ArgumentOutOfRangeException(nameof(severity), severity, "Unknown darkness severity."),
        };

        var label = $"{source}: {(severity == DarknessSeverity.PitchBlack ? "pitch black" : "semi-darkness")} " +
            "(Ch 7, Darkness; Ch 5, Situational Modifiers)";

        if (opponentDetected)
        {
            // Ch 7 (p.169): "If successful, reduce the darkness modifier by half." Scale the penalty
            // magnitude by the ruleset's fraction, truncating toward zero so the reduced penalty
            // rounds in the roller's favor (the codebase convention for rounding a penalty), then
            // restore the sign.
            var magnitude = Math.Abs(delta) * ruleset.DarknessDetectionHalvingNumerator
                / ruleset.DarknessDetectionHalvingDenominator;
            delta = Math.Sign(delta) * magnitude;
            label = $"{source}: {(severity == DarknessSeverity.PitchBlack ? "pitch black" : "semi-darkness")}, " +
                "opponent detected, penalty halved (Ch 7, Darkness)";
        }

        return [new AdditiveModifier(label, delta, AdditiveKind.Situational)];
    }

    /// <summary>
    /// The modifier(s) firing a missile weapon in or around a melee (Ch 7, "Firing Into Combat",
    /// p.173) contributes. The book distinguishes two independent conditions, reconciling the two
    /// earlier partial citations (ADR 0007 recorded this as Difficult; <c>AdditiveModifier</c>'s
    /// remarks recorded it as -20%) -- both were half the rule:
    /// <list type="bullet">
    /// <item><paramref name="firingIntoMelee"/> -- the shot passes <em>into</em> a hand-to-hand
    /// combat others are engaged in: "Firing a missile weapon into combat is modified by -20%" -- a
    /// situational <see cref="AdditiveModifier"/> from <paramref name="ruleset"/>.</item>
    /// <item><paramref name="firingWhileEngaged"/> -- the shooter is themselves engaged in melee:
    /// "firing a missile weapon while engaged in combat is Difficult" -- a
    /// <see cref="DifficultyModifier"/>.</item>
    /// </list>
    /// The book's point-blank cancellation -- "if the attacker and the target are both within close
    /// combat range, the attack is Easy (for Point-blank Range), so the Difficult and Easy modifiers
    /// cancel one another" -- is <em>not</em> re-emitted here: the point-blank Easy is
    /// <see cref="RangeBandResolver"/>'s contribution (<see cref="RangeBand.PointBlank"/>), and when a
    /// caller composes it alongside this rule's while-engaged Difficult, ADR 0007's non-stacking
    /// collapse cancels the pair automatically. Producing the Easy here as well would double-count it.
    /// The stray-ally risk on a roll between the skill rating and the -20% chance is the
    /// <see cref="SpotRuleDecisionId.FiringIntoCombatStrayTarget"/> port's concern (and such a shot
    /// earns no experience check), not a modifier.
    /// </summary>
    /// <param name="firingIntoMelee">Whether the shot passes into a melee others are engaged in.</param>
    /// <param name="firingWhileEngaged">Whether the shooter is themselves engaged in melee.</param>
    /// <param name="ruleset">The data-defined -20% firing-into-combat penalty.</param>
    /// <param name="source">A label prefix used in the rendered chain.</param>
    public static IReadOnlyList<Modifier> FiringIntoCombat(
        bool firingIntoMelee, bool firingWhileEngaged, SpotRuleRuleset ruleset, string source = "firing into combat")
    {
        ArgumentNullException.ThrowIfNull(ruleset);
        ArgumentNullException.ThrowIfNull(source);

        var modifiers = new List<Modifier>();
        if (firingIntoMelee)
        {
            modifiers.Add(new AdditiveModifier(
                $"{source}: into a melee (Ch 7, Firing Into Combat)",
                ruleset.FiringIntoCombatModifier,
                AdditiveKind.Situational));
        }

        if (firingWhileEngaged)
        {
            modifiers.Add(DifficultyModifier.Difficult($"{source}: while engaged (Ch 7, Firing Into Combat)"));
        }

        return modifiers;
    }

    /// <summary>
    /// Composes a roll's spot-rule contributions with its other pending modifiers through
    /// <see cref="ModifierPipeline"/>, so ADR 0007's stage order (situational additives applied
    /// after the difficulty grade) and difficulty non-stacking (Easy/Difficult collapse and cancel)
    /// govern the result. A thin convenience over <see cref="ModifierPipeline.Evaluate"/> that makes
    /// the "spot rules are ordinary pipeline modifiers" contract explicit; callers may equally
    /// concatenate the lists and call the pipeline directly.
    /// </summary>
    /// <param name="baseChance">The character's current rating the modifiers apply to.</param>
    /// <param name="spotRuleContributions">The modifiers produced by this resolver's rule methods.</param>
    /// <param name="otherModifiers">The roll's other modifiers (range band, and so on).</param>
    /// <param name="policy">The stage order and difficulty multipliers, or null for the default.</param>
    public static ModifierChain Evaluate(
        Percent baseChance,
        IEnumerable<Modifier> spotRuleContributions,
        IEnumerable<Modifier> otherModifiers,
        ModifierPolicy? policy = null)
    {
        ArgumentNullException.ThrowIfNull(spotRuleContributions);
        ArgumentNullException.ThrowIfNull(otherModifiers);

        return ModifierPipeline.Evaluate(baseChance, spotRuleContributions.Concat(otherModifiers), policy);
    }

    private static ArgumentOutOfRangeException UnknownKind<TKind>(TKind kind) =>
        new(nameof(kind), kind, $"Unknown {typeof(TKind).Name}.");

    private static ArgumentOutOfRangeException UnknownRole(SpotRuleRole role) =>
        new(nameof(role), role, "Unknown spot-rule role.");
}
