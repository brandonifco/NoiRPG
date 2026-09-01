# NNNN. Vehicles — cars only: the hand-picked automobile subset and its armor semantics

## Status

Accepted — 2026-09-01. Resolves #232 (sub-issue of #116).

## Context

`orc-scope-filter.md`, Ch 8: Equipment (line 136): **"Vehicles: cars
only."** The chapter's Vehicles section (p.217-220) prints five vehicle
tables — Horse & Horse-Drawn Vehicles (p.219), Autos, Trucks, Trains &
Tanks (p.220), Boats & Ships (p.220), Air Vehicles (p.220), and Space
Vehicles (p.220) — and every row outside the automobile entries in the
second table is out of scope: aircraft, watercraft, and spacecraft are
explicitly cut by the scope filter, and so are horse-drawn vehicles,
motorcycles, trucks, trains, and tanks, per the issue's own "cars only"
framing (not "land vehicles only").

The `Drive` skill (Ch 3, p.37) already exists in `skill-ruleset.json`
(#11/Layer 2) and needed no changes. This issue's job was the vehicle
*stats* a Drive roll, a chase, or a collision acts on.

## Decision: three cars kept, everything else cut

The book's "Autos, Trucks, Trains & Tanks" table (p.220) prints eight
rows; only three are automobiles, matching "Vehicle Descriptions" (p.218)
exactly: Automobile, Vintage ("An old boxy automobile, equivalent to the
Model-T"); Automobile, Modern Sedan ("An average four-door modern
automobile"); Automobile, Modern Sportscar ("An extremely fast, two-door,
two-seat, high-performance automobile"). Cut, same table: Pickup Truck,
18-wheeler (trucks), Motorcycle (two-wheeled), Land Skimmer (sci-fi
hovercraft tech), Tank Vintage/Modern (military). Cut, other tables: the
entire Horse & Horse-Drawn table, Boats & Ships, Air Vehicles, and Space
Vehicles.

## Decision: schema (`Brp.Rules.Gear`)

Mirrors #42's `WeaponDefinition`/`ArmorDefinition` pattern — value-type
records in `Brp.Rules.Gear`, loaded by `Brp.Data.NoirGearRuleset.Load()`
from embedded JSON (`vehicle-ruleset.json`), added as a third collection
on the existing `GearRegistry` (`Vehicles`, `VehicleById`) rather than a
parallel registry type. `VehicleId`/`VehicleDefinition` carry the book's
Rated Speed/Handling/Acceleration/MOV chase-system columns as plain ints,
SIZ, HP, Crew, Passengers (a string — two of the three cars print a range
like `"3-4"`), Cargo, and a `ValueTier` string carrying the book's own
qualitative price label (`"Average"`, `"Expensive"`) rather than a
numeric price or a wealth-level enum — the "Money and Wealth levels"
abstraction is a separate Ch 8 scope item, not this issue's job.

**`VehicleArmor` is its own type, not a reuse of `ArmorValue`.** An
earlier draft of this data reused `Gear.ArmorValue`'s
`Melee`/`Firearms` fields for a vehicle's slash-separated armor pair.
That mislabels the number: the Vehicles section's own "Armor:" term
definition (p.217) reads *"The vehicle's general armor value and
protection it provides to crew or passengers. Usually, attacks on
passengers are through a window or open section of the cabin. If these
two numbers are different, they are expressed as two values separated by
a slash."* That is a **general-armor / occupant-protection** split, not
the Modern Armor table's (p.207) melee-vs-firearms split — two different
tables that both happen to print a slash-separated pair. Corrected to a
dedicated `VehicleArmor(GeneralArmor, OccupantProtection)` record. The
three cars' printed values are unchanged (10/1, 14/2, 10/2); only the
field names and doc-comment meaning were wrong.

**Vehicles are not wired into `GearRegistry.Resolve(EquipmentItem)`.**
That resolver answers "does this piece of *carried* gear have combat
stats" for a character's `EquipmentList`. A car is not carried equipment
in that sense (Ch 8 treats vehicle ownership as a separate concept,
p.9); `VehicleById` is the direct lookup path instead, matching how
`WeaponById`/`ArmorById` are used by anything that already holds a
stable id.

## Verification

Every kept car's stats (Rated Speed, Handling, Acceleration, MOV, Armor,
SIZ, HP, Crew, Passengers, Cargo, Value) were checked against the "Autos,
Trucks, Trains & Tanks" table (p.220) cell by cell.
`NoirGearRulesetTests.Every_vehicle_reproduces_its_printed_stats` is a
data-driven theory, one row per car. `NoirGearRulesetScopeTests` asserts
the exact id set loaded, that every other row from all five of the
book's vehicle tables is absent by name, and that every vehicle's
`SkillId` resolves against `skill-ruleset.json`.

**Page citation correction.** The table itself is on p.220, not p.219 as
an earlier draft of this data had it — p.219 is the Horse & Horse-Drawn
Vehicles table two pages earlier in the same section, verified against
`BasicRoleplaying-ORC-Content-Document.pdf`'s own page-footer markers.

## Consequences

- `GearRegistry`'s constructor is now three-arity
  (`weapons, armor, vehicles`); the one production call site
  (`NoirGearRuleset.Load()`) was updated in the same change.
- A future chase-system or collision-resolution issue (Ch 8, "Chases",
  p.220) has typed Rated Speed/Handling/Acceleration/MOV/Armor/SIZ/HP
  data to build against for the three in-scope cars.
- **Known limitation:** `ValueTier` is not yet connected to any
  purchasing or wealth-level mechanic — recorded as data now, matching
  how #42 recorded `WeaponClass` before the combat layer used it.
- **Known limitation:** the Ch 8 "Chases" mechanics (acceleration,
  collision, ramming, p.220) are not implemented here — this issue is
  data only.
