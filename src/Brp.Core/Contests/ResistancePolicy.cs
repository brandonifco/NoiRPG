namespace Brp.Core.Contests;

/// <summary>
/// The constants that define the Resistance Table, Ch 5: System, "Resistance Rolls" (BRP ORC
/// Content Document, p.129): "The base chance of a resistance roll equals 50% + (active
/// characteristic x 5) - (passive characteristic x 5)... Differences of 10 points or more
/// result in automatic success or failure."
/// <para>
/// These live in one named place rather than as literals in <see cref="ResistanceResolver"/>,
/// mirroring <c>Resolution.ResolutionPolicy</c> -- the same rationale applies: these describe
/// the resolution system itself, not a setting, so they are not ruleset JSON.
/// </para>
/// <para>
/// Treat <see cref="Standard"/> as the only supported configuration, exactly as
/// <c>ResolutionPolicy.Standard</c> is documented.
/// </para>
/// </summary>
/// <param name="ParityChance">
/// The chance when the active and passive factors are equal (Ch 5, "Resistance Rolls", p.129:
/// "If the active and passive factors are equal, the active factor has a 50% chance of
/// success.").
/// </param>
/// <param name="PercentPerPointOfDifference">
/// The chance shifts by this many percentage points for every point the active factor differs
/// from the passive factor -- up, in the active party's favor, or down, in the passive party's
/// (Ch 5, "Resistance Rolls", p.129).
/// </param>
/// <param name="AutomaticFailureBelow">
/// A computed chance strictly below this value is automatic failure. Taken from the table's own
/// caption (p.130: "Changes below 05% are in the range of automatic failure"), which is exactly
/// the linear formula's value at the last printed row (a 9-point disadvantage yields this value
/// itself, still printed and rollable, in 15 of the table's cells; a 10-point disadvantage yields
/// one step further down and is the first automatic cell).
/// </param>
/// <param name="AutomaticSuccessAbove">
/// A computed chance strictly above this value is automatic success (same caption, p.130: "and
/// over 95% in the range of automatic success").
/// </param>
/// <param name="MaximumRoll">
/// The highest percentile result. A printed roll of 00 is read as this value -- see
/// <see cref="Randomness.IEntropySource.NextD100"/>.
/// </param>
public sealed record ResistancePolicy(
    int ParityChance,
    int PercentPerPointOfDifference,
    int AutomaticFailureBelow,
    int AutomaticSuccessAbove,
    int MaximumRoll)
{
    /// <summary>The values printed in the source book. The only supported configuration.</summary>
    public static ResistancePolicy Standard { get; } = new(
        ParityChance: 50,
        PercentPerPointOfDifference: 5,
        AutomaticFailureBelow: 5,
        AutomaticSuccessAbove: 95,
        MaximumRoll: 100);
}
