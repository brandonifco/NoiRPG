using Brp.Core.Skills;

namespace Brp.Rules.Gear;

/// <summary>
/// A car's identity and the stats a Drive roll, a vehicle chase, or a collision needs. Sourced:
/// Ch 8: Equipment, "Vehicles" (p.217-219), the "Autos, Trucks, Trains &amp; Tanks" table (p.219)
/// and each car's own entry under "Vehicle Descriptions" (p.218). Hand-picked to the three
/// automobile rows the book itself prints, per `orc-scope-filter.md`, Ch 8: "Vehicles: cars
/// only" -- motorcycles, trucks, trains, tanks, and every aircraft/watercraft/spacecraft row in
/// the same tables are out of scope for this issue.
/// </summary>
/// <param name="Id">The stable ruleset identifier.</param>
/// <param name="Name">The display name, as printed in the book.</param>
/// <param name="SkillId">
/// The skill used to drive this car -- always <c>Drive</c> (Ch 3, p.37) for the in-scope
/// automobile entries; kept as a <see cref="SkillId"/> rather than hardcoded so the definition
/// still validates against the Layer 2 skill list the way <see cref="WeaponDefinition.SkillId"/> does.
/// </param>
/// <param name="RatedSpeed">
/// The car's maximum sustainable speed, an abstract value used by the chase system ("Rated
/// Speed", p.217).
/// </param>
/// <param name="Handling">
/// The modifier applied to the driver's Drive roll, reflecting the car's maneuverability
/// ("Handling", p.217). A positive value is a bonus, a negative value a penalty; <c>0</c> when
/// the book prints "&#8212;".
/// </param>
/// <param name="Acceleration">
/// The number of Rated Speed increments the car can accelerate or decelerate by each combat
/// round ("ACC", p.217). The book prints this as a "&#177;" value; both directions share this
/// one magnitude.
/// </param>
/// <param name="MetersPerRound">The car's maximum speed within a single combat round ("MOV", p.217).</param>
/// <param name="Armor">
/// The car's armor value, protecting crew and passengers from attacks that reach the cabin
/// ("Armor", p.217). Reuses <see cref="Gear.ArmorValue"/> because the book prints this the same
/// way as the Modern Armor table -- a melee/low-velocity figure and a firearms figure, slash-
/// separated when they differ.
/// </param>
/// <param name="Siz">The car's apparent SIZ value ("SIZ", p.217).</param>
/// <param name="HitPoints">The car's hit points ("HP", p.217); see the Vehicle Damage rule (p.219) for how these are spent.</param>
/// <param name="Crew">The number of characters required to drive the car at full efficiency ("Crew", p.217).</param>
/// <param name="Passengers">
/// The number of passengers the car normally carries ("Passengers", p.217). Kept as the book's
/// own text (e.g. <c>"3-4"</c>) rather than a single int, since the printed column is a range for
/// two of the three cars.
/// </param>
/// <param name="Cargo">The car's cargo capacity, expressed in SIZ ("Cargo", p.217).</param>
/// <param name="ValueTier">
/// The book's qualitative price tier for the car (e.g. <c>"Average"</c>, <c>"Expensive"</c>),
/// as printed in the "Value" column (p.217, "Money and Purchasing Equipment"). Kept as the
/// book's own label rather than mapped onto a numeric price or a wealth-level enum -- the
/// `Money and Wealth levels` abstraction the scope filter also keeps in-scope for Ch 8 is a
/// separate, not-yet-built issue.
/// </param>
/// <param name="Source">The book table (and entry) this definition was transcribed from.</param>
public sealed record VehicleDefinition(
    VehicleId Id,
    string Name,
    SkillId SkillId,
    int RatedSpeed,
    int Handling,
    int Acceleration,
    int MetersPerRound,
    ArmorValue Armor,
    int Siz,
    int HitPoints,
    int Crew,
    string Passengers,
    int Cargo,
    string ValueTier,
    string Source);
