# NNNN. Equipment Quality Modifiers and the Skills-and-Equipment mapping

## Status

Accepted — 2026-09-01. Resolves #228.

## Context

Ch 8: Equipment prints two short, adjacent sections that `orc-scope-filter.md`'s Ch 8 IN-list
(lines 130-131) both keep: "Equipment Quality Modifiers" (p.185) and "Skills and Equipment"
(pp.185-186). Issue #228 frames them as one coupled question -- "does your gear help this skill
roll?" -- and asks that the numeric half go through the existing `ModifierPipeline` (ADR 0007)
rather than a parallel path, and that the mapping half live as data in `Brp.Data`.

## Decision

Two primitives under `Brp.Rules.Gear`, plus the mapping that gates them together:

- **`EquipmentQuality.Modifier`** (sourced, p.185) -- "The quality of equipment can provide a
  modifier to a skill roll, as described in Situational Modifiers. This modifier can range from
  inferior equipment penalizing your character's skill rating by -20%, to superior quality
  equipment offering a +20% bonus." The book itself names this a *situational* modifier and even
  restates the Ch 5 ordering rule ("situational modifiers... are applied after an Easy modifier
  doubles it or Difficult divides it in half"), so it is built as an `AdditiveKind.Situational`
  `AdditiveModifier` and evaluated through `ModifierPipeline.Evaluate` exactly like every other
  situational source -- no bespoke arithmetic, no parallel path. `EquipmentQualityRuleset` loads
  the two deltas (-20/+20) from `equipment-quality-ruleset.json` (AGENTS.md invariant 7).
- **`SkillEquipmentRuleset`** (sourced, pp.185-186) -- the "Skills & Equipment" table: "The Skills
  & Equipment table describes potential specialized or general equipment to use with skills. If
  the skill is not listed, it does not require any equipment, or it is obvious (such as weapon
  skills)." Modelled as gear-to-skill data (`skill-equipment-ruleset.json`, loaded by
  `Brp.Data.NoirSkillEquipmentRuleset`), keyed by the engine's existing skill ids
  (`docs/decisions/0011-skill-definition.md`), not a new naming scheme.
- **`EquipmentQuality.ModifierForSkill`** -- the coupling point. Looks the skill up in
  `SkillEquipmentRuleset` first and throws if it is not listed, then delegates to
  `EquipmentQuality.Modifier`. This is what makes the two mechanics "coupled" rather than two
  independent, unrelated features: the mapping is the gate that says whether a quality modifier
  is meaningful for a given skill at all, matching the book's own framing that an unlisted skill's
  equipment "does not require... or it is obvious."

### Hand-picked subset -- sourced list, house selection

`orc-scope-filter.md`, Ch 8: "Do not transcribe it wholesale into ruleset JSON -- hand-pick the
entries a noir detective could plausibly encounter." The book's Skills & Equipment table lists
18 skill rows; `skill-equipment-ruleset.json` keeps the 16 whose skill has an engine equivalent
in `skill-ruleset.json` (Appraise, Art, Climb, Disguise, Locksmith [book: Fine Manipulation],
First Aid, Gaming, Knowledge, Language, Medicine, Navigate, Repair, Research, Science, Teach,
Technical Skill). The book's Craft and Literacy rows are cut along with those two skills, which
are not part of the engine's skill list at all (out of scope already, independent of this issue).

**Specialty expansion (house shape, not a book rule).** Five of those 16 book rows -- Art,
Knowledge, Repair, Science, Technical Skill -- name a skill that `SkillRegistry` does not itself
resolve: `NoirSkillRuleset.Load()` treats a parent entry with `specialties` as a category, not a
rollable skill, and registers only the leaves (`docs/decisions/0011-skill-definition.md`). Rather
than invent a synthetic parent id the rest of the engine has no roll for, `skill-equipment-ruleset.json`
records one link per resolvable specialty (Photography; Streetwise/Law/Accounting/Knowledge
(Group)/Knowledge (Region)/Knowledge (Politics); Repair (Electrical/Electronic/Mechanical);
Science (Chemistry/Forensics); Technical Skill (Computer Use/Electronics/Security Systems)),
each carrying the parent row's equipment text and citing which book row it came from. This keeps
`SkillEquipmentRuleset.UsesEquipment` meaningful against every id it is actually asked about --
the same ids `SkillRoll` and the rest of Layer 2/3 already use -- at the cost of 26 links instead
of a literal 16. Two equipment descriptions ("Navigate": astrolabe; "Medicine": herbalist
materials) also drop their pre-modern flavor text per AGENTS.md invariant 4 (modern era
baselines) -- a house wording choice, the underlying skill-equipment relevance itself is
unchanged from the book.

### Not implemented -- GM discretion, not an engine rule

p.185 also covers the case of *missing* required equipment: "your gamemaster may make the skill's
chance Difficult or Impossible, or simply rule that the skill cannot be attempted without the
right gear... Your gamemaster may allow your character a straight 1% chance." The book presents
three different possible outcomes, explicitly left to gamemaster judgment, with no single rule to
encode. This issue implements the two mechanics it names (the quality modifier and the mapping)
and does not invent a house rule for the missing-equipment case; a caller that wants to express
"no gear, no roll" already has `GateModifier` and `DifficultyModifier` available to compose that
decision itself, scene by scene.

## Consequences

- `EquipmentQuality.Modifier` returns a modifier for every tier, including a zero-delta one for
  `Average`, rather than `null` for "no bonus" -- matching the precedent of other zero-delta
  additive modifiers in this engine, so a caller never needs a conditional.
- `SkillEquipmentRuleset.UsesEquipment` returning `false` for an unlisted skill is the expected,
  non-error outcome (Brawl, Persuade, and most Combat/Communication skills carry no equipment
  row) -- not a defect to fix by inventing more rows.
- Neither primitive is wired into character creation, inventory, or a specific scene; per ADR
  0028's precedent for a comparable "general primitive" issue, wiring specific gear items to
  specific skill checks in play is Layer 5 content work, out of scope here.
- `EquipmentQuality.ModifierForSkill`'s gate is an engineering choice about how these two sourced
  mechanics compose, not itself a printed book rule -- the book states the mapping and the
  quality table as separate facts and never explicitly forbids applying a quality modifier to an
  unlisted skill's roll. Throwing here is the stricter, safer reading: it surfaces a caller
  mistake immediately rather than silently accepting a modifier the book gives no basis for.

## Known limitations

- The missing-equipment case (Difficult/Impossible/no-attempt) is deliberately not automated; see
  "Not implemented" above.
- `SkillEquipmentLink.PotentialEquipment` is free text for reference/GM use, not machine-parsed
  into a specific item catalogue -- there is no attempt here to say *which* weapon/armor
  definitions in `weapon-ruleset.json`/`armor-ruleset.json` satisfy a given skill's equipment
  need; the book's own table is prose-level guidance, not a lookup table of specific items.
