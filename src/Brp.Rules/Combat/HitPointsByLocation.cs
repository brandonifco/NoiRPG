namespace Brp.Rules.Combat;

/// <summary>
/// A character's maximum hit points at each <see cref="HitLocation"/> (Ch 6: Combat, "Hit Points by
/// Hit Location (Option)", p.14), computed once from a total hit-point figure by
/// <see cref="HitPointsByLocationCalculator"/>. Ch 6, p.14: "The sum of your character's hit points
/// by location exceeds their maximum hit points" -- this is expected, not an error; each location is
/// tracked separately from, and in addition to, the running total (<see cref="HitLocationHitPoints"/>).
/// </summary>
/// <param name="RightLeg">Maximum hit points at the right leg.</param>
/// <param name="LeftLeg">Maximum hit points at the left leg.</param>
/// <param name="Abdomen">Maximum hit points at the abdomen.</param>
/// <param name="Chest">Maximum hit points at the chest.</param>
/// <param name="RightArm">Maximum hit points at the right arm.</param>
/// <param name="LeftArm">Maximum hit points at the left arm.</param>
/// <param name="Head">Maximum hit points at the head.</param>
public sealed record HitPointsByLocation(
    int RightLeg, int LeftLeg, int Abdomen, int Chest, int RightArm, int LeftArm, int Head)
{
    /// <summary>Looks up the maximum hit points for a given location.</summary>
    public int At(HitLocation location) => location switch
    {
        HitLocation.RightLeg => RightLeg,
        HitLocation.LeftLeg => LeftLeg,
        HitLocation.Abdomen => Abdomen,
        HitLocation.Chest => Chest,
        HitLocation.RightArm => RightArm,
        HitLocation.LeftArm => LeftArm,
        HitLocation.Head => Head,
        _ => throw new ArgumentOutOfRangeException(nameof(location), location, "Unknown hit location."),
    };
}
