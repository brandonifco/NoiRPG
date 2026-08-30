# 0015. Combat round: three phases, DEX-rank ordering, and the weapon-type tiebreak

## Status

Accepted — 2026-08-30. Resolves #47 (Layer 4 piece B). Feeds piece C
(`AttackDefenseMatrix`, a future issue).

## Context

Layer 4 gear (#42) and range bands (#21, ADR 0014) exist, but nothing said *who acts when*
in a combat round. Every combat mechanic above this layer — attacks, parries, dodges, spot
rules — resolves inside a round and in an order. This record adds the round scaffold and the
DEX-rank ordering engine, without resolving any attack (that is piece C's concern).

Ch 6: Combat, pp.142–144, is the sole source consulted. `engine-implementation-plan.md`'s
combat numbers are derivations, not authority (AGENTS.md invariant 2), and the plan's own
phase list ("Intent → Movement → Actions → Resolution") does not match the printed text —
see "The three-phase scope decision" below for what the book actually prints.

## Decision

### Combat round phases — sourced, with a scope-driven deviation

Ch 6, "Combat Round Phases" (p.142): "A combat round consists of four phases: **Statements**,
**Powers**, **Action**, and **Resolution**. These always occur in the same order and are
repeated with each new combat round until the combat is over."

NoiRPG implements **three** phases — `Statements`, `Action`, `Resolution` — omitting
**Powers**. This is a deliberate, owner-approved scope deviation, not a transcription gap:
the Powers phase (p.143) exists to sequence instantaneous-power activation by INT rank, and
`orc-scope-filter.md` cuts the entire powers/magic subsystem that phase serves in full
("Chapter 4: Powers — cut in full... Nothing in this chapter enters the codebase: magic,
sorcery, spells, divine/rune magic, psychic abilities, superpowers, mutations"). With no
powers to activate, the Powers phase has no content in this game; modelling it as a permanent
no-op phase would be dead configuration, not fidelity. `CombatRoundPhase` therefore has three
members, and `combat-round-ruleset.json`'s `combatRoundPhases` list carries three entries in
book order.

**Corrected transcription defect:** the rules-extractor's first pass at
`combat-round-ruleset.json` also listed Powers as a phase (matching the book) — that draft
was correct on this point and was edited down to three phases only after this owner decision,
not because the extractor mistranscribed the count.

### DEX rank: numerically equal to the DEX characteristic — sourced

Ch 6, p.142: "your character can perform actions and react to other actions in an order
usually determined by their DEX characteristic; higher DEX characters act before characters
with lower DEX." The glossary (p.2) is consistent but non-numeric: "DEX Rank: Based on the
Dexterity characteristic, this determines when your character can usually act."

The exact numeric identity — DEX rank equals the DEX characteristic's value, not a derived or
scaled figure — is confirmed by the (cut) spellcasting examples in Ch 4: Powers (p.57): "a
magician with DEX 15 wants to cast a spell in a combat round, the spell is cast at DEX rank
−1 per level of the spell. Thus, a level 1 spell is cast at DEX rank 14 (15−1=14)." This only
works arithmetically if DEX rank starts as the bare DEX value. `CombatRoundRuleset` carries
this as `DexRankSourceCharacteristic` ("DEX") for provenance; `CombatActionRequest.BaseDexRank`
is supplied by the caller (typically `abilitySet.ValueOf(new CharacteristicId("DEX"))`) rather
than derived internally — `Brp.Rules.Combat` takes no dependency on `Brp.Core.Abilities`,
matching the existing pattern in `RangeBandResolver`, which likewise takes a plain `int
dexterity` rather than an `AbilitySet`.

### Ordering direction — sourced

Ch 6, p.142: "higher DEX characters act before characters with lower DEX" — descending order.
No alternative direction is stated; `CombatRoundRuleset.DexRankOrderedDescending` still reads
this from data (invariant 7) rather than hardcoding the sort direction.

### The weapon-type tiebreak: four tiers, not five — sourced, with a corrected transcription defect

Ch 6, "Action" (p.143): "Within a particular DEX rank, attacks usually go in order of weapon
type. Attackers armed with missile weapons (bows, guns, etc.) are considered to act before
those in hand-to-hand (**melee**) combat. After these go characters armed with long weapons
(spears, lances, etc.), then those with medium-length weapons (swords, axes, etc.) and finally
those with short weapons (daggers, etc.) **or who are unarmed**."

**Corrected transcription defect:** the rules-extractor's first pass at
`combat-round-ruleset.json` listed `["missile", "melee", "long", "medium", "short"]` — five
tiers, with "melee" as a distinct entry between missile and long. This misreads the passage:
"hand-to-hand (melee)" is the umbrella term the sentence uses to name the *set* of the three
tiers that follow it (long, medium, short/unarmed), not a fifth tier standing beside them.
Nowhere in the weapon tables (Ch 8) is there a "melee" weapon class distinct from long,
medium, or short — "melee" is a category label, not a weapon type. The corrected order is
**missile → long → medium → short**, four tiers, with "short" also covering unarmed
combatants per the printed text. `WeaponTypeTier` has four members
(`Missile`, `LongWeapon`, `MediumWeapon`, `ShortOrUnarmed`; the `Weapon`/`OrUnarmed` suffixes
avoid CA1720's "identifier contains a type name" analyzer rule, which flags bare `Long` and
`Short`) and `combat-round-ruleset.json`'s `weaponTypeTiebreakOrder` carries the corrected
four-entry list.

The book's own fallback beyond weapon type — "If there is a need to determine who acts first
when DEX ranks are tied, use the relevant skill... If these are still tied, the actions occur
simultaneously" (p.142, restated for weapon-type ties by extension) — is not implemented here.
`CombatRound.Create` leaves combatants tied on both DEX rank and weapon tier in their input
order, which is out of scope for this piece (it needs each combatant's skill rating, which
belongs to piece C).

### Movement fractions the DEX rank, applied first — sourced

Ch 6, "Move" (p.144): "Moving between 6-15 meters means that your character acts at 1/2 their
normal DEX rank. Moving between 16-29 meters in a combat round means that your character acts
at 1/4 their normal DEX rank. These modified DEX ranks are cumulative with penalties for
additional actions, with movement modifiers to DEX rank being applied first." Distances of 5m
or less (an ordinary attack's own "moving up to 5 meters" allowance, per "Attack," p.144) and
distances of 30m or more (the book states no tier there; a fully-unengaged character's move
without other actions) leave the DEX rank unmodified — no third tier is invented for either
case.

**House interpretation, unsourced:** the book states the 1/2 and 1/4 fractions but never
states a rounding direction for the resulting rank. `EffectiveDexRankCalculator.ApplyMovement`
truncates toward zero (rounds down for the non-negative ranks the game uses). No printed
example in Ch 6 exercises this fraction with an odd DEX value to settle the direction either
way; this is a house call, not a transcription from the text.

### Flat penalties: drawing a weapon, and successive-attack spacing — sourced, modelled as ordering arithmetic only

Ch 6, "Noncombat Action" (p.144): "An unengaged character can attempt the use of a skill or
power or do some other action not requiring a skill check, such as drawing a weapon or opening
a door. ... These actions, if combined with combat actions or multiple non-combat actions,
incur a DEX rank penalty of 5 per action."

Ch 6, "Attack" (p.144): "If your character can perform more than one action in a round (some
weapons allow for multiple attacks, and combat skill levels in excess of 100% also allow
multiple attacks), each attack should be separated by 5 DEX ranks. The first action is at the
full DEX rank; the second is at DEX rank −5; the third at DEX rank −10; etc."

Both passages state the same number — 5 — for two related but distinct situations: combining
a noncombat action (e.g. drawing a weapon) with another action, and spacing successive attacks
within a round. `CombatRoundRuleset` keeps these as two separate fields
(`DrawWeaponDexRankPenalty`, `MultipleActionDexRankPenalty`), both sourced to the value 5
today, so a future ruleset could diverge them without that being mistaken for two
independently-stated printed numbers — the book gives one number used in two contexts, not
two numbers.

**Deliberately out of scope (the seam for piece C and beyond):** this piece models only the
*spacing arithmetic* — given a flat penalty, subtract it after movement. It does **not**
decide *whether* a combatant is drawing a weapon, or *what* grants more than one action per
round (a multi-attack weapon, or combat skill over 100%). `CombatActionRequest.FlatDexRankPenalty`
is supplied by the caller, who is expected to sum
`ruleset.DrawWeaponDexRankPenalty` and/or `N * ruleset.MultipleActionDexRankPenalty` as their
own situation requires. Building that decision logic is piece C's (or a later piece's)
concern.

### The DEX-rank-0 floor — sourced

Ch 6, "Attack" (p.144): "Your character cannot act on DEX rank 0, so any actions that would
occur below DEX rank 1 are lost." `CombatRoundRuleset.DexRankFloor` (0) is read from data
rather than hardcoded; `EffectiveDexRankCalculator.Compute` returns `null` — not a clamped
rank of 0 or 1 — for any action whose computed rank is at or below the floor, and
`CombatRound.Create` drops such actions from `ActionPhaseOrder` entirely rather than ordering
them last. A lost action is absent from the sequence, not merely ranked lowest.

### What this piece does not build

- **Attack/defense resolution** (`AttackDefenseMatrix`, Ch 6 "Resolution," p.145 onward) —
  piece C, a separate issue.
- **Damage, wounds, unconsciousness/death** — pieces D/E.
- **Spot rules** (ambush, cover, darkness, etc.) — piece F.
- **The optional Initiative Rolls variant** (Ch 6, "Initiative Rolls (Option)," p.143: D10 +
  DEX, or D10 + INT for powers) — cut per `orc-scope-filter.md`'s OFF-list. `CombatRound`
  exposes no D10 or initiative-value concept anywhere in its public surface; a reflection-based
  scope test (`CombatRoundTests.The_optional_initiative_rolls_variant_is_not_implemented`)
  pins this.
- **What triggers a combatant having more than one action per round** (multi-attack weapons,
  combat skill over 100%) — the spacing arithmetic exists; the trigger logic does not, and is
  left for piece C or a later piece.
- **Statements-phase alternates** (p.142: "Removing the Statement of Intent," "Reverse Order
  Statement of Intent") — optional variants, out of scope, same reasoning as Initiative Rolls.
- **Total Hit Points, Encumbrance, Miniatures/Maps/VTT grids, Fatigue Points** — cut/deferred
  per the scope filter; none of these concepts appear in this piece's data or code.

## Consequences

- Piece C (`AttackDefenseMatrix`) consumes `CombatRound.ActionPhaseOrder` — an
  `IReadOnlyList<CombatantTurn>` — to know when each combatant's attack/defense resolves, and
  supplies `CombatActionRequest.FlatDexRankPenalty` itself once it has decided how many actions
  a combatant is taking and whether one of them is a weapon draw.
- The movement-fraction rounding direction is a house call (see above) and may need revisiting
  if a later spot rule or worked example in the book turns out to depend on the opposite
  direction; flag any such finding against this record rather than the new piece.
- The two flat-penalty fields (`DrawWeaponDexRankPenalty`, `MultipleActionDexRankPenalty`) are
  presently identical (5) because the book only ever states one number; this is not itself an
  invariant a future edit should assume is permanent.
