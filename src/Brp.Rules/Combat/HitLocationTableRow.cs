namespace Brp.Rules.Combat;

/// <summary>
/// One row of Ch 6: Combat, "Hit Locations" (p.145), banded by the D20 result. Mirrors
/// <see cref="MajorWoundRow"/> / <see cref="IllnessSeverityBand"/>.
/// </summary>
/// <param name="Minimum">The lowest D20 result this row covers.</param>
/// <param name="Maximum">The highest D20 result this row covers.</param>
/// <param name="Location">The hit location this row maps to.</param>
/// <param name="Description">The printed description of the location's extent (e.g. "Right leg from hip to bottom of foot").</param>
public sealed record HitLocationTableRow(int Minimum, int Maximum, HitLocation Location, string Description)
{
    /// <summary>Whether this row covers the given D20 result.</summary>
    public bool Contains(int roll) => roll >= Minimum && roll <= Maximum;
}
