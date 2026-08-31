namespace Brp.Core.Contests;

/// <summary>
/// The named gamemaster-adjudication points the Ch 6 combat fumble tables leave open. Like
/// <see cref="SpotRuleDecisionId"/> (#50, ADR 0018) and <see cref="InjuryDecisionId"/> (#96,
/// ADR 0019), each member is a decision the source book explicitly hands to the situation rather
/// than resolving mechanically. The four D100 fumble tables (Ch 6, pp.148-149) each carry rows of
/// the form "hit nearest ally ... <em>or use result NN-NN if no ally nearby</em>"; whether a
/// friendly target is actually in range is a fact about the encounter this rules layer does not
/// model, so it is named as a first-class id rather than silently assumed. The canonical
/// kebab-case id is given in the summary and returned by <see cref="FumbleDecisionIds.CanonicalId"/>.
/// See <c>docs/decisions/0020-fumble-tables.md</c>.
/// </summary>
public enum FumbleDecisionId
{
    /// <summary>
    /// Canonical id <c>fumble-ally-in-range</c>. Ch 6, the fumble tables (pp.148-149): the
    /// "hit nearest ally for normal/special/critical damage, or use result NN-NN if no ally nearby"
    /// rows branch on whether a friendly target is within reach of the fumbled blow. Whether an ally
    /// is nearby is an encounter fact (position, who is adjacent) this combat-rules layer does not
    /// hold, so it is a caller/gamemaster call. A pre-effect ruling: it selects which branch of the
    /// row applies before any damage is figured (damage itself is never applied here — no encounter
    /// model in this layer).
    /// </summary>
    AllyInRange,
}

/// <summary>Canonical kebab-case ids for the <see cref="FumbleDecisionId"/> ports.</summary>
public static class FumbleDecisionIds
{
    /// <summary>
    /// The canonical kebab-case id for <paramref name="decisionId"/> -- the stable string a GM
    /// tool, authored policy, or log keys on (e.g. <c>fumble-ally-in-range</c>), matching the id
    /// named in Issue #97 and ADR 0020.
    /// </summary>
    public static string CanonicalId(FumbleDecisionId decisionId) => decisionId switch
    {
        FumbleDecisionId.AllyInRange => "fumble-ally-in-range",
        _ => throw new ArgumentOutOfRangeException(nameof(decisionId), decisionId, "Unknown fumble decision id."),
    };
}

/// <summary>
/// A gamemaster-discretion port for the Ch 6 combat fumble tables (pp.148-149), modeled -- like
/// <see cref="ISpotRuleAdjudicator"/> and <see cref="IInjuryAdjudicator"/> -- as a first-class
/// interface rather than a set of silent hardcoded choices. Its single method answers the one
/// <see cref="FumbleDecisionId"/> the tables leave open. A GM tool can prompt a human; an unattended
/// simulation can supply an authored policy; tests supply a deterministic stub. The return type is a
/// plain <c>bool</c>, so this port stays within <c>Brp.Core</c> and does not invert the layer
/// dependency (AGENTS.md invariant 6). See <c>docs/decisions/0020-fumble-tables.md</c>.
/// </summary>
public interface IFumbleAdjudicator
{
    /// <summary>
    /// Decides whether a friendly target is within reach of a fumbled blow, for the
    /// <see cref="FumbleDecisionId.AllyInRange"/> rows. Pre-effect: <see langword="true"/> selects
    /// the "hit nearest ally" branch, <see langword="false"/> the printed "no ally nearby" fallback.
    /// </summary>
    bool IsAllyInRange();
}

/// <summary>
/// The documented default policy for <see cref="FumbleDecisionId.AllyInRange"/>: the most
/// minimal-assumption answer to "the book does not say," mirroring <see cref="DefaultInjuryAdjudicator"/>
/// and <see cref="DefaultSpotRuleAdjudicator"/>. A table with a house rule or a human gamemaster
/// should supply its own <see cref="IFumbleAdjudicator"/> instead.
/// </summary>
public sealed class DefaultFumbleAdjudicator : IFumbleAdjudicator
{
    /// <summary>
    /// Defaults to <see langword="false"/> -- no ally in range. The safest neutral reading for an
    /// unattended run: it asserts no friendly bystander the caller has not placed on the field, so a
    /// fumble that could hit an ally falls back to the printed self-affecting result instead of
    /// inventing collateral damage.
    /// </summary>
    public bool IsAllyInRange() => false;
}
