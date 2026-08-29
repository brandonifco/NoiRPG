# Case Data Schema — v0.1
*First draft of the production case format (development-plan.md, Phase 1 item 4). `overpass.yaml` is the reference implementation; `tools/case_validator.py` enforces the rules below.*

A case is one YAML file: fiction and mechanics together, authorable by a writer, traversable by a machine. Cases-as-data is the framework's structural bet — this file defines what "a case" is.

## Top-level keys

| Key | Purpose |
|---|---|
| `schema_version` | Format version; the validator refuses versions it doesn't know. |
| `case` | Identity and fiction: `id`, `title`, `logline`, `victim`, `truth` (the designer's ground truth, never shown to the player). |
| `locations` | The map nodes this case unlocks or uses. Nodes, never streets. |
| `suspects` | Interrogable characters: `archetype`, `composure`, `stonewall`, `guilty_of`, `break_behavior` (per interrogation-design.md). |
| `evidence` | Everything pinnable on the board: `type` (document / photo / statement / object), art-bible `treatment`, optional `tag` (`texture`, `red-herring`, `falsifiable-alibi`). |
| `core_clues` | The case spine. Each clue has `doors` — see below. |
| `accusation` | The commit form: `solution`, `required_clues` (minimum solve), `full_clear_clues`, and authored `wrong_paths` (failure is a branch). |
| `junctions` | Cross-case reads. **Hard cap: 3** (junction-point rule). |
| `decay` | Junction-only decay hooks: `trigger` → `effect`, all authored states. |

## Doors (the Three Doors rule, enforced)

Every core clue needs **at least two `skill` doors with distinct skills, plus at least one `fallback` door**. `interrogation` doors are welcome extras (statements land on the record regardless of rolls) but don't count toward the minimum.

- `skill` door: `skill` (from the canonical 18-skill list), `min_rating` (working threshold **40** — at or above it the door always opens; the d100 grades `texture` only), `location`, `evidence_out`.
- `interrogation` door: `suspect`, `evidence_out` — guaranteed via the statement record.
- `fallback` door: `trigger` (an event, never a skill), `evidence_out`, `cost` (time, obligation, or decay — fallbacks are free of skill, never free of price).

## Authoring guidelines (validator warns, doesn't fail)

- **Door-skill coverage**: across a case's core clues, each background package's top three skills should open at least two doors. The `overpass` audits showed how a case can silently orphan a build: v1 added Research/Persuade doors for desk builds, v2 added Intimidate/Spot doors for the ex-soldier — see `case-board-test.md`.
- Every `evidence_out` must reference a declared evidence item; every evidence item must be reachable through some door or carry a `tag` explaining why it exists (red herrings and texture are legitimate, orphans are bugs).
- `wrong_paths` must reference real suspects: every authored dead end is still authored story.
