# 0013. Layer 4 keystone — weapon/armor definition schema and the hand-picked subset

## Status

Accepted — 2026-08-30. Resolves #42.

## Context

Layer 3 gave `Character` an equipment container that is deliberately
name-only (`EquipmentItem(string Name)`); its own doc comment says gear
stats "are Layer 4/8 (#21) and do not exist yet." Every downstream Layer 4
mechanic — damage resolution, the attack/defense matrix, and #21's missile
range bands specifically — needs weapon and armor data to act on, so this
issue is the gate the rest of Layer 4 is blocked behind
(`engine-implementation-plan.md` §3).

A `rules-extractor` pass had already transcribed Ch 8: Equipment's Modern
Melee Weapons, Modern Missile Weapons, and Modern Armor tables into
`weapon-ruleset.json` (19 weapons) and `armor-ruleset.json` (5 armor types).
This issue's job was twofold: trim that transcription to the hand-picked
subset `orc-scope-filter.md` calls for, and build the `Brp.Rules`
definition types plus the `Brp.Data` loader that consume it.

## Decision: the hand-picked subset

**Sourced:** `orc-scope-filter.md`, "Chapter 8: Equipment — keep the modern
slice": *"perhaps a fifth survives ... hand-pick the entries a noir
detective could plausibly encounter. A dozen firearms and three armor types
is the realistic target, not two hundred rows."*

### Weapons: 18 kept (12 firearms + 6 melee), 9 cut

| Cut | Reason |
|---|---|
| Pistol, Flintlock | Pre-modern (AGENTS.md invariant 4; `orc-scope-filter.md` cuts all pre-modern gear). |
| Rifle, Musket | Pre-modern, same rule. |
| Rifle, Assault | Trim to the "couple of rifles" target — kept Bolt-action and Sniper instead (see below). |
| Rifle, Elephant | Colonial big-game hunting rifle; not a noir-plausible encounter, and outside the "couple of rifles" budget. |
| Rifle, Sporting | Same trim — a third rifle would exceed the target. |
| Shotgun, Automatic | Trim to the two-shotgun target ("shotguns incl. sawed-off"); more tactical/military than noir-plausible. |
| Shotgun, Sporting | Same trim, redundant with Double-barreled and Sawed-off. |
| Chainsaw | The book's own Weapon Classes list (p.196) puts it under an "Improvised" skill class that has no matching entry in `skill-ruleset.json` (Ch 3's IN list has no Improvised-Weapon skill). Rather than invent a new skill specialty — out of scope for this issue and explicitly forbidden by the issue text — or silently misfile it under an unrelated skill, it is cut outright. **Flagged for rules-conformance/scope-warden**: if a future issue reopens the skill list to add an Improvised-Weapon specialty, this is the weapon that motivates it. |

Kept firearms (12, matching the "dozen" target exactly): Pistol
Derringer/Light/Medium/Heavy, Revolver Light/Medium/Heavy, Rifle
Bolt-action, Rifle Sniper, Shotgun Double-barreled, Shotgun Sawed-off, Gun
Submachine.

Kept melee (6): Brass Knuckles, Knife Butcher/Pocket/Switchblade, and two
Club entries added back in (see below) — none cut from the original
extraction, since the extractor's melee set was already tight.

**Club, Heavy and Club, Light are additions, not extractor holdovers.** The
original extraction had zero Club entries even though `skill-ruleset.json`
defines a `Melee Weapon (Club)` specialty and `orc-scope-filter.md`
explicitly lists "clubs" as IN for Ch 8. The book's own "Club" stat block
(Heavy club 1D8+dm, Light club 1D6+dm) lives in the **Primitive Melee
Weapons** table (p.196), not the Modern Melee Weapons table — but each
entry's description text (p.194) names its modern equivalent explicitly:
"Club, Heavy ... also describes a crowbar" and "Club, Light ... a baseball
bat, tire iron, lamp, chair leg, or truncheon." Given the scope filter's own
instruction to keep clubs, and the book's own text confirming these two
stat blocks describe ordinary modern objects rather than a primitive-only
weapon, they are included here, sourced to that table with the modern
equivalents noted. **Sourced**, not a house rule — the numbers are the
book's printed Club stats, verbatim.

**Submachine gun skill mapping — flagged for rules-conformance.** The book's
own Firearm skill (Ch 3, p.39) lists six specialties: Machine Gun, Pistol,
Revolver, Rifle, Shotgun, Submachine Gun. NoiRPG's Layer 2 skill list
(`skill-ruleset.json`) collapsed Pistol and Revolver into one `Firearms
(Handgun)` specialty and never added a Submachine Gun or Machine Gun
specialty — a decision made in Layer 2, out of scope to revisit here. `Gun,
Submachine` is mapped to `Firearms (Handgun)` as the closest existing
specialty (it is a one-handed or two-handed short-to-medium-range weapon
per its own description, p.194), matching the extractor's original choice.
This is a **house-ruled mapping**, not a sourced one-to-one correspondence,
and is called out both in the weapon's `source` field and here so a future
Layer 2 revision knows to revisit it if a Submachine Gun specialty is ever
added.

### Armor: 3 kept, 2 cut

| Kept | Why |
|---|---|
| Bulletproof Vest, Early | The classic 1940s-detective-plausible piece — heavy, older tech, distinct armor value from Modern. |
| Bulletproof Vest, Modern | The contemporary/neo-noir counterpart; same hit location, higher firearms AV, lower skill penalty — demonstrates the era range without adding a redundant third vest. |
| Riot Gear | The one full-body/all-locations entry, plausible for a detective caught in a police raid or riot scene; distinct niche from the two vests (which only cover Chest). |
| **Cut:** Ballistic Cloth | Softest/rarest specialty item (concealable soft armor); redundant with the vest concept for the three-type budget. |
| **Cut:** Flak Jacket | Same armor value and hit locations as Bulletproof Vest, Early in all but flavor; more military- than noir-plausible, and redundant with Riot Gear's full-body coverage. |

All five kept and cut armor values, skill penalties, and hit locations were
re-verified against the Modern Armor table (p.207) during this pass; the
extraction was accurate for all five (see "Verification" below).

## Decision: schema (`Brp.Rules.Gear`)

Mirrors the Layer 2 pattern (`SkillDefinition`/`SkillRegistry`,
`Brp.Data.NoirSkillRuleset`) exactly: value-type records in `Brp.Rules`,
loaded by a static `Brp.Data.NoirGearRuleset.Load()` from embedded JSON via
a private DTO and `PropertyNameCaseInsensitive`.

- **`WeaponDefinition`**: id, name, `SkillId` (validated against the Layer 2
  skill list by a scope test, not at load time — a load-time check would
  create a runtime dependency from `Brp.Data`'s gear loader onto the skill
  loader that the two ruleset files do not otherwise need), `WeaponClass`
  (an enum covering only the classes the hand-picked subset uses: Brawl,
  Club, Dagger, Pistol, Revolver, Rifle, Shotgun, SubmachineGun — not the
  book's full taxonomy, most of which is out of scope), a parsed
  `Brp.Core.Dice.DiceExpression` for damage (reusing Layer 0's existing dice
  parser rather than inventing a second one), `ApplyDamageBonus`, an
  optional `FirearmProfile`, and a `DamageByRange` list for the two
  shotguns whose damage falls off with distance.
- **`FirearmProfile`**: listed range (both the raw printed string and, when
  unambiguous, a parsed `int?`), malfunction number, ammo capacity, attacks
  per round, and the printed base chance (Ch 3, "Firearm (various)": "Base
  Chance: As per weapon specialty" — the value
  `Brp.Core.Skills.WeaponDerivedBaseChance` documented as missing until this
  issue). Sniper-specific bipod/scope fields are nullable and populated
  only for that one weapon, rather than forcing every firearm to carry
  fields only one of them uses.
- **`ArmorDefinition`** / **`ArmorValue`** / **`ArmorSkillPenalty`**: armor
  points (always both a melee/low-velocity and a firearms figure — `.Flat()`
  when the book prints one number for both), the skill-penalty category
  (reusing `Brp.Core.Skills.SkillCategory`) and percentage, and hit
  locations as plain strings (no `HitLocation` type exists yet; formalizing
  one is a combat-layer concern this issue was not asked to design).
- **`GearRegistry`**: keyed dictionaries by id, plus name-indexed lookups
  used by `Resolve(EquipmentItem)` (see below).

**Damage-by-range weapons store the closest-range figure as their
top-level `Damage`.** The book's own "Dmg" column for shotguns prints a
combined display string (`4D6/2D6/1D6`) that is not itself rollable
notation; `WeaponDefinition.Damage` is set to the parsed closest-range
increment, and the full breakdown lives in `DamageByRange`. This is a data
modeling choice this issue makes explicit, not a printed number in its own
right.

**The `ListedRange` field is deliberately typed loosely for shotguns.**
#21 (missile range bands) will need to decide how a shotgun's own printed
`10/20/50` damage-by-range table interacts with the general Ch 6/7
"Extended Range" point-blank/normal/medium/long multiplier rule — the two
mechanics are printed separately in the book and this issue does not
reconcile them. `ListedRange` carries the raw printed string for every
firearm (guaranteeing #21 always has *some* typed value to start from,
per the issue's acceptance criteria) and `ListedRangeMeters` is populated
only when that string is a single unambiguous number (11 of the 12
firearms); the two shotguns are the deliberate exception, flagged as an
open design question for #21 rather than resolved here.

## Decision: the `EquipmentItem` tie

`GearRegistry.Resolve(EquipmentItem item)` matches the item's free-text
`Name` case-insensitively against defined weapon and armor names and
returns a `GearLookup(Weapon, Armor)` record where both properties are
`null` for plain gear (a flashlight, lockpicks) — a normal, non-exceptional
outcome, not a thrown error. `EquipmentItem` itself is untouched: still
`record EquipmentItem(string Name)`, per the issue's explicit instruction
not to redesign the Layer 3 container. Matching is by exact display name
(e.g. `"Revolver, Light"`), not fuzzy matching against player-typed
free text like `"revolver"` — a `Character`'s equipment list is not
required to use the book's exact display names, so unmatched items simply
resolve with no definition, which is the documented, intended behavior.

## Verification

Every kept weapon and armor stat (damage, range, malfunction number, ammo
capacity, attacks per round, armor value, skill penalty, hit locations) was
re-checked against `BasicRoleplaying-ORC-Content-Document.pdf` during this
pass, not just carried over from the extractor's output. One transcription
error was caught and corrected:

- **Rifle, Sniper's bipod modifier was transcribed backwards.** The
  extractor recorded `baseChanceWithBipod: 40` and
  `baseChanceWithoutBipod: 20`. Note 4 (p.202) reads: "Sniper rifles are
  usually equipped with a bipod, doubling the chance; without a bipod (or
  similar stabilizer), reduce the base chance to 10%." The printed base
  chance in the table itself is 20% — since sniper rifles are "usually"
  equipped with a bipod, that printed 20% already assumes the bipod is
  present (i.e. it is the doubled figure, from an unmodified 10%), and the
  "reduce to 10%" clause is the without-bipod case. Corrected to
  `baseChanceWithBipod: 20` (equal to the printed base) and
  `baseChanceWithoutBipod: 10`. Covered by
  `NoirGearRulesetTests.Sniper_rifle_reproduces_its_printed_stats_and_bipod_scope_notes`.

`NoirGearRulesetTests` reproduces every kept weapon's and armor's printed
stats individually (data-driven, one row per weapon/armor, per AGENTS.md's
"the test reproduces that table in full" rule).
`NoirGearRulesetScopeTests` asserts the exact id sets loaded, that every cut
name above is genuinely absent, that no Shield skill or weapon class
appears (the Shield skill is cut — `orc-scope-filter.md`, Ch 3), that every
weapon's `SkillId` exists in `skill-ruleset.json`, and that every firearm
carries a typed listed-range value.

## Alternatives considered

**Keeping all 19 extracted firearms and 5 armor types.** Rejected outright
— this is exactly the "two hundred rows" failure mode `orc-scope-filter.md`
names, just at a smaller scale; the issue's acceptance criteria require the
hand-picked subset, not the full transcription.

**Inventing a `Melee Weapon (Improvised)` specialty to keep the chainsaw.**
Rejected: the issue explicitly forbids silently adding a new skill
specialty in this issue, and a one-off skill for one weapon is exactly the
kind of scope creep the skill list's own IN/OUT filter (Ch 3) is meant to
prevent. If a future noir scenario genuinely needs improvised-weapon
combat, that is a Layer 2 skill-list change with its own review, not a side
effect of a gear-data issue.

**Mapping the submachine gun to `Firearms (Rifle)` instead of `Firearms
(Handgun)`.** Considered, since both are technically imperfect fits.
Rejected in favor of Handgun because the book's own SMG description ("a
small machine gun, designed for one-handed use," p.194) and its printed
range (40, well below any of the rifles' 110-250) sit far closer to the
handgun cluster than the rifle cluster.

**A `HitLocation` enum instead of plain strings on `ArmorDefinition`.**
Deferred — no hit-location type exists anywhere in the engine yet, and
introducing one is a bigger design surface (it would need to interact with
the combat layer's damage-per-location rules) than this issue was asked to
cover. Plain strings keep the data faithful to the book without prejudging
that design.

## Consequences

- `Brp.Data` is now the fourth tenant of the loader pattern for `Brp.Rules`
  types (`NoirCharacterCreationRuleset`, `NoirBackgroundPackageRuleset`,
  `NoirExperienceRuleset`, and now `NoirGearRuleset`).
- #21 (missile range bands) is unblocked: it has `ListedRange` /
  `ListedRangeMeters` and `WeaponClass` to build its range-band and
  DEX-rank-tiebreak math against, with the shotgun banded-range question
  flagged above as still open for that issue to resolve.
- **Known limitation:** the submachine-gun-to-Handgun skill mapping and the
  chainsaw cut are both house calls made without reopening Layer 2's skill
  list; either could change if a future issue revisits
  `skill-ruleset.json`'s Firearm/Melee Weapon specialties.
- **Known limitation:** `WeaponClass` does not yet drive any combat
  ordering or range-band logic — it is recorded as data now so the future
  combat-mechanics issues (attack/defense matrix, DEX-rank tiebreak) do not
  also have to add it retroactively.
