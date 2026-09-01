# 0029. Special-damage effects and Fighting Defensively

## Status

Accepted — 2026-09-01. Resolves #113. Consumes #52's damage-number piece
(`docs/decisions/0017-damage.md`), which explicitly deferred every special-damage-type
*effect* (only the damage *number* each type produces was built there).

## Context

ADR 0017 built the damage-number arithmetic for all five special-damage types
(Bleeding, Crushing, Entangling, Impaling, Knockback) but explicitly deferred each
type's mechanical *effect* -- Crushing's stun, Impaling's lodged-weapon/extraction
rules, Knockback's resisted shove, Bleeding's ongoing loss, and Entangling's
immobilization -- naming them out of scope for #52. Fighting Defensively (Ch 6, p.151)
is a separate, unrelated Ch 6 core mechanic that had never been built at all. This
record covers both, per #113.

Ch 6: Combat, pp.149-151, is the sole source consulted (extracted via
`tools/source-slice.py --pages 148-151`).

## Decision

Every effect is built as a stateless, caller-driven resolver -- a "structured outcome"
returned to the caller rather than a hidden state mutation or an enforced round-loop
integration this layer does not own (matching the shape of every existing
`Brp.Rules.Combat` resolver, e.g. `DiseaseResolver`, `HealingResolver`). New ruleset
data lives in `special-damage-effects-ruleset.json` /
`Brp.Rules.Combat.SpecialDamageEffectsRuleset`, loaded by
`Brp.Data.NoirSpecialDamageEffectsRuleset.Load()`.

### Crushing stun (`CrushingStunResolver`) -- live trigger

Ch 6, pp.149-150: a Crushing special success (clubs, brass knuckles -- every shipped
Crushing weapon) requires a Stamina roll or the target is stunned for 1D3 rounds.
**Sourced**: the duration dice (1D3), the printed penalties (cannot attack; dodge/parry
gated behind a successful Idea roll each attempt; attacks against the target are Easy;
fleeing needs Idea then Agility) are all direct quotes. The "Stamina roll" is modeled
as the standard CON roll (CON x5) -- the same mapping `DiseaseResolver.RollContraction`
already uses for the identical named roll elsewhere in this codebase (the book does not
restate Stamina's rating in this section, so this is not a fresh interpretation).

### Impaling lodged weapon (`ImpalingLodgedWeaponResolver`) -- live trigger

Ch 6, pp.149-150: an Impaling special success (firearms, pointed knives -- every
shipped Impaling weapon) leaves the weapon lodged. **Sourced**: the immediate
(Difficult) and focused (full-chance) extraction rolls, the target's own STR-vs-
cumulative-damage resistance-roll self-extraction (with its 1D3 failure penalty), and
the "half the weapon's damage roll" movement-damage rule are all direct quotes.

**House rule by precedent**: the book states no rounding direction for halving the
fresh movement-damage roll. Rounded up, matching the book's own consistently-stated
round-up convention used everywhere else a fraction arises (hit points p.13, major
wound level p.14/p.155, thrown-weapon damage modifier p.147) -- see
`special-damage-effects-ruleset.json`'s `impaling.movementDamageRoundingNote` and
`ImpalingLodgedWeaponResolver.RollMovementDamage`'s remarks.

### Knockback (`KnockbackResolver`) -- dormant, no shipped weapon

Ch 6, p.151: a Knockback special success (no shipped weapon uses this type -- ADR
0017) pits the total damage rolled against the target's SIZ in a resistance roll, then
(on a lost resistance roll) knocks the target back one meter per 5 points of damage,
rolls an Agility check against falling prone, and optionally rolls obstacle-impact
damage (1D6 per 3 meters or fraction thereof remaining).

**House interpretation, resistance-roll direction**: the printed prose narrates the
single resistance roll from the target's point of view ("if unsuccessful, knocked
back" / "if the target wins, not moved") without stating which side is the resistance
table's "active" party in the Ch 5 formulaic sense. The engine reads active = the
damage rolled, passive = the target's SIZ (Ch 5, p.129: "active" is "the party or force
trying to influence the passive factor" -- the knockback force is what tries to move
the target, resisted by their SIZ), so that a higher damage total relative to SIZ
raises the chance of a successful knockback. This is the only assignment under which
the resistance formula's `chance = 50 + 5*(active-passive)` increases with more damage
relative to SIZ, matching the intuitive physics the rest of the rule describes (see
`special-damage-effects-ruleset.json`'s `knockback.resistanceRollDirectionNote` and
`KnockbackResolver.Resolve`'s remarks for the full reasoning). The "one meter per 5
points of damage" figure is a straight floor division (no "or fraction thereof"
language, unlike the obstacle-impact rule three sentences later, which explicitly says
"or fraction thereof" and is rounded up accordingly) -- sourced, not a house choice.

### Bleeding (`BleedingEffectResolver`) -- dormant, no shipped weapon

Ch 6, p.149: a Bleeding special success (no shipped weapon uses this type -- ADR 0017,
no edged slashing weapon is in the hand-picked gear subset) opens an ongoing 1 HP
(and 1 fatigue point, if used) loss each round on DEX rank 1, staunchable by a Stamina
roll (Difficult on other actions while attempting; canceled by dodging), and stopping
permanently after five consecutive staunched rounds. All figures sourced directly.

### Entangling (`EntanglingEffectResolver`) -- dormant, no shipped weapon

Ch 6, pp.150-151: an Entangling special success (no shipped weapon uses this type --
ADR 0017, no net/rope/flexible weapon is in the hand-picked gear subset) immobilizes
the target for the rest of the round and the next, escapable by an Agility roll or a
STR-vs-STR resistance roll on the following round, and negated by a Dodge/Wrestle
success or a *critical* parry against a *critical* entangle (an ordinary parry success
has no effect -- sourced directly, not a simplification). The follow-up Grapple effects
an attacker may choose from (immobilize limb/target, throw, knockdown, disarm, injure,
strangle) are named as an enum (`GrappleFollowUpEffect`) -- a structured seam only; each
effect's own mechanics belong to the not-yet-built Grapple skill piece, explicitly out
of scope here.

### Fighting Defensively (`FightingDefensivelyResolver`)

Ch 6, p.151: forgoing all attacks for a round substitutes one free, unpenalized
**Dodge** attempt for the round's attack ("one free Dodge attempt", "a free Dodge
skill attempt", "Essentially, it is a free Dodge" -- Dodge-only, never a Parry).
Only if the character can normally make multiple attacks per round (e.g. a skill over
100%) is a *second* free defense granted, and that second one may be either a Dodge or
a Parry ("a second free Dodge or parry"). The count is capped at two: it is gated on
multi-attack capability, not on how many attacks were forgone, so a character forgoing
three attacks by having three actions still gets at most two free defenses, not three.
`Declare(bool canMakeMultipleAttacksPerRound)` returns
`FirstFreeDefenseType = DefenseType.Dodge` unconditionally and
`SecondFreeDefenseAvailable` / `SecondFreeDefenseAllowedTypes = [Dodge, Parry]` only
when that flag is true.

**Correction (post-review):** the first implementation of this record exposed an
untyped `FreeDefenseAttempts` count set to the caller-supplied number of attacks
forgone (so three forgone attacks wrongly yielded three free, type-unrestricted
defenses). Codex conformance caught both defects against the printed text above; the
signature and return shape were redesigned as described here rather than patched
in place, since the original shape could not express "first is Dodge-only, second is
conditional and capped at one."

This piece also gives the successive Dodge/parry -30% cumulative penalty (Ch 6, pp.144
and 151) its first implementation, previously a named-but-unbuilt seam
(`attack-defense-matrix-ruleset.json`'s `deferred` list, ADR 0016) -- built here because
Fighting Defensively's entire point is exempting its free attempt(s) from that count.
`SuccessiveDefensePenaltyPercent(countedPriorAttempts, ...)` computes the cumulative
-30%-per-attempt penalty; a caller simply never passes a free Fighting-Defensively
attempt into `countedPriorAttempts`, which is how "does not incur the cumulative
penalty" and "the modifier does not increase" (p.151) are satisfied without special
casing. `ForfeitsAllAttacksThisRound`, `CannotCombineWithAnyOffensiveAction`
(including the Desperate Action, named explicitly), and
`CannotDodgeAndParryWithinTheSameDexRank` are direct quotes, exposed as caller-read
flags rather than enforced against a round loop this layer does not own.

## Explicitly out of scope (seams for other pieces)

- **Enforcing any of these effects against a live round loop** -- every resolver here
  returns a structured outcome; applying it (skipping a stunned character's attack
  phase, tracking an entangled target's immobilization across rounds, moving a
  knocked-back combatant on a map) is a future combat-loop concern.
- **The Grapple skill's own mechanics** -- named only as an enum seam
  (`GrappleFollowUpEffect`) for Entangling's follow-up round.
- **A fatigue-point subsystem** -- Bleeding's 1-fatigue-point/round loss is reported
  (`BleedingRoundLoss.FatiguePoints`) but not applied; no fatigue-point subsystem exists
  in this engine yet.
- **First Aid stopping bleeding** -- Ch 6, p.149's "the most reliable way to stop
  bleeding damage is a successful First Aid roll" is narrative; the existing
  `HealingResolver.ResolveFirstAid` already models a generic First Aid roll, and a
  caller choosing to treat one of its successes as "bleeding stopped" rather than
  "hit points healed" is a caller-level interpretation, not a new resolver method.

## Consequences

- Six new resolvers in `Brp.Rules.Combat`: `CrushingStunResolver`,
  `ImpalingLodgedWeaponResolver`, `KnockbackResolver`, `BleedingEffectResolver`,
  `EntanglingEffectResolver`, `FightingDefensivelyResolver`.
- One new ruleset, `SpecialDamageEffectsRuleset` / `special-damage-effects-ruleset.json`
  / `NoirSpecialDamageEffectsRuleset.Load()`, holding every numeric value the six
  resolvers read (AGENTS.md invariant 7).
- `ADR 0017`'s "Explicitly out of scope" section (special-success effects) is now
  resolved by this record; ADR 0017 itself is not edited, per the "a decision that
  turns out wrong (or, here, incomplete) gets a new record" convention.
