namespace Brp.Rules.Combat;

/// <summary>
/// The result of routing a single blow's damage to a struck <see cref="HitLocation"/> and the
/// character's total hit points, from <see cref="HitLocationDamageResolver.ApplyDamage"/>.
/// </summary>
/// <param name="Location">The struck location.</param>
/// <param name="RawDamage">
/// The full, uncapped damage the blow dealt (after armor), before any limb cap. Drives
/// <see cref="Band"/>.
/// </param>
/// <param name="AppliedDamage">
/// The damage actually subtracted from both the location and the total (Ch 6, p.157: for a capped
/// limb hit, this is at most <see cref="RawDamage"/> and never more than
/// <see cref="HitLocationRuleset.LimbDamageCapMultiplier"/> times the location's hit points).
/// </param>
/// <param name="CapApplied">
/// Whether the limb cap actually reduced the damage applied (<see cref="RawDamage"/> exceeded the
/// cap). Always <see langword="false"/> for a non-limb location or when the falling exception
/// bypassed the cap.
/// </param>
/// <param name="LocationRemainingHitPoints">The location's remaining hit points after this blow.</param>
/// <param name="TotalRemainingHitPoints">The character's remaining total hit points after this blow.</param>
/// <param name="Band">Which printed damage-vs-location-hit-points threshold this blow reached.</param>
public sealed record HitLocationDamageResult(
    HitLocation Location,
    int RawDamage,
    int AppliedDamage,
    bool CapApplied,
    int LocationRemainingHitPoints,
    int TotalRemainingHitPoints,
    HitLocationDamageBand Band);
