# 0011. SkillDefinition: base-chance shape, specialties, and the skill registry

## Status

Accepted — 2026-08-29. Advances #35.

## Context

Layer 1 gives the engine a way to name a characteristic and evaluate it against ruleset
data (`AbilitySet`, `AbilityRuleset`). Nothing yet names a *skill*: `SkillResolver`
documents its first parameter as "the skill's unmodified base chance" but has no source
for that number, and `tools/Brp.Cli` fakes it with a `--base-chance` flag (#27's open
question). Ch 3: Skills, "Base Chances" (p.31) — **sourced** — states every skill has a
base chance, and that base chances take four distinct printed shapes across the
chapter's individual skill entries: a flat percentage (`Spot 25%`), a formula over one or
more characteristics (`Dodge DEX×2`, `Gaming INT+POW`), an either/or pair (`Drive 20% or
01%`), and a value the skill description explicitly declines to state (`Firearm: as per
weapon specialty`). A single numeric field cannot represent all four; this record is
about the type that does.

## Decision

### Base chance is a small closed expression hierarchy, not a formula string

`Brp.Core.Skills.BaseChanceExpression` is an abstract record with one method,
`Percent Evaluate(AbilitySet abilities)`, and four sealed implementations:

| Type | Shape | Example — **sourced** |
|---|---|---|
| `ConstantBaseChance` | flat `Percent` | `Spot`, Ch 3 p.50, "Base Chance: 25%" |
| `CharacteristicFormulaBaseChance` | a list of `(characteristic, multiplier)` terms, summed | `Dodge`, Ch 3 p.37, "DEX×2"; `Gaming`, Ch 3 p.40, "INT+POW%" |
| `EraConditionalBaseChance` | a `Modern` / `Historical` pair, always evaluates to `Modern` | `Drive`, Ch 3 p.37, "20% or 01%" |
| `WeaponDerivedBaseChance` | no value; `Evaluate` throws | `Firearm`, Ch 3 p.39, "As per weapon specialty" |

**Why a closed type hierarchy over a formula parser or a `Func<AbilitySet, Percent>`
delegate:** ADR 0002/AGENTS.md invariant 7 requires rules values to live in ruleset data,
not code. A closed hierarchy is exactly what a JSON discriminated union
(`{"type": "constant", ...}`) deserializes into without an expression parser, while a
delegate cannot be represented in data at all. It also keeps every shape individually
testable and matches `RoundingMode`'s existing style of "small named alternatives, not an
open-ended calculator."

**Formula terms are summed multiples, and that is deliberately all they are.** Every
in-scope skill's formula (`DEX×2`, `INT×5`, `INT+POW`) is a sum of straight multiplies —
none divide. `Rounding`/`RoundingMode` therefore has no role in this issue; it becomes
relevant only for an out-of-scope skill like `Fly` (½ DEX), which is not implemented
here.

### The era-conditional shape always evaluates to the modern side — house rule, generalizing a sourced project policy

AGENTS.md invariant 4 and `orc-scope-filter.md`'s "Modern noir" section — **sourced** as
the project's standing policy, not new here — require NoiRPG to always take the modern
value where the book prints an either/or pair. `EraConditionalBaseChance.Evaluate` always
returns `Modern`; `Historical` is retained on the type for provenance and is never
evaluated.

Applying this to `Drive` needed a judgment call, recorded here as a **house rule**: Ch 3's
own axis for `Drive` is vehicle familiarity ("common vehicles... 20%, unknown/uncommon
vehicles... 01%"), not literally historical-versus-modern. A car is the modern-era common
case, so `Drive`'s 20% is read as the "modern" side of the pair and encoded as `Modern`.
This is the only in-scope skill with a printed either/or base-chance pair verified
against the PDF; no other in-scope skill (`First Aid`, `Knowledge`, `Medicine`) carries
one in this document; see "Corrections found against the PDF" below.

### Corrections found against the PDF

`orc-scope-filter.md`'s era table lists `First Aid 30% | INT×1`, `Knowledge (any) 05% |
01%`, and `Medicine 05% | 00%` as era pairs. **Verified against the PDF** (Ch 3 entries
for `First Aid` p.39, `Knowledge (various)` p.42, `Medicine` p.46): none of these three
carry a second printed value in this document. `First Aid` and `Medicine` are each a
single flat percentage; `Knowledge (various)` prints `05% or 00%`, but that pair is
gated on whether the *specialty* is common or requires dedicated study, not on era — Ch 3
p.42: "The gamemaster should determine whether the Knowledge skill has a base chance of
05% for specialties that are common, or 00% for those requiring research and study."
AGENTS.md's document-authority order puts the PDF above the scope filter, and a conflict
between them "is a bug — file an Issue" (AGENTS.md, "Source-of-truth documents"). Rather
than encode a pair this document does not print, `First Aid` and `Medicine` are
`ConstantBaseChance`, and every in-scope `Knowledge` specialty is authored at the common
value (`05%`), since the framework's chosen specialties — Law, Streetwise, Accounting,
Group, Region, Politics — are all common ones. This divergence from the scope filter's
era table should be corrected there in a follow-up documentation Issue; it is not
re-litigated as part of #35's scope.

### Specialty: a `SkillDefinition` with a `Parent`, not a separate root type

Ch 3, "Knowledge Specialties" (p.43) — **sourced** — instructs writing specialties as
`Knowledge (Law)`, and the framework's experience rule ("two specialties are two checks")
requires each to be independently resolvable. `Specialty.Create(parent, id, name,
baseChance, bookEquivalent)` returns an ordinary `SkillDefinition` whose `Parent`
property is the shared parent instance and whose `Category` is inherited from it; its
`Id`, `Name`, and `BaseChance` are its own. Two specialties of one parent (`Knowledge
(Law)` and `Knowledge (Streetwise)`) are therefore two distinct `SkillDefinition`
instances with `object.ReferenceEquals(a.Parent, b.Parent)` true and everything else
independent — exactly the "distinct instance sharing a parent definition" shape the
Issue asks for, without a second type whose only job would be to duplicate
`SkillDefinition`'s fields.

A skill entry with specialties (e.g. `Knowledge`, `Science`, `Firearm`) is not itself
independently resolvable in the shipped data — only its specialties are registered in
`SkillRegistry`. This mirrors the book's own distinction between a skill *category*
description ("Knowledge (various)") and an actual rollable skill ("Knowledge (Law)").

### `SkillDefinition` carries a `SkillCategory`

`docs/decisions/0006-skill-bonus-system.md` (Accepted) states as part of its own
consequences: "Layer 2 is unblocked. `SkillDefinition` must carry a category." This
record honors that: `SkillCategory` is Ch 3's six printed categories (p.31), and every
skill in `skill-ruleset.json` carries one, verified against the "Skill List by Category"
table (p.32) for the in-scope skills. **Computing the category bonus itself is out of
scope for #35** — ADR 0006 records the bonus formula and `tools/skill_bonus.py`
prototypes it, but no C# code applies it yet. `SkillDefinition.BaseChanceFor` returns the
printed base chance only; combining it with a category bonus into an effective rating is
later work, consistent with the Issue's "do not re-solve #27" instruction.

### Canonical naming: framework name is the key, book name is `BookEquivalent`

`orc-scope-filter.md`, "Skill naming: the framework's names win" — **sourced** as
existing project policy — requires the registry to be keyed by the framework's 18-name
list where it renames a book skill, and to map inward to the book skill otherwise.
`SkillDefinition.BookEquivalent` records the book's own name (`Streetwise` →
`Knowledge (Streetwise)`, `Shadow` → `Stealth`, `Locksmith` → `Fine Manipulation`,
`Accounting` → `Knowledge (Accounting)`, `Photography` → `Art (Photography)`, `Law` →
`Knowledge (Law)`); for every other skill it equals `Name`. `Intimidate` is loaded as a
house-rule entry with `BookEquivalent = "Intimidate (no book equivalent)"`, per
`docs/decisions/0006-skill-bonus-system.md`'s prior finding that it has none; its base
chance (`05%`, Communication-category typical) is a **house rule** invented for this
record, since the book supplies no value to transcribe. This is the one number in
`skill-ruleset.json` not backed by a page citation, and is flagged here for the project
owner to confirm or replace.

**Open conflict found, not resolved here: is `Shadow` the same skill as `Stealth`, or
two distinct skills?** `orc-scope-filter.md`'s naming table states `Shadow (tailing) →
Stealth`, read here as an identity rename (one skill, two names). But `Stealth` is
*also* separately listed in `orc-scope-filter.md`'s own Ch 3 IN enumeration ("...Sense,
Sleight of Hand, Spot, Status, Stealth, Strategy..."), in `noir-rpg-framework.md`'s
working skill list ("Streetwise, Shadow (tailing), ... Stealth, Spot"), and in
`tools/case_validator.py`'s locked `SKILLS` set — all three list `Shadow` and `Stealth`
as two separate entries, not one skill under two names. AGENTS.md's locked-decisions
section ties "the canonical skill list" specifically to `case_validator.py`'s hardcoded
set and says not to diverge from it. This record does not resolve which reading is
correct — that is a framework-design question, not an engine-structure one — and
instead takes the non-destructive path: `Stealth` is loaded as its own top-level entry
(`Constant(10%)`, Ch 3 p.52, same value `Shadow` already carries) *in addition to*
`Shadow`, so neither locked source is contradicted by omission. Whether `Shadow` and
`Stealth` should in fact resolve identically, or diverge once the game defines what
"tailing" adds mechanically, is left for the project owner.

`tools/case_validator.py`'s `SKILLS` set also uses a single flat `Firearms` label,
where this record's data splits `Firearm` into `Handgun`/`Shotgun`/`Rifle` specialties
per `orc-scope-filter.md`'s explicit Ch 3 filter text. This is judged **not** a
conflict of the same kind: `case_validator.py`'s set omits several other in-scope
skills entirely (`Grapple`, `Melee Weapon`, `Martial Arts`, every `Knowledge`
specialty beyond the framework's renamed four, `Research`'s siblings, etc.), which
confirms it is a narrower, tool-specific label set for clue-door validation rather than
a claim about the full character skill list this record builds. No change made for
this one.

### Registry and resolution routing

`SkillRegistry` is a flat, data-loaded `SkillId → SkillDefinition` lookup, loaded by
`Brp.Data.NoirSkillRuleset.Load()` from an embedded `skill-ruleset.json`, mirroring
`NoirAbilityRuleset`'s pattern exactly (recursive JSON DTOs, `PropertyNameCaseInsensitive`,
manifest-resource stream). `Brp.Core.Skills.SkillRoll.Resolve` composes a
`SkillDefinition`'s base chance with a caller-supplied `AbilitySet` and effective chance,
then calls `SkillResolver.Resolve` unchanged — this is the home #27 was missing. It does
not touch `ModifierChain` or `tools/Brp.Cli`; unwinding the CLI's `--base-chance`
workaround and wiring the modifier pipeline through this path is left to #27.

## Alternatives considered

**A formula mini-language parsed from a string** (`"DEX*2"`, `"20|1"`). Rejected: it
reintroduces a parser and a grammar for four fixed shapes, trades compile-time
exhaustiveness (a `switch` over the closed hierarchy) for runtime string matching, and
does not obviously improve on the JSON discriminated union `NoirAbilityRuleset` already
uses for `DiceExpression` notation.

**A single `Percent? Constant` plus nullable formula fields on `SkillDefinition` itself.**
Rejected: it does not compose (an era-conditional pair whose modern side is itself a
formula, which the book's `Fly` entry needs even though it is out of scope) and it makes
"which shape is this skill" an implicit null-check pattern instead of a type.

**A separate `Specialty` class wrapping a `SkillDefinition`.** Rejected: it would
duplicate every field `SkillDefinition` already has (id, name, category, base chance) for
no behavioral difference — a specialty is not resolved differently from any other skill,
it only carries provenance back to a shared parent. `Specialty.Create` is a factory, not
a type, because there is nothing a specialty can do that a `SkillDefinition` cannot.

## Consequences

- `Brp.Core.Skills` has no game-engine or `Brp.Data` dependency, consistent with
  AGENTS.md invariant 6; `Brp.Data.NoirSkillRuleset` is the only place JSON is parsed.
- The falsification target named in #35 (Science/Strategy/Martial Arts at 01% base) is
  covered by a named pinning test through `SkillRoll`, not just bare `SkillResolver`
  numbers, so a future change that accidentally keys the floor on the effective rating
  instead of the base chance is caught at the Layer 2 boundary, not only at the kernel.
- `orc-scope-filter.md`'s era table for `First Aid`, `Knowledge`, and `Medicine` does not
  match this printed document and should be corrected in a follow-up documentation Issue;
  this record does not fix `orc-scope-filter.md` itself, only avoids propagating its error
  into ruleset data.
- `Intimidate`'s base chance (05%) is an unreviewed house-rule number and should be
  confirmed or replaced before it is used in character creation or balance work.
- Category bonuses (ADR 0006) are not yet computed anywhere in `Brp.Core`; `SkillDefinition`
  carries the field ADR 0006 asked for, but effective-rating composition remains open,
  tracked against #27 and Layer 3.
- `SkillDefinition`'s `Parent` reference makes the type a small object graph rather than a
  flat value; equality/serialization of `SkillDefinition` beyond what tests need
  (reference identity for `Parent`) is not addressed here.
