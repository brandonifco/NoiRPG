# NNNN. Item SIZ/hit points for breaking doors, windows, and locks

## Status

Accepted — 2026-09-01. Resolves #230. Sub-issue of #116 (Ch 8 world completeness).

## Context

`orc-scope-filter.md`, Ch 8, line 137 keeps "Item SIZ/hit points for breaking doors,
windows, locks" in scope. Nothing in the engine resolved forcing or bashing through an
inanimate object before this record: `Brp.Rules.Combat.DamageResolver` only applies
damage to a character's `AbilitySet`, and no ruleset carried object SIZ/hit-point/armor
data.

The book's mechanic (Ch 8: Equipment, "General Qualities of Objects" and "Damage to
Inanimate Objects", p.224) is deliberately close to, but not identical to, the existing
character damage path:

> "Your gamemaster should consult the SIZ values for sample items and use SIZ as an
> object's hit points, assigning an armor value based on its equivalent... If the
> damage exceeds the object's armor value, then the hit points are reduced by the
> remaining damage and that many damage points reduce its armor value (representing
> how much less it is able to withstand damage once damaged). If an object is smaller
> than human-sized (such as a chair), it is totally destroyed if it is reduced to 0
> hit points."

"SIZ of Common Objects" (pp.225-226) prints sample SIZ ranges for a Door (4-8) and a
Glass door (8) and Glass window (3); "Armor Value of Substances" (p.224) prints armor
values for "1 cm of glass" (1) and "5 cm thick door" (3). Neither table prints a lock.

## Decision

**Reuse `DamageResolver.RollDamage` unchanged for the attack roll**, rather than a
bespoke damage-rolling path. `DamageRoll.DamageDealt` is already "raw damage minus
armor value, floored at zero" — the identical arithmetic p.224 describes as "the hit
points are reduced by the remaining damage" once the object's current armor value is
passed as `RollDamage`'s `armorValue` parameter. A caller rolls the attacker's weapon
damage exactly as it would for a character (`LandedGrade.Normal`,
`ArmorTreatment.Subtracted`, the object's current armor value in place of a defender's
armor) and hands the resulting `DamageRoll` to the new
`Brp.Rules.Combat.BreakableItemResolver.ApplyDamage`.

**One new resolver method for the one genuinely different rule.** The only thing p.224
adds beyond the existing character-damage arithmetic is armor degradation — armor
wearing down as the object takes hits — which characters never do (a character's armor
value is fixed). `BreakableItemResolver.ApplyDamage(currentHitPoints, currentArmorValue,
DamageRoll)` subtracts `DamageDealt` from hit points and reduces the armor value by 1
per landed hit (see "Rules interpretation: armor degradation" below for why 1 per hit,
not `DamageDealt` per hit), and classifies `BreakableItemCondition.Destroyed` at 0 or
fewer hit points.

### Rules interpretation: armor degradation

Two passages on the same page point different ways for these specific items, and this
is the committed reconciliation, recorded so a future conformance pass checks the
recorded decision rather than re-litigating the raw text.

**Passage A — "Damage to Inanimate Objects" (p.224), the general object rule:**

> "If the damage exceeds the object's armor value, then the hit points are reduced by
> the remaining damage and that many damage points reduce its armor value (representing
> how much less it is able to withstand damage once damaged)."

Read alone, Passage A supports degrading the armor value by the full penetrating
damage (`DamageDealt`) on every hit that exceeds it.

**Passage B — "Armor Value of Substances" (p.224-225), specific to the armor source
every shipped item uses:**

> "Natural armor values such as these above are not lost and do not deteriorate through
> multiple attacks, unless through some environmental means or a specific attempt to
> reduce the armor value of an object."
>
> "For example, your character bashes at a window made of bulletproof glass repeatedly
> with a sledgehammer, aiming at the same spot in an attempt to cause enough cracking to
> break through it. You[r] gamemaster decides to represent this by reducing the armor
> value by 1 with each successful hit, and rolling damage. When the damage roll
> overcomes the steadily reducing armor value, the window bursts."

**Passage B governs, for every item this ruleset ships.** Every entry
(`doorWoodInterior`, `doorGlass`, `windowGlass`, `lockPadlock`) draws its starting armor
value from "Armor Value of Substances" (p.224), the exact table Passage B's own
"natural armor values such as these above" refers to. Breaking through a door, window,
or lock — this resolver's entire purpose — is precisely "a specific attempt to reduce
the armor value of an object": Passage B's carve-out condition is met by construction,
not by interpretation. Passage B is also the more specific of the two (it names the
exact armor source this ruleset uses, where Passage A speaks of objects in general) and
supplies the book's only worked example of the carve-out actually being exercised. Under
ordinary interpretive practice, a more specific, example-backed passage governs over a
more general one it directly qualifies.

Passage A's hit-point arithmetic is unaffected by this reconciliation — Passage B says
nothing about hit points, only armor. `DamageRoll.DamageDealt` ("raw damage minus armor
value, floored at zero") still reduces hit points exactly as Passage A states; only the
armor-value degradation step changes to "1 per landed hit" (Passage B's worked example)
rather than "`DamageDealt` per landed hit" (Passage A's general phrasing).

**Corollary:** the worked example degrades armor "with each successful hit" without
qualifying it on whether that swing's damage penetrated — so `BreakableItemResolver`
degrades armor by 1 for any landed (non-Miss) hit, Normal/Special/Critical alike, even
one whose `DamageDealt` is 0 because the raw damage that swing rolled did not exceed the
armor value in effect for it.

**Correction (post-review, codex-conformance pass on PR #237):** the first
implementation of this record read Passage A alone and degraded armor by the full
`DamageDealt` per hit, without having read the "Armor Value of Substances" section's own
non-deterioration clause and worked example a few paragraphs later on the same page.
`BreakableItemResolver.ApplyDamage` and its tests were corrected to the "1 per landed
hit" reading recorded above; this section and this correction note were added in the
same pass.

**Stateless, caller-driven, like every other `Brp.Rules.Combat` resolver** (matching
`FallingResolver`, `PoisonResolver`): `ApplyDamage` takes and returns plain
hit-point/armor-value integers rather than introducing a new mutable "game object"
domain type. There is no existing object-state abstraction to hang this off (unlike
`AbilitySet`/`WoundTrack` for characters), and inventing one is out of scope for this
issue — a caller (a future Layer 5 game/scene loop) threads the returned
`ResultingHitPoints`/`ResultingArmorValue` into its own next call for a second hit.

**Only the "smaller than or about human-sized, destroyed at 0 HP" branch is built.**
p.224's other branch — an object larger than human-sized gets "a human-sized hole" in
one segment reduced to 0 HP, leaving the rest of the object standing — does not apply
to any item hand-picked here (a door, a glass door, a window, a lock are all
human-sized or smaller) and is out of scope; `BreakableItemCondition` only has
`Intact`/`Destroyed`.

**Data lives in `item-hit-points-ruleset.json`**, loaded by
`Brp.Data.NoirItemHitPointsRuleset.Load()` into a `Brp.Rules.Gear.BreakableItemRegistry`
of `BreakableItemDefinition` (Id, Name, Siz, HitPoints, ArmorValue, Source) — mirroring
the `WeaponDefinition`/`GearRegistry` Layer 4 pattern. Four hand-picked entries, per
`orc-scope-filter.md`'s "hand-pick... not two hundred rows" instruction:

| Id | Name | SIZ | HP | Armor | Source |
|---|---|---|---|---|---|
| `doorWoodInterior` | Door, Wood Interior | 6 | 6 | 3 | p.226 Door (4-8, midpoint), p.224 "5 cm thick door" |
| `doorGlass` | Door, Glass | 8 | 8 | 1 | p.226 Glass door (8); armor extrapolated from p.224's glass entry |
| `windowGlass` | Window, Glass | 3 | 3 | 1 | p.226 Glass window (3), p.224 "1 cm of glass" |
| `lockPadlock` | Lock, Padlock | 1 | 1 | 6 | House hand-pick, no printed row (see below) |

Every entry's hit points equal its SIZ, per p.225's printed guideline ("a simple
guideline for destroying objects is that an average object has hit points roughly
equivalent to its SIZ") — stored as an explicit ruleset-data field rather than derived
in code, per AGENTS.md invariant 7.

### Sourced or house rule

- **Wood interior door, glass door, glass window**: SIZ and armor values are direct
  quotes or a same-substance armor lookup (the door's 4-8 range is narrowed to its
  midpoint, 6, per p.225's own "use judgment when assigning SIZ" instruction — not a
  house mechanic, a printed invitation to pick within a printed range).
- **Glass door's armor value**: the book prints Glass door's SIZ (8) but no armor value
  specific to a door made of glass. Extrapolated from the "1 cm of glass" row (armor
  value 1) rather than the wood-door row, since the object is glass, not wood — the
  book explicitly sanctions this ("your gamemaster should be able to extrapolate
  additional armor values or estimate them based on rough equivalencies", p.224).
- **Padlock**: entirely a house hand-pick. The book's "SIZ of Common Objects" table
  has no lock entry at all. SIZ 1 is read off the "Comparative Sizes" table (p.225: SIZ
  1 spans 1-12 lb / 0.5-5.5 kg, which a padlock's weight fits) using that table's own
  stated latitude ("This table is not precise or restrictive: you should use judgment
  when assigning SIZ and weight based on the makeup of the item or creature"). Armor
  value 6 is a bare judgment call for a solid steel shackle — well below "3 cm of steel
  plate" (armor value 28, p.224) since a shackle is far thinner stock — under the same
  p.224 extrapolation invitation quoted above. This is the one entry with no printed
  anchor at all, flagged plainly rather than presented as a transcription.

## Explicitly out of scope

- **Forcing a stuck door open (STR vs. STR resistance roll)** — Ch 5: System,
  "Resistance Rolls" (p.129) names "attempts to force open a stuck door or bend an iron
  bar" as a STR-vs-STR example, but that contest is against the *door's* effective STR
  (not modeled anywhere, printed or otherwise) and is a different mechanic from "Damage
  to Inanimate Objects" (bashing through with a weapon). `orc-scope-filter.md` line 137
  names only "Item SIZ/hit points for breaking doors, windows, locks" — the
  damage/armor/HP mechanic this record builds. The generic `ResistanceResolver` already
  exists and is unrelated engine work if a future issue wants the STR-vs-STR spot rule.
- **Enforcing this against a live combat round or scene loop** — like every other
  `Brp.Rules.Combat` resolver, `BreakableItemResolver.ApplyDamage` returns a structured
  result; tracking an object's hit points/armor across multiple hits, narrating when it
  gives way, and any Locksmith-skill-based (non-destructive) lockpicking path are all
  caller/future-layer concerns.
- **Larger-than-human-sized objects and the "segment hole" rule** (p.224) — out of
  scope for this hand-picked door/window/lock set; see `BreakableItemCondition`.

## Consequences

- New `Brp.Rules.Gear` types: `BreakableItemId`, `BreakableItemDefinition`,
  `BreakableItemRegistry`.
- New `Brp.Rules.Combat` types: `BreakableItemResolver`, `BreakableItemDamageResult`,
  `BreakableItemCondition`.
- New ruleset, `item-hit-points-ruleset.json` / `Brp.Data.NoirItemHitPointsRuleset.Load()`.
- `Brp.Data.Tests.NoirItemHitPointsRulesetTests` reproduces every hand-picked row cell
  by cell; `Brp.Rules.Tests.Combat.BreakableItemResolverTests` covers the hit-point
  arithmetic, the pinned "armor degrades by 1 per landed hit, not by `DamageDealt`"
  behavior (including a large-penetrating-damage hit that still only costs 1 armor,
  and a non-penetrating hit that still costs 1 armor) with its p.224-225 citation,
  armor flooring at zero, destruction at 0 HP, and one end-to-end test that rolls a
  real weapon's damage through `DamageResolver.RollDamage` and applies it via
  `BreakableItemResolver.ApplyDamage`, demonstrating the reused machinery.
