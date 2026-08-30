# 0012. Layer 3 — Character, CharacterBuilder, and ExperienceSystem shape

## Status

Accepted — 2026-08-30. Resolves #40.

## Context

Layers 0–2 (`Brp.Core`'s abilities and skills) supply the pieces a whole
character needs, but nothing yet binds them into a `Character` aggregate, a
creation path, or the locked tick-on-use advancement rule
(`noir-rpg-framework.md` v0.2, `AGENTS.md`). `engine-implementation-plan.md`
§3 lists "power points" on the Layer 3 `Character`; that is superseded by
`orc-scope-filter.md`'s Chapter 4 cut (see ADR 0009) and is not built.

## Decision: `Character` aggregate

**Sourced:** Ch 2: Characters, "Derived Characteristics" (p.13) and "Hit
Points" (p.14).

`Brp.Rules.Characters.Character` binds an identity, a Layer 1 `AbilitySet`,
and a dictionary of `CharacterSkill` instances keyed by `SkillId`. It exposes
`CurrentHitPoints`/`MaximumHitPoints` as pass-through properties that read
`AbilitySet` live on every access rather than caching a value computed at
construction — `AbilitySet` already made HP a computed-on-read value (ADR
0008); `Character` does not re-introduce a cache on top of it. Verified by a
test that drops CON on an existing `Character` and observes
`MaximumHitPoints` fall, with `CurrentHitPoints` clamped alongside it.

`CharacterSkill` is Layer 2's "two-number contract"
(`Brp.Core.Skills.SkillRoll`) made concrete per character: `Definition`
supplies the printed base chance, `CurrentRating` is the character's own
number, set by `CharacterBuilder` and moved afterward only by
`ExperienceSystem`. `HasExperienceCheck` is the per-skill experience flag Ch
5 p.138 requires ("the small box next to that skill").

`WoundTrack` and `EquipmentList` are structure only: an ordered list of
`Wound`/`EquipmentItem` records with add/remove, carrying no wound mechanics
or gear stats. Layer 4 (#21) owns First Aid-per-wound, Major Wounds, hit
locations, and weapon/armor data; this issue only guarantees `Character` has
somewhere to put them.

**Scope cut, enforced:** no spendable power-point pool, Fate Points, or PP
reservoir exists on `Character`. POW remains a characteristic (it still
drives the Luck roll, Ch 2 p.11, and POW-vs-POW resistance). A reflection
test (`ArchitectureTests.Character_carries_no_spendable_power_point_pool`)
guards against a future PR reintroducing one under a different name.

## Decision: `CharacterBuilder` — point-buy creation

**Sourced:** Ch 2: Characters, "Point-Based Character Creation (option)"
(pp.9-10) for characteristics; "Step Seven: Profession and Skills" (p.8) for
skill points; "Freeform Professions" checklist entry (p.229) for the
background-package mechanism.

### Characteristics

`CharacteristicPointBuy.Allocate` reproduces the printed option exactly:

| Parameter | Value | Source |
|---|---|---|
| Point pool | 24 | p.9, "You have 24 points to spend ... equivalent of the 'normal' power level" |
| Starting value | 10 (all seven) | p.9, "All characteristics ... begin at 10" |
| Cost: STR/CON/SIZ/CHA | 1/point, symmetric refund | p.9 |
| Cost: DEX/INT/POW | 3/point, symmetric refund | p.9 |
| Creation-time maximum | 21 (all seven, including INT/POW) | p.9, "No initial characteristic can be raised to higher than 21" |
| Floor | 3 general; SIZ/INT floor at 8 | p.9-10; already encoded in `ability-ruleset.json`'s per-characteristic `Minimum` (ADR 0008) |

The 21 creation-time ceiling is deliberately **not** the same value as
`AbilityRuleset.Characteristics[id].Maximum`: that property is `null` for
INT and POW because Ch 2 p.10 says mental characteristics "can usually be
raised without limits" **during play**. `CharacterCreationRuleset
.CharacteristicCreationMaximum` is a separate, creation-time-only cap, taken
as `min(21, AbilityRuleset maximum)` per characteristic
(`CharacteristicPointBuy.ValidateBounds`) — the two numbers do different
jobs and neither one absorbs the other.

**House rule — the ±3 shift.** After point-buy allocation,
`CharacteristicPointBuy.ApplyShift` lets up to
`CharacterCreationRuleset.FreeShiftPoints` (3) points move between
characteristics, zero-sum. **This is not printed for the point-buy option.**
Ch 2 p.8's "redistribute up to 3 points between your characteristics" belongs
to the dice-rolled Step One (`3D6`/`2D6+6`), which point-buy replaces
outright ("the following adjustments are made to Step One" — full
substitution, no redistribution clause carried over). NoiRPG extends the
same small fine-tuning allowance to the point-buy path so a build is not
locked to whole-point-cost increments at the margins. It is bounded,
optional (0 disables it via ruleset data), and it can never move a
characteristic outside its bounds. If this combination proves confusing at
the table or in tooling, it can be turned off by setting
`freeShiftPoints: 0` in `character-creation-ruleset.json` without touching
code.

EDU is out of scope for the point-buy allocator: Ch 2 p.9 has the
gamemaster assign it "based on your character's age and background," which
is a game-layer decision this issue does not make. `CharacterBuilder`
accepts an optional `Education` value on the creation request, defaulting to
the ruleset's starting value (10) — the same neutral fixture ADR 0006
already used for the audited background packages.

### Skills

| Parameter | Value | Source |
|---|---|---|
| Professional skill points (Normal power level) | 250 | p.8, "Allot 250 points to professional skills" |
| Personal skill points (RAW) | INT×10 | p.8, "multiply your character's INT×10" |
| Personal skill points (Increased option) | INT×15 | p.8, "Increased Personal Skill Points (Option)" |
| Starting soft cap | 75% | p.8, "No skill should begin higher than 75%" |

**House rule — which "Increased" tier NoiRPG uses.** The book's Increased
Personal Skill Points option states INT×15/20/25 for heroic/epic/superhuman
campaigns and is silent on a Normal-power-level increase; Normal only has
the plain INT×10 baseline. NoiRPG runs at Normal power level throughout (250
professional points, 75% cap — never adopting heroic's 325/90%) and borrows
only the heroic tier's INT×15 personal multiplier, because the Noir entry
recommends the option specifically so "characters are professionals with
prior experience" (`orc-scope-filter.md`), which is a personal-skill-pool
concern, not a campaign-power-level one. `CharacterCreationRuleset
.IncreasedPersonalSkillPointsIntMultiplier` documents this explicitly so a
future reader does not mistake it for a plain transcription of the printed
heroic row.

The 75% cap is enforced per skill as `max(cap, printedBase)` — reproducing
p.8's own carve-out: "If a combination of bonuses increases the skill to
more than 75% before this step, do not add any additional skill points."
`CharacterBuilder` throws rather than silently clamping, so a caller
reallocates the points instead of losing them.

### Background packages (Freeform Professions)

`BackgroundPackage` is a name plus a professional skill-point allocation,
applied through the same professional-skill-point mechanic every printed
profession uses (Ch 2, "Professions A Through Z," p.17 onward — "Your
character will spend their professional skill points on these skills"). The
book's own text for Freeform Professions is one checklist line ("Useful for
customized, difficult-to-categorize player characters," p.229) with no
further mechanic given, so reusing the ordinary professional-points path
rather than inventing a new one is the natural reading. `background-packages
.json` ships exactly one placeholder fixture package, explicitly labeled as
a test fixture, not a Layer 5 noir package (ex-cop, ex-journalist, and so on
are #40's stated follow-on work).

## Decision: `ExperienceSystem` — tick-on-use, RAW toggle

**Sourced:** Ch 5: System, "Skill Improvement," "Making an Experience Roll,"
"Increasing Skills by Experience," and "Training and Study" (pp.138-140).

- **Gate (mechanical, both policies):** `CheckStakes.Easy` or `.NoStakes`
  never records a tick — Ch 5 p.138, "If a skill roll was Easy, no
  experience check is allowed," extended mechanically (there is no
  gamemaster) to the "nothing at stake" exemption from the same page.
- **Once per case:** `CaseExperienceLedger` refuses a second tick for the
  same skill within one case — Ch 5 p.138, "made only once per adventure, no
  matter how many times the skill is successfully used."
- **House rule — `ExperiencePolicy.TickOnUse` (the default):** a real-stakes
  check ticks whether it succeeded or failed. This is the locked deviation
  from BRP RAW (`noir-rpg-framework.md` v0.2, `AGENTS.md`), justified by
  `tools/advancement_sim.py`'s 10,000-character simulation showing RAW ticks
  are "nearly invisible at video-game length" and "starve low skills."
- **Sourced — `ExperiencePolicy.RawTickOnSuccess`:** the toggle that
  reproduces Ch 5 p.138 exactly ("If a skill is used successfully, you
  almost always get an experience check"). This is the falsification target
  `rules-conformance` should check: flipping the policy and nothing else
  must make every previously-ticking failed check stop ticking, and change
  nothing else about the gate or the improvement roll.
- **Improvement roll:** `ExperienceSystem.ImprovementRoll` draws a d100 via
  the injected `IEntropySource`, adds an optional `experienceBonus`, and
  compares against a success threshold: `roll > rating` grants `+1D6` (Ch 5
  p.138-139) below 100%. **Corrected 2026-08-30 (post-implementation
  conformance review):** at or above 100%, the threshold is pinned at 100
  rather than at the (possibly much higher) current rating — Ch 5 p.138,
  "Exceeding 100% in a Skill": "No matter how much over 100% the skill has
  risen, any roll of 100 or over earns a skill improvement." The original
  implementation used `roll <= rating` unconditionally, which meant a skill
  at or above 100% could **never** improve (the maximum possible roll of 100
  always satisfies `100 <= rating` once `rating >= 100`) — a real defect,
  since NoiRPG does not cut skills at 100% and tick-on-use can carry a
  primary skill past it over a campaign. Fixed by comparing against
  `min(rating, 100)`, with `roll >= 100` at that cap instead of `roll >
  100` (an unmodified d100 tops out at 100, so "greater than" would make
  the capped threshold unreachable without the experience bonus, which
  contradicts the book's own "any roll of 100 or over" clause).
  `tools/advancement_sim.py`'s `Skill.improvement_roll` was updated with the
  identical cap in the same change, so the two stay reconciled — see that
  file's docstring and inline comment.
  The experience check clears either way. **The experience bonus (½INT
  rounded up, `AbilitySet.ExperienceBonus`, already built in Layer 1)
  defaults to 0** rather than being applied unconditionally. Ch 5 p.138
  prints it as part of the roll ("added to the die roll ... just to the
  roll to see if there is improvement"), and passing
  `character.Abilities.ExperienceBonus` now reproduces Ch 5's printed rule
  exactly, including the 100%-and-above case — the default of 0 exists only
  so the implementation's simplest call matches `tools/advancement_sim.py`'s
  deliberately simplified model, which omits the bonus term. This is a
  reconciliation with the simulation's scope, not a claim that the book has
  no such bonus.
- **Teaching:** `ExperienceSystem.Teach` now resolves the teacher's Teach
  roll through the same five-grade `Brp.Core.Resolution.SkillResolver` every
  other skill roll uses, rather than a bespoke percent-threshold check, so
  its fumble band matches the book's general fumble rule exactly. A Success
  (or better — Ch 5 p.138 does not distinguish Special/Critical for
  teaching) grants `+1D6`, capped so training alone cannot carry the skill
  past a training cap (75% per Ch 5 p.139: "No skill can be trained above
  75%, no matter how good the instructor. Any increase above this must come
  through successful use of the skill"). **Corrected 2026-08-30:** the
  original implementation applied the die roll uncapped, which could train a
  skill past 75% — a real defect, since that cap is the book's explicit
  floor on what teaching alone can do. A plain Failure grants nothing. A
  **fumble now applies the book's printed penalty**: `CharacterSkill.Degrade`
  reduces the rating by `1D3`, floored at zero (Ch 5 p.138: "a fumble is
  counterproductive, with the teacher causing self-doubt and contradicting
  your character's prior learnings, reducing the skill by -1D3"). This was
  previously left unmodeled with an incorrect scope note calling it "a
  combat/injury effect, Layer 4" — a teaching fumble is skill *degradation*,
  not a physical injury, and there was no genuine scope reason to defer it.
  `Teach` returns the signed change actually applied (positive for a capped
  gain, negative for a floored fumble penalty, zero for a plain failure),
  not the raw uncapped die roll, so a caller reading the return value never
  sees a number larger than what the skill's rating actually moved by.
- **Corrected 2026-08-30 (second conformance pass): the training cap is now
  ruleset data, not a bare constant.** The first fix for the missing 75%
  cap (above) initially landed it as a `Teach` method-parameter default
  (`int trainingCapPercent = 75`) — itself a nick against invariant 7 (rules
  values are data, not constants), just one step removed from the defect it
  fixed. It now lives on a new `Brp.Rules.Advancement.ExperienceRuleset`
  (one field, `TrainingCapPercent`), loaded from `Brp.Data`'s
  `experience-ruleset.json` via `NoirExperienceRuleset.Load()`, following
  the exact loader/JSON/validation pattern
  `NoirCharacterCreationRuleset`/`character-creation-ruleset.json` already
  established. `Teach` now takes an `ExperienceRuleset` as a required
  parameter (no in-code default), so a caller must supply the sourced
  value rather than silently getting one baked into the method signature.
  **This is deliberately a separate ruleset field from
  `Creation.CharacterCreationRuleset.StartingSkillCapPercent`, not a shared
  read of the same number**, even though the book prints 75% for both at
  Normal power level: one gates what a skill may start at during character
  creation (Ch 2 p.8), the other gates what teaching alone may raise an
  *existing* skill to during play (Ch 5 p.139) — two different rules the
  book happens to pin at the same value, not one rule with two names. A test
  proves the value is genuinely read from data by constructing an
  `ExperienceRuleset` with a non-default cap (40) and confirming `Teach`
  clamps to it instead of 75.

Policy names (`TickOnUse`, `RawTickOnSuccess`), the mechanical gate
(`CheckStakes`), and the improvement rule (`d100 > rating → +1D6`, with the
100%-and-above threshold correction above) are written to match
`tools/advancement_sim.py`'s two simulated variants ("B tick on use" / "A RAW
(tick on success)") so the simulation remains a valid sanity check against
this implementation, per the issue's explicit requirement. The
rules-conformance falsification target — flipping `ExperiencePolicy` and
nothing else must reproduce BRP's tick-on-success behavior exactly, with no
other divergence from the book's experience rule — is unaffected by either
correction: both defects lived inside "no other divergence," not in the
gate itself, and both are now closed.

## Alternatives considered

**Baking HP at construction time.** Rejected outright — Ch 2 p.13 requires
derived values to change immediately, and `AbilitySet` (Layer 1) already
solved this; re-caching it in `Character` would silently undo that work.

**Applying the printed heroic power level wholesale (325 points, 90% cap)
instead of borrowing only its personal-points multiplier.** Rejected: the
framework and scope filter want a Normal-power-level game with one
professional-competence knob turned up, not a heroic-tier campaign; adopting
the whole tier would also raise combat lethality assumptions the framework
explicitly rejects ("danger never retires").

**Applying the RAW experience bonus unconditionally in `ImprovementRoll`.**
Considered for exactness to Ch 5 p.138, but rejected as the default because
it would silently diverge from `tools/advancement_sim.py`'s model that the
issue requires this system to reconcile with. Solved instead by making the
bonus an explicit opt-in parameter, documented on both sides.

## Consequences

- `Brp.Rules` is the third tenant of `Brp.Data`'s loader pattern
  (`NoirCharacterCreationRuleset`, `NoirBackgroundPackageRuleset`, and now
  `NoirExperienceRuleset`), mirroring `NoirAbilityRuleset`/`NoirSkillRuleset`.
  `Brp.Data` now references `Brp.Rules` in addition to `Brp.Core`.
- Layer 5 can author the real noir background packages
  (`background-packages.json`) and the Composure/Vices/Passions layer
  directly against `Character`, `CharacterSkill`, and `BackgroundPackage`
  without further Layer 3 changes.
- Layer 4 (#21) will add wound and equipment mechanics that populate
  `WoundTrack`/`EquipmentList` — no rework of `Character` should be needed,
  only additions.
- **Known limitation:** weapon-derived skills (Firearms, Melee Weapon
  variants) resolve to a printed base of 0% in `CharacterBuilder` because
  their real base chance needs weapon data that does not exist until Layer
  4 (`Brp.Core.Skills.WeaponDerivedBaseChance` already documents this gap).
  A character built today can still be assigned professional/personal
  points on those skills, but the resulting rating undercounts what the
  book would show once a weapon is chosen. This is not a defect introduced
  here; it is the same gap Layer 2 already recorded, now visible at
  character-build time.
- **Post-implementation correction (2026-08-30, rules-conformance audit):**
  the initial implementation had two real defects, both inside the
  falsification target's "no other divergence from the book" zone rather
  than in the gate: the improvement roll's `>=100%` threshold (skills at or
  above 100% could never improve) and the Teach roll's missing 75% training
  cap (training could push a skill past it). Both are fixed and covered by
  tests (see the "Decision: `ExperienceSystem`" section above); a Teach
  fumble now applies the printed -1D3 skill degradation instead of being
  left unmodeled under an inaccurate Layer-4-injury scope note.
- **Second post-implementation correction (2026-08-30, re-audit):** the
  training-cap fix above initially landed as a hardcoded method-parameter
  default rather than ruleset data — a smaller instance of the same
  invariant-7 gap this ADR otherwise documents at length. Moved to
  `ExperienceRuleset`/`experience-ruleset.json`, with a test proving a
  non-default cap value changes `Teach`'s clamp. See the data-sourcing note
  under "Teaching" above.
