namespace Brp.Rules.Combat;

/// <summary>
/// One row of Ch 7: Spot Rules, "Illness Severity Table" (p.170), banded by the number of failed
/// CON recovery rolls. Mirrors <see cref="Core.Abilities.DamageModifierBand"/>: a lower bound, an
/// optional upper bound (open-ended for the "4+" Terminal row), and the printed effect.
/// </summary>
/// <param name="MinimumFailures">The lowest failure count this row covers.</param>
/// <param name="MaximumFailures">
/// The highest failure count this row covers, or <see langword="null"/> for the open-ended "4+"
/// Terminal row.
/// </param>
/// <param name="Degree">The printed degree of illness for this row.</param>
/// <param name="LossPeriod">The printed loss period for this row.</param>
public sealed record IllnessSeverityBand(
    int MinimumFailures,
    int? MaximumFailures,
    IllnessDegree Degree,
    IllnessLossPeriod LossPeriod)
{
    /// <summary>Whether this row covers the given number of failed CON recovery rolls.</summary>
    public bool Contains(int failures) =>
        failures >= MinimumFailures && (MaximumFailures is null || failures <= MaximumFailures.Value);
}
