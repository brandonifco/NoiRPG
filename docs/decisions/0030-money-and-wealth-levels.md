# 0030. Money and Wealth levels: the modern-era Status table only, five ordinal levels, no economy sim

## Status

Accepted — 2026-09-01. Resolves #229.

## Context

Issue #229 (sub-issue of #116) asks for "the Money & Wealth abstraction (Destitute → Wealthy) as
a clean rules mechanic tied to the Status skill", citing `orc-scope-filter.md` Ch 8, line 129:
"a clean abstraction (Destitute → Wealthy) that suits a game where the PI's finances are a story
element, and it ties to the `Status` skill."

The book prints the five Wealth levels and their prose descriptions once, in Ch 2: Characters,
"Wealth" (p.19): Destitute, Poor, Average, Affluent, Wealthy. It then ties Status to Wealth in
Ch 3: Skills, "Status Skill, Social Status, & Character Wealth" (p.51), which prints **three**
era tables mapping a Status rating (01-00) to a Social Rank, a typical Wealth Rating, and a
Wealth Cap: Prehistoric, Ancient/Dark Age/Medieval/Imperial, and Victorian/Western/Pulp/Modern.
"Your gamemaster should revise these tables or create new ones, as desired" — the book treats the
choice of table as a setting decision.

NoiRPG's own era policy (AGENTS.md invariant 4: "Modern era baselines, not historical") already
resolves this choice project-wide, and is the same policy `EraConditionalBaseChance`
(`docs/decisions/0011-skill-definition.md`) encodes for skill base chances. A noir-era PI setting
is squarely the "Modern" column of the third table's own title
("Victorian/Western/Pulp/Modern").

## Decision

- Ship only the **Victorian/Western/Pulp/Modern Status** table (p.51) as
  `wealth-ruleset.json` / `NoirWealthRuleset.Load()`, rather than transcribing all three era
  tables and picking one at evaluation time. The Prehistoric and Ancient tables describe settings
  this project's era policy never selects, and carrying dead data for them would misrepresent
  what the engine actually uses (contrast `EraConditionalBaseChance`, which does carry both sides
  because a single `SkillDefinition` datum is reused verbatim from the printed pair; here the
  choice is a whole ruleset, so nothing is gained by shipping the untaken branches).
- `Brp.Rules.Wealth.WealthLevel` is a five-value enum (`Destitute` = 0 through `Wealthy` = 4) in
  the book's own ascending order, so callers can compare levels ordinally — the book itself
  relies on this ordering (Ch 8, "Charges or Limited-Use Equipment", p.190: a resource "two or
  three Wealth levels lower than the equipment's cost").
- `WealthTable.ForStatus(int)` mirrors the existing banded-lookup shape (`MajorWoundTable`,
  `IllnessSeverityTable`): a printed 00 reads as 100, matching the project's universal d100
  convention. `WealthRuleset` wraps the table with two convenience reads,
  `WealthLevelForStatus` (the "Wealth Rating" column) and `MaximumWealthForStatus` (the
  "Wealth Cap" column).
- Nothing here wires a `WealthLevel` onto `Brp.Rules.Characters.Character` or reads a specific
  character's Status rating automatically. Issue #229 asks for the mechanic, not for Layer 4
  character state; a caller (creation, or a future Ch 8 gear-affordability issue) looks up a
  Status rating against `WealthRuleset` itself.

## Consequences

- `Brp.Data.NoirWealthRuleset` loads the table from `wealth-ruleset.json` (AGENTS.md invariant 7:
  rules values are data, not constants), matching every other table-backed ruleset's loader shape.
- Money amounts, prices, starting cash, and purchasing power are explicitly out of scope — "the
  PI's finances are a story element," not a simulated economy. A future Ch 8 issue that needs
  actual currency (e.g. #228's gear↔skill mapping, or a starting-equipment budget) can consume
  `WealthLevel` as an input without this ADR needing to be revisited.
- The Prehistoric and Ancient/Dark Age/Medieval/Imperial Status tables are not modeled. If a
  future setting needs them, that is a new, explicit scope decision, not a silent extension of
  this one.

## Known limitations

- The book's alternate initial-wealth method (Ch 2, p.19: "begin your character at the lowest of
  the wealth ranges, adjusted upward for each successful Status roll you can make... after your
  character has been created") is not implemented; this ADR covers only the Status→Wealth lookup
  table itself, not a character-creation wealth-assignment procedure.
