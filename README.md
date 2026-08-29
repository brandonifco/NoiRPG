# NoiRPG

A modern-day noir action/mystery RPG — no magic, no unrealistic technology, presented entirely in black and white. Built on Chaosium's *Basic Roleplaying: Universal Game Engine* under the ORC license, with deduction as the core gameplay: case boards, interrogations, and percentile skill checks where the roll shapes consequences but never stalls the mystery.

## Design Pillars

1. **Fun first, realism close behind** — believable systems that never stall play.
2. **The player's mind is the content** — deduction and decision-making carry the game, not rendered spectacle.
3. **Danger never retires** — a revolver is as lethal in the final chapter as in the first; hit points don't grow.
4. **Failure is a branch, not a wall** — losing produces consequences and new story, not game-over screens.

## Documents

| Document | Purpose |
|---|---|
| [noir-rpg-framework.md](noir-rpg-framework.md) | The design framework (v0.1) — system foundation, resolution mechanics, advancement, investigation rules, art direction, and scope budget. |
| [design-review-notes.md](design-review-notes.md) | Critical review of the framework — open design risks and recommended next steps. |
| [orc-scope-filter.md](orc-scope-filter.md) | What we implement from the ORC Content Document and what we cut. ~60% of the book is out of scope. Read before any engine work. |
| [engine-implementation-plan.md](engine-implementation-plan.md) | Dependency-ordered plan for the C#/.NET rules engine — resolution kernel, architecture decisions, build layers, first milestone. |
| [AGENTS.md](AGENTS.md) | Operating contract for coding agents — source-of-truth order, invariants, work protocol. Read this first. |
| [docs/decisions/](docs/decisions/) | Architecture decision records. |

## How work is tracked

GitHub Issues are the only work queue. One concern, one Issue, one branch, one pull
request. Durable decisions live in `docs/decisions/`; current and future work lives
in Issues. Neither is duplicated in this README.

## Status

Early design phase. Decisions made so far: modern-day era, player-created protagonist via point-buy with background packages, open city structured as map nodes with multiple concurrent intersecting cases. Platform and engine undecided.

## Licensing

The mechanical skeleton adapts ORC-licensed material from *Basic Roleplaying: Universal Game Engine* (Chaosium, 2023), from the freely published ORC Content Document. Setting, story, characters, writing, art, and code are reserved material. An ORC Notice and a "Powered by BRP" credit will be included as the project formalizes.
