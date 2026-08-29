# BRP Rules Engine — Implementation Plan v0.1
*Target: C# / .NET class library usable by (a) a video game client and (b) a gamemaster scenario authoring & testing tool.*
*Source text: `BasicRoleplaying-ORC-Content-Document.pdf` (Basic Roleplaying: Universal Game Engine, Chaosium, 2023), 303 pp., ORC License.*
*Scope is governed by [orc-scope-filter.md](orc-scope-filter.md) — read that first. Roughly 60% of the book is out of scope.*
*Superseded: v0.1 of this plan was written against `BRP SRD 1.0.2.pdf` (2020, BRP-OGL). That document is not our source.*

---

## 0. Source text — resolved

The framework doc was right and my first pass was wrong. The repo now holds the **ORC Content Document** (BRP: Universal Game Engine, 2023), which is what `noir-rpg-framework.md` described all along. The 23-page `BRP SRD 1.0.2.pdf` is a different, much smaller 2020 document under a different license; it is not our source and should be treated as superseded reference only.

Three corrections follow from that, all of which loosen constraints I previously flagged.

### 0.1 Licensing is lighter than BRP-OGL

The ORC document states that, trademarks aside, the entire text of *Basic Roleplaying: Universal Game Engine* is available for personal and commercial use under the ORC License, and asks for a "Powered by BRP" credit. Obligations: an ORC attribution notice, and don't use Chaosium trademarks as your own branding.

**There is no Prohibited Content list.** BRP-OGL §1(e) carved out Sanity, Passions, Allegiance, Augments, Reputation, and more; ORC has no equivalent, and those mechanics are present *inside* the licensed text here. There is also **no logo-display requirement** equivalent to BRP-OGL §15.

### 0.2 Composure and Vices can be built on the book's own mechanics

My earlier warning — that Composure and Vices had to be original designs to avoid Prohibited Content — **is withdrawn.** Under ORC:

- **Sanity** is licensed content. Composure/Corruption can derive from it directly.
- **Passions** are licensed content, and the book's own **Noir** setting entry *recommends* them. They are the intended vehicle for vices, obligations, and loyalties.

This is a large scope reduction: two subsystems move from "design from first principles" to "re-skin and tune."

### 0.3 Everything the framework assumed is present

| Framework assumes | In the ORC document? |
|---|---|
| Criticals | **Yes** — five degrees of success, critical at 1/20. |
| Major wounds | **Yes.** |
| Hit locations | **Yes**, optional, with armor-by-location. |
| Chase rules | **Yes.** |
| Passions / allegiances | **Yes**, both. |
| Sanity | **Yes.** |
| Fatigue | **Yes**, optional. |
| Augments (one ability aiding another) | **Yes** — and see Complimentary Skills in the scope filter; directly useful for multi-route clue access. |
| Charisma (CHA) | **Yes** — CHA, not APP. The framework doc was correct. |

The design risk has inverted. It is no longer "these mechanics don't exist"; it is **"too much exists, and scope discipline is the whole game."** Hence the scope filter.

## 0.4 Sequencing against the development plan

`development-plan.md` places engine and platform choice in **Phase 2**, informed by
what the Phase 1 paper prototypes reveal, and gates expensive work behind the Phase 1
go/no-go. This plan starts engine work now, which is a departure.

**Resolution: Layer 0 proceeds in parallel with Phase 1; Layers 1 and above wait for
the Phase 1 gate.**

The rationale is that Layer 0 — seeded dice, the resolution kernel, the modifier
pipeline — is the one part of the engine that is *independent of every open design
question*. It does not care whether interrogations are fun, how clues route, or how
much a skill improves. It is also what the paper prototypes increasingly need: the
advancement simulation and the case validator are already re-implementing dice and
grading logic in Python, and a shared kernel stops those from drifting apart from
the engine before the engine exists.

Everything above Layer 0 genuinely depends on Phase 1 outcomes and should not be
built until the gate clears. Layer 5 in particular is already partly settled on the
Python side (`cases/SCHEMA.md`, `tools/case_validator.py`) and must be reconciled
with, not duplicated by, the C# work.

## 1. Architecture decisions (make these once, now)

**D1 — Code holds procedures, data holds values.**
Every number in the source becomes a record in a JSON ruleset, not a constant in C#. The Skill Results Table, damage modifier table, resistance table, weapon and armor stats, skill base chances, profession skill lists — all data. This is what lets the noir setting drop the out-of-scope skills, take modern-era base chances over historical ones, and swap optional-rule modules on and off without forking the engine. It is also what makes the scope filter enforceable: cutting content becomes deleting data rows, not editing code.

**D2 — Every roll returns full provenance, never a bare bool.**
`RollOutcome` carries: the raw d100, the base chance, an *ordered list of every modifier that touched it*, the effective chance, the special/fumble thresholds, and the grade. The framework pillar *"the rating is the displayed probability"* is only deliverable if the engine can render `Fast Talk 65% → 33% (darkness ÷2)` on demand. This is also the entire debugging story for a GM tool.

**D3 — Modifiers are a typed, ordered pipeline — not an int.**
The SRD mixes four incompatible modifier kinds and never states an ordering:

| Kind | SRD examples |
|---|---|
| Gate | Automatic (no roll, succeeds), Impossible (no roll, fails) |
| Override | Shield parry vs missiles: flat 15% / 30% / 60% / 90% |
| Additive | Armor `−20% to physical skills`, Firing into combat `−20%` |
| Multiplicative | Easy `×2`, Difficult `×½`, range band 2× `×½`, range band 3× `×¼` |

Note **`×¼` exists** (weapon range band 3), so `Difficulty` cannot be a closed enum — it must reduce to a rational multiplier. Canonical order must be a documented, configurable policy: `Gate → Override → Additive → Multiplicative → Clamp`. Also undefined by the SRD and therefore a policy decision: **do two Difficults stack to ×¼ or stay ×½?** (darkness + firing into combat, SRD 6.4 + 6.8).

**D4 — Determinism and a recorded roll log.**
Seedable, serializable PRNG injected everywhere; no `Random` static, no `DateTime.Now`. Every roll appends to an event log with sequence number and context. This single decision buys four things at once:
- the reload-scumming answer the review notes demand (pre-seeded rolls are a two-line policy, not a rewrite),
- reproducible bug reports from a shipped game,
- regression tests,
- the Monte-Carlo harness that answers *"does advancement register over 8–12 cases?"* (review note §5).

**D5 — Gamemaster discretion is a first-class port, not an omission.**
The SRD says "at the gamemaster's discretion" ~20 times (difficulty assignment, opposed-failure interpretation, cover penetration, disease conditions, antidote cross-effect, special success narrative result). An engine that hardcodes these silently is lying. Model them as `IAdjudicator` decision points with a named id. The video game supplies an authored policy; the GM tool prompts a human; tests supply a deterministic stub. **This is the concrete meaning of "everything accessible and quantified"** — quantify the discretion points rather than erasing them.

**D6 — Pure core, zero engine dependency.**
`net8.0`, no Unity/Godot/MonoGame references in `Brp.Core` or `Brp.Rules`. No reflection-heavy DI, `System.Text.Json` source generators — keeps IL2CPP/AOT viable if Unity is chosen later. Engine adapters are separate projects added when the platform decision is made.

**D7 — Centralize rounding.**
The resolution kernel is all `ceil` (§2), but other rules round differently, and the superseded 2020 SRD rounded special success differently again. One `Rounding` policy type; every formula names which rule it uses. This is the classic source of silent divergence between an engine and its source book.

---

## 2. The resolution kernel — re-derived and verified

The book's Skill Results Table is the conformance spec. I derived closed-form rules and checked them against **all 24 printed rows**, including the rows above 100%. All four hold exactly.

Let `c` = the modified chance of success after all modifiers.

| Grade | Rule |
|---|---|
| **Critical** | roll ≤ `ceil(c / 20)` |
| **Special** | roll ≤ `ceil(c / 5)` |
| **Success** | roll ≤ `c` |
| **Failure** | roll > `c`, or roll ≥ 96 regardless of `c` |
| **Fumble** | roll among the top `max(1, ceil((101 − c) / 20))` results; a roll of 00 always fumbles |

Five behaviors the engine must encode explicitly, each of which is easy to get wrong:

1. **Five grades, not four.** `Fumble < Failure < Success < Special < Critical`.
2. **The special range contains the critical range, and they do not stack.** When a roll qualifies as both, apply the critical result only — never both. This is a stated rule, not an inference, and it's a natural source of double-counting bugs.
3. **Rolls of 96+ always fail**, no matter how high the skill. There is no auto-success. The exception is resistance rolls, where a 10-point characteristic advantage reduces failure to a roll of 00 only.
4. **Skills whose base chance is 5% or higher always succeed on 01–05**, even when difficulty or penalties push the modified rating below 5%. This is a floor on the *modified* value, and it resolves cleanly a question the 2020 SRD left ambiguous.
5. **Ratings above 100% are natively supported.** The table runs past 120 and continues by fives. Critical and special thresholds keep climbing; fumble stays at 00.

**These formulas differ from the 2020 SRD.** That document banded its table differently and produced round-half-up for special success; this one is a clean `ceil`, and it adds the critical grade the older table lacked. Any test fixture written against the old document must be discarded rather than adapted.

### Also load-bearing
- **Resistance table** — active vs. passive characteristic, 50% at parity, ±5% per point of difference, with its own failure rule at 10+ points of advantage.
- **Damage modifier** — derived from STR+SIZ via a lookup table; recompute whenever either characteristic changes.
- **Difficulty grades** — the book's difficulty tiers reduce to multipliers on `c`. Confirm the full set against Chapter 5 before coding, since the grade names differ from the 2020 SRD's.

## 3. Dependency-ordered build

Each layer depends only on the layers above it. Nothing in Layer *n* may reference Layer *n+1*.

### Layer 0 — Zero dependencies (**start here**)
| Component | Content |
|---|---|
| `IEntropySource` | Seedable, serializable-state PRNG. `NextD100()`, `NextDie(int sides)`. |
| `DiceExpression` | Parse + evaluate `3D6`, `2D6+6`, `1D10+1D4`, `1D6-2`, `1D8+2+db`, `½db`. Returns individual die faces, not just a sum (UI + replay need them). |
| `Percent` | Value type. Unbounded above 100 (SRD allows >100% skills), floors at 0. |
| `Rounding` | `HalfUp`, `Up`, `Down` — named at every call site. |
| `GameTime` | Combat round = 12s; turn = 5 min = 25 rounds; day; week. Skill-time bands from SRD 4.4. |
| `SuccessLevel` | `Fumble < Failure < Success < Special`, ordered & comparable. Extensible for a later `Critical`. |
| `RollModifier` / `ModifierChain` | D3's four kinds + canonical ordering policy. |
| `ResolutionTable` | Special & fumble threshold computation, loaded-and-verified against the printed table. |

### Layer 1 — Abilities
- `Characteristic` as **data** (id, name, generation formula) — not a hardcoded enum. Ships as STR, CON, SIZ, INT, POW, DEX, **CHA**, with point-buy as the NoiRPG generation mode.
- `CharacteristicRoll(characteristic, multiplier)` → `Percent`. Covers Effort/Stamina/Idea/Luck/Agility/Charisma at ×5 *and* the disease-recovery ladder at ×1..×5 with one type.
- `DerivedCharacteristic` as recomputable formulas: `MOV`, `HP = RoundUp((CON+SIZ)/2)`, `PowerPoints = POW`, `DamageBonus = table(STR+SIZ)`. **Must recompute on characteristic loss** — disease and poison drain characteristics (SRD 6.5, 6.10), so these cannot be baked at creation.
- `AbilityResolver` — *the single most-used API in the system.* `(ability, modifiers, entropy) → RollOutcome`.
- `ResistanceRoll` and `OpposedRoll` (SRD 3.3 degrade rules: Special vs Special → two failures but experience checks still allowed; Special vs Success → Success vs Failure; ties → higher rating wins).

### Layer 2 — Skills
- `SkillDefinition`: id, name, base chance which may be a constant (`Spot 25`) **or a formula** (`Dodge DEX×2`, `Language(Own) INT×5`, `Gaming INT+POW`, `Fly DEX×½ | DEX×4`) **or era-conditional** (`First Aid 30 | INT×1`, `Drive 20 | 01`) **or weapon-derived** (`Firearm var`).
- `Specialty` — `Knowledge(Law)` is a distinct skill instance sharing a parent definition. Required by the experience rule ("two specialties are two checks") and by profession packages.
- `SkillRegistry` loaded from ruleset data. The noir 18-skill list becomes a data file, not a code change.

### Layer 3 — Characters
- `Character`: identity, characteristic set, skill set with per-skill experience flags, current/max HP, power points, wounds, equipment.
- `CharacterBuilder`: SRD path (roll characteristics, ±3 point shift, profession = 300 pts across 10 skills, personal = INT×10, 75% soft cap) **and** the noir point-buy path with background packages. Both produce the same `Character`; validation rules are data.
- `ExperienceSystem`. **NoiRPG deviates from BRP RAW here, and the deviation is locked** — see `noir-rpg-framework.md` v0.2. RAW awards a tick only on *successful* use; `tools/advancement_sim.py` showed across 10,000 characters per scenario that this is nearly invisible at video-game length and starves low skills, which rarely succeed and so rarely tick. The rule is **tick-on-use**: exercising a skill under real stakes earns the tick whether the roll succeeded or failed. The improvement roll at case close still gates the gain, so high skills stay slow and grinding stays pointless.
  - Implement tick-on-use as the default policy, with RAW available as a ruleset toggle so the simulation stays re-runnable against both.
  - Retained from RAW: the improvement roll (`d100 > skill` → gain), one tick per skill per case, no tick when the check was Easy or nothing was at stake, and teaching.
  - The "nothing at stake" gate must be enforced mechanically — there is no gamemaster to adjudicate it.

### Layer 4 — Combat, gear, spot rules
- Weapon/armor/shield definitions as data, including range bands (`≤R` normal, `≤2R` ×½, `≤3R` ×¼, `>3R` impossible) and armor's skill penalties by skill *category* (SRD names the physical and perception sets explicitly).
- `CombatRound`: four phases (Intent → Movement → Actions → Resolution); DEX-rank ordering with the weapon-type tiebreak (missile → long → medium → short/unarmed); movement tiers (6–15 m = ½ DEX rank, 16–29 m = ¼ DEX rank); draw weapon = −5 DEX ranks.
- `AttackDefenseMatrix` — the 7-row table as data, not an `if` chain.
- Damage: armor subtraction, special success = `weaponMax + normalRoll + db`, unconscious at ≤2 HP, dead at 0 at end of round, negative HP tracked, knockout rule (Difficult attack, damage > ½ *total* HP).
- Wounds tracked **individually** — First Aid heals 1D3 *per wound*, capped at that wound's damage, once per wound. A single `currentHp` integer cannot express this; model wounds as a list.
- Spot rules as pluggable modifier sources: ambush, backstab, cover, darkness, disease ladder + illness severity, falling (1D6 per 3 m), firing into combat, poison + antidotes (POT vs CON on the resistance table).

### Layer 5 — Setting & scenario (your original content)
`Noir.Rules` (trimmed skills, background packages, Composure, Vices — original designs per §0.2) and `Noir.Scenario` (cases as data, clue routing, the multi-route rule from review note §1, narrative-state junction budget from review note §2). Nothing here belongs in `Brp.*`.

---

## 4. Project layout

```
src/
  Brp.Core/          # Layers 0–2. No game concepts. No engine refs.
  Brp.Rules/         # Layers 3–4.
  Brp.Data/          # Ruleset JSON + schema + loader (source-generated).
  Brp.Simulation/    # Monte-Carlo harness, balance reports.
  Noir.Rules/        # Layer 5 — setting.
  Noir.Scenario/     # Layer 5 — cases, clues, GM authoring model.
tests/
  Brp.Core.Tests/    # SRD conformance fixtures: the printed tables, verbatim.
  Brp.Rules.Tests/
tools/
  Brp.Cli/           # roll / simulate / validate — a GM can test a scenario with no game client.
```

---

## 5. Milestone 1 — the first thing to build

**Scope:** `Brp.Core` Layer 0 + `AbilityResolver` + `ResistanceRoll` + `OpposedRoll`.

**Acceptance criteria:**
1. All 24 rows of the Skill Results Table — including the rows above 100% — reproduce exactly from the §2 formulas, for all five grades.
2. The resistance table reproduces across its full printed range, including the 10-point-advantage failure rule.
3. `DiceExpression` round-trips every notation appearing in the in-scope chapters, including half-damage-modifier forms and negative modifiers flooring at 0.
4. Same seed + same call sequence ⇒ byte-identical roll log.
5. `brp roll --skill 65 --difficulty difficult --modifier "-20 firing-into-combat" --seed 42` prints the full modifier chain and the graded outcome.

**Why here:** every other rule in the book — combat, resistance, experience, chases, Passions, Sanity — funnels through "roll d100 against an effective chance and grade the result." Get the grading and the modifier pipeline exactly right against the printed table, and the remaining layers are transcription. Get them wrong and every layer above inherits the error. This is also why the kernel is worth building before the scope filter is fully applied: the kernel is identical no matter which optional modules ship.

**Second milestone**, once M1 is green: Layers 1–2 plus the Monte-Carlo harness, and immediately run the review-note §5 question — *simulate 8–12 cases of experience checks and measure whether skills move enough for a player to feel it.* That answer changes the advancement design, so it should arrive before combat is written, not after.

---

## 6. Decisions required

**The live queue is GitHub Issues, not this list.** These are summarised for context only; each has an Issue, and the Issue is authoritative if they diverge.

Already resolved: the source-text question and the Prohibited Content constraint (ADR 0001), the sub-5% ambiguity (§2.4), and — locked in framework v0.2, not open — the roll-integrity model (**pre-seeded at scene entry**) and advancement (**tick-on-use**).

Still open:

1. **Skill Category Bonuses vs. Simpler Skill Bonuses** — mutually exclusive by the book's own statement, and this is the highest-priority call. It changes how *every* skill's effective rating is computed, so it must be settled before Layer 2 is written.
2. **Difficulty stacking** — do two Difficult conditions compound, or floor at one step?
3. **Modifier ordering** — confirm `Gate → Override → Additive → Multiplicative → Clamp`.
4. **Hit locations on or off** — the framework's persistent visible injuries argue for on; it meaningfully enlarges Layer 4. Decide before combat is written.
5. **Fate Points** — attractive for a video game, but built on the power-point economy the scope filter deletes. Needs re-basing on another currency or dropping.
6. **Acting Without Skill** — interacts directly with the framework's clue rule; the book warns it can strain plausibility.
7. ~~Roll-determinism policy~~ — **decided**: pre-seeded at scene entry (framework v0.2). The engine must implement this; it is no longer a question. ADR 0003's architecture supports it directly.
