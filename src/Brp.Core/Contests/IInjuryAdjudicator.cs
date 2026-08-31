using Brp.Core.Abilities;

namespace Brp.Core.Contests;

/// <summary>
/// The named gamemaster-adjudication points the Ch 7 injury/effect spot rules (Falling, Poison,
/// Disease) leave open. Like <see cref="SpotRuleDecisionId"/> for the situational-modifier spot
/// rules (#50, ADR 0018), each member is a decision the source book explicitly hands to the
/// gamemaster ("at the gamemaster's discretion", "unless otherwise specified by the gamemaster",
/// "the type of disease dictates") rather than resolving mechanically. Naming them as first-class
/// ids keeps the injury resolvers from silently hardcoding these calls. The canonical kebab-case
/// id for each is given in its summary and returned by <see cref="InjuryDecisionIds.CanonicalId"/>.
/// See <c>docs/decisions/0019-injury-spot-rules.md</c>.
/// </summary>
public enum InjuryDecisionId
{
    /// <summary>
    /// Canonical id <c>falling-surface</c>. Ch 7, "Falling" (p.172): "The gamemaster may adjust the
    /// damage based on the surface impacted, or any intervening minor obstacles like branches."
    /// How much a soft (or unusually hard) landing surface, or obstacles broken through on the way
    /// down, adjust the rolled falling damage is a gamemaster call. A post-roll ruling: it adjusts
    /// the damage the Falling rule has already rolled.
    /// </summary>
    FallingSurface,

    /// <summary>
    /// Canonical id <c>poison-onset</c>. Ch 7, "Poison" (p.176): "Poison damage does not usually
    /// occur on the same combat round in which the character is poisoned... Unless otherwise
    /// specified by the gamemaster, the delay is three combat rounds for fast-acting poisons, or
    /// three full turns for slower poisons." Which onset category a given poison falls in -- and
    /// whether the gamemaster overrides the printed default with a bespoke delay -- is a gamemaster
    /// call. A pre-effect ruling: it decides when the already-resolved poison damage lands.
    /// </summary>
    PoisonOnset,

    /// <summary>
    /// Canonical id <c>antidote-cross-type</c>. Ch 7, "Poison Antidotes" (p.176): "An antidote for
    /// one type of poison may give a lessened benefit even when used with a different poison type,
    /// at the gamemaster's discretion." How much of a mismatched antidote's POT still subtracts from
    /// the poison POT is a gamemaster call. A pre-effect ruling: it feeds the effective antidote POT
    /// into the poison's damage figure.
    /// </summary>
    AntidoteCrossType,

    /// <summary>
    /// Canonical id <c>disease-affected-characteristic</c>. Ch 7, "Disease" (p.170): "The type of
    /// disease dictates what characteristic points are being lost... A major disease such as plague
    /// might attack any characteristic, but most diseases attack CON or hit points." Which
    /// characteristic a given disease drains -- and, "at the gamemaster's discretion, some diseases
    /// may combine the effects" -- is a gamemaster/setting call. A pre-drain ruling: it selects the
    /// characteristic the Illness Severity Table's loss is applied to via
    /// <see cref="AbilitySet.Set"/> so derived values recompute.
    /// </summary>
    DiseaseAffectedCharacteristic,
}

/// <summary>Canonical kebab-case ids for the <see cref="InjuryDecisionId"/> ports.</summary>
public static class InjuryDecisionIds
{
    /// <summary>
    /// The canonical kebab-case id for <paramref name="decisionId"/> -- the stable string a GM
    /// tool, authored policy, or log keys on (e.g. <c>falling-surface</c>), matching the ids named
    /// in Issue #96 and ADR 0019.
    /// </summary>
    public static string CanonicalId(InjuryDecisionId decisionId) => decisionId switch
    {
        InjuryDecisionId.FallingSurface => "falling-surface",
        InjuryDecisionId.PoisonOnset => "poison-onset",
        InjuryDecisionId.AntidoteCrossType => "antidote-cross-type",
        InjuryDecisionId.DiseaseAffectedCharacteristic => "disease-affected-characteristic",
        _ => throw new ArgumentOutOfRangeException(nameof(decisionId), decisionId, "Unknown injury decision id."),
    };
}

/// <summary>
/// Which onset category a poison falls in, for the <see cref="InjuryDecisionId.PoisonOnset"/>
/// ruling (Ch 7, p.176). The two categories select different default delays and different time
/// units (combat rounds vs. full turns).
/// </summary>
public enum PoisonOnsetSpeed
{
    /// <summary>A fast-acting poison: the printed default delay is three combat rounds (p.176).</summary>
    FastActing,

    /// <summary>A slower poison: the printed default delay is three full turns (p.176).</summary>
    SlowActing,
}

/// <summary>
/// The gamemaster's ruling on a poison's onset, for the <see cref="InjuryDecisionId.PoisonOnset"/>
/// port (Ch 7, p.176).
/// </summary>
/// <param name="Speed">Which onset category the poison falls in.</param>
/// <param name="GamemasterSpecifiedDelay">
/// A bespoke delay the gamemaster specifies in place of the printed default ("unless otherwise
/// specified by the gamemaster"), in the units implied by <paramref name="Speed"/>, or
/// <see langword="null"/> to use the printed default for that speed.
/// </param>
public readonly record struct PoisonOnsetRuling(PoisonOnsetSpeed Speed, int? GamemasterSpecifiedDelay);

/// <summary>
/// The gamemaster's adjustment to rolled falling damage, for the
/// <see cref="InjuryDecisionId.FallingSurface"/> port (Ch 7, p.172).
/// </summary>
/// <param name="DamageAdjustment">
/// A signed adjustment applied to the rolled (and armor-mitigated) falling damage: negative for a
/// soft surface or obstacles broken through, positive for an unusually unforgiving landing. The
/// final damage is floored at zero regardless. Defaults to zero -- no adjustment.
/// </param>
public readonly record struct FallingSurfaceRuling(int DamageAdjustment);

/// <summary>
/// A gamemaster-discretion port for the Ch 7 injury/effect spot rules (Falling, Poison, Disease),
/// modeled -- like <see cref="ISpotRuleAdjudicator"/> for the situational-modifier spot rules --
/// as a first-class interface rather than a set of silent hardcoded choices. Each method answers
/// one <see cref="InjuryDecisionId"/> the book leaves open. A GM tool can prompt a human; an
/// unattended simulation can supply an authored policy; tests supply a deterministic stub. The
/// return types are ordinary <c>Brp.Core</c> values so this port stays within <c>Brp.Core</c> and
/// does not invert the layer dependency (AGENTS.md invariant 6). See
/// <c>docs/decisions/0019-injury-spot-rules.md</c>.
/// </summary>
public interface IInjuryAdjudicator
{
    /// <summary>
    /// Decides how the landing surface or intervening obstacles adjust rolled falling damage
    /// (<see cref="InjuryDecisionId.FallingSurface"/>). Post-roll.
    /// </summary>
    FallingSurfaceRuling DecideFallingSurface();

    /// <summary>
    /// Decides a poison's onset category and any bespoke delay
    /// (<see cref="InjuryDecisionId.PoisonOnset"/>). Pre-effect.
    /// </summary>
    PoisonOnsetRuling DecidePoisonOnset();

    /// <summary>
    /// Decides how much of a cross-type antidote's POT still applies against a poison
    /// (<see cref="InjuryDecisionId.AntidoteCrossType"/>). Pre-effect.
    /// </summary>
    /// <param name="crossTypeAntidotePotency">
    /// The mismatched antidote's full POT. The returned value is the "lessened benefit" POT that
    /// subtracts from the poison POT, in <c>[0, crossTypeAntidotePotency]</c>.
    /// </param>
    int DecideAntidoteCrossTypePotency(int crossTypeAntidotePotency);

    /// <summary>
    /// Decides which characteristic a disease drains
    /// (<see cref="InjuryDecisionId.DiseaseAffectedCharacteristic"/>). Pre-drain.
    /// </summary>
    CharacteristicId DecideDiseaseAffectedCharacteristic();
}

/// <summary>
/// The documented default policy for every <see cref="InjuryDecisionId"/>: the most
/// minimal-assumption answer to "the book does not say," mirroring
/// <see cref="DefaultSpotRuleAdjudicator"/>. A table with a house rule or a human gamemaster should
/// supply its own <see cref="IInjuryAdjudicator"/> instead.
/// </summary>
public sealed class DefaultInjuryAdjudicator : IInjuryAdjudicator
{
    /// <summary>
    /// Defaults to no adjustment (<c>0</c>) -- the printed falling damage stands, adding no fact
    /// about the surface the book did not state.
    /// </summary>
    public FallingSurfaceRuling DecideFallingSurface() => new(DamageAdjustment: 0);

    /// <summary>
    /// Defaults to <see cref="PoisonOnsetSpeed.FastActing"/> with no bespoke delay -- the
    /// combat-round default the book lists first, the reading most relevant to a fight.
    /// </summary>
    public PoisonOnsetRuling DecidePoisonOnset() => new(PoisonOnsetSpeed.FastActing, GamemasterSpecifiedDelay: null);

    /// <summary>
    /// Defaults to no benefit (<c>0</c>) from a cross-type antidote -- the book says the gamemaster
    /// <em>may</em> allow a lessened benefit, so the neutral default grants none.
    /// </summary>
    public int DecideAntidoteCrossTypePotency(int crossTypeAntidotePotency) => 0;

    /// <summary>
    /// Defaults to <c>CON</c> -- the book states "most diseases attack CON or hit points," so CON
    /// is the least-assuming default characteristic to drain.
    /// </summary>
    public CharacteristicId DecideDiseaseAffectedCharacteristic() => new("CON");
}
