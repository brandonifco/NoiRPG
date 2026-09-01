namespace Brp.Rules.Combat;

/// <summary>The result of rolling a D20 against the <see cref="HitLocationTable"/>.</summary>
/// <param name="Roll">The raw D20 result.</param>
/// <param name="Location">The hit location the roll mapped to.</param>
/// <param name="Description">The printed description of the location's extent.</param>
public sealed record HitLocationRoll(int Roll, HitLocation Location, string Description);
