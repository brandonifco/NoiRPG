using Brp.Core.Abilities;

namespace Brp.Core.Contests;

/// <summary>
/// The named gamemaster-adjudication points the Ch 6: Combat, "Major Wounds" (pp.155-156) rule
/// leaves open. The Major Wounds sibling of <see cref="IInjuryAdjudicator"/> (the Ch 7 injury spot
/// rules, #96) and <see cref="ISpotRuleAdjudicator"/> (the situational-modifier spot rules, #50):
/// each member is a call the Major Wounds Table hands to the gamemaster -- which side a limb wound
/// falls on when the roll "does not specify," and which characteristics the 00 row's "gamemaster's
/// discretion" removes -- rather than resolving mechanically. Naming them as first-class ids keeps
/// <c>MajorWoundResolver</c> from silently hardcoding these calls. The canonical kebab-case id for
/// each is given in its summary and returned by <see cref="MajorWoundDecisionIds.CanonicalId"/>.
/// See <c>docs/decisions/0021-major-wounds.md</c>.
/// </summary>
public enum MajorWoundDecisionId
{
    /// <summary>
    /// Canonical id <c>major-wound-limb-side</c>. Ch 6, "Major Wounds" (p.155): "When a limb is not
    /// specified, roll 1D6: a result of 1-3 is left, 4-6 is right." Which side an unspecified limb
    /// wound (the "left or right arm" of the 95-96 row) falls on is a gamemaster call. Purely
    /// narrative: the characteristic loss is identical whichever side is chosen, so this decides only
    /// flavor, not mechanics. The book's suggested resolution method is a 1D6 (1-3 left, 4-6 right) --
    /// an adjudicator wanting that randomization rolls it; the neutral default does not.
    /// </summary>
    LimbSide,

    /// <summary>
    /// Canonical id <c>major-wound-characteristics</c>. Ch 6, Major Wounds Table 00 row (p.156):
    /// "Remove 1D4 points each from four characteristics (gamemaster's discretion)." Which four
    /// characteristics the worst result strikes is a gamemaster call; the dice (1D4 each) and the
    /// count (four) are fixed by the table. A pre-drain ruling: it selects the characteristics the
    /// row's loss is applied to via <see cref="AbilitySet.Set"/> so derived values recompute.
    /// </summary>
    Characteristics,
}

/// <summary>Canonical kebab-case ids for the <see cref="MajorWoundDecisionId"/> ports.</summary>
public static class MajorWoundDecisionIds
{
    /// <summary>
    /// The canonical kebab-case id for <paramref name="decisionId"/> -- the stable string a GM tool,
    /// authored policy, or log keys on (e.g. <c>major-wound-limb-side</c>), matching the ids named in
    /// Issue #111 and ADR 0021.
    /// </summary>
    public static string CanonicalId(MajorWoundDecisionId decisionId) => decisionId switch
    {
        MajorWoundDecisionId.LimbSide => "major-wound-limb-side",
        MajorWoundDecisionId.Characteristics => "major-wound-characteristics",
        _ => throw new ArgumentOutOfRangeException(nameof(decisionId), decisionId, "Unknown major wound decision id."),
    };
}

/// <summary>
/// Which side an unspecified limb wound falls on, for the <see cref="MajorWoundDecisionId.LimbSide"/>
/// ruling (Ch 6, p.155). Narrative only -- the characteristic loss does not depend on the side.
/// </summary>
public enum BodySide
{
    /// <summary>The left side -- the book's 1D6 result of 1-3 (p.155).</summary>
    Left,

    /// <summary>The right side -- the book's 1D6 result of 4-6 (p.155).</summary>
    Right,
}

/// <summary>
/// A gamemaster-discretion port for Ch 6: Combat, "Major Wounds" (pp.155-156), modeled -- like
/// <see cref="IInjuryAdjudicator"/> and <see cref="ISpotRuleAdjudicator"/> -- as a first-class
/// interface rather than a set of silent hardcoded choices. Each method answers one
/// <see cref="MajorWoundDecisionId"/> the table leaves open. A GM tool can prompt a human; an
/// unattended simulation can supply an authored policy; tests supply a deterministic stub. The
/// return types are ordinary <c>Brp.Core</c> values so this port stays within <c>Brp.Core</c> and
/// does not invert the layer dependency (AGENTS.md invariant 6). See
/// <c>docs/decisions/0021-major-wounds.md</c>.
/// </summary>
public interface IMajorWoundAdjudicator
{
    /// <summary>
    /// Decides which side an unspecified limb wound falls on
    /// (<see cref="MajorWoundDecisionId.LimbSide"/>). Narrative only.
    /// </summary>
    BodySide DecideLimbSide();

    /// <summary>
    /// Decides which characteristics the 00 row's "gamemaster's discretion" loss strikes
    /// (<see cref="MajorWoundDecisionId.Characteristics"/>). Pre-drain.
    /// </summary>
    /// <param name="count">
    /// How many distinct characteristics the row removes points from (four, per the 00 row). The
    /// returned list must contain exactly this many distinct characteristics.
    /// </param>
    IReadOnlyList<CharacteristicId> DecideCharacteristics(int count);
}

/// <summary>
/// The documented default policy for every <see cref="MajorWoundDecisionId"/>: the most
/// minimal-assumption answer to a call the table leaves open, mirroring
/// <see cref="DefaultInjuryAdjudicator"/>. A table with a house rule or a human gamemaster should
/// supply its own <see cref="IMajorWoundAdjudicator"/> instead.
/// </summary>
public sealed class DefaultMajorWoundAdjudicator : IMajorWoundAdjudicator
{
    // The 00 row strikes four distinct characteristics; the neutral default draws from the physical
    // and mental characteristics a living being always has, in a fixed order, so a defaulted result
    // is deterministic and never repeats a characteristic. A table wanting a different or random
    // selection supplies its own adjudicator.
    private static readonly IReadOnlyList<CharacteristicId> DefaultCharacteristicOrder =
    [
        new("STR"), new("CON"), new("DEX"), new("INT"), new("POW"), new("CHA"), new("SIZ"),
    ];

    /// <summary>
    /// Defaults to <see cref="BodySide.Left"/> -- the low half of the book's 1D6 (1-3), a neutral
    /// fixed answer that adds no randomness the caller did not ask for.
    /// </summary>
    public BodySide DecideLimbSide() => BodySide.Left;

    /// <summary>
    /// Defaults to the first <paramref name="count"/> characteristics of a fixed order (STR, CON,
    /// DEX, INT, POW, CHA, SIZ) -- distinct and deterministic, adding no setting-specific choice the
    /// book left to the gamemaster.
    /// </summary>
    public IReadOnlyList<CharacteristicId> DecideCharacteristics(int count)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(count);
        if (count > DefaultCharacteristicOrder.Count)
        {
            throw new ArgumentOutOfRangeException(
                nameof(count), count, "The default adjudicator knows fewer distinct characteristics than requested.");
        }

        return DefaultCharacteristicOrder.Take(count).ToList();
    }
}
