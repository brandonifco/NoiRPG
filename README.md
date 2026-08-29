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
| [AGENTS.md](AGENTS.md) | Operating contract for coding agents — source-of-truth order, invariants, work protocol. Read this first. |
| [noir-rpg-framework.md](noir-rpg-framework.md) | The design framework (v0.2) — system foundation, resolution mechanics, advancement, investigation rules, art direction, and scope budget. |
| [design-review-notes.md](design-review-notes.md) | Critical review of framework v0.1 — open design risks and recommended next steps. |
| [development-plan.md](development-plan.md) | Risk-sequenced development plan — phases, gates, and risk register. |
| [orc-scope-filter.md](orc-scope-filter.md) | What we implement from the ORC Content Document and what we cut. ~60% of the book is out of scope. |
| [engine-implementation-plan.md](engine-implementation-plan.md) | C#/.NET rules engine plan — resolution kernel, architecture decisions, build layers. |
| [interrogation-design.md](interrogation-design.md) | Design doc for the interrogation minigame, with paper-prototype rules and go/no-go criteria. |
| [tools/advancement_sim.py](tools/advancement_sim.py) | Advancement math simulation — validates that skills move perceptibly at video-game length. |
| [case-board-test.md](case-board-test.md) | Paper test kit for the case-board deduction loop, with the machine-run Three Doors build audit results. |
| [cases/SCHEMA.md](cases/SCHEMA.md) | Case data schema v0.1 — the production case format. |
| [cases/overpass.yaml](cases/overpass.yaml) | Case 01 "The Overpass" — the first case authored as pure data. |
| [tools/case_validator.py](tools/case_validator.py) | Case validator — enforces the Three Doors and junction-point rules, audits build coverage. |
| [interrogation-cards-overpass.md](interrogation-cards-overpass.md) | Statement card decks for all four Overpass suspects — the interrogation paper-prototype materials. |
| [paper-kit/overpass-cards.html](paper-kit/overpass-cards.html) | Print-ready paper test kit — open in a browser and print: statement decks, suspect dossiers with trackers, evidence cards, reference cards, build sheets. |
| [docs/decisions/](docs/decisions/) | Architecture decision records. |
| [docs/agent-team.md](docs/agent-team.md) | Agent roster and routing rules — which model does what, and why. |

## How work is tracked

GitHub Issues are the only work queue. One concern, one Issue, one branch, one pull
request. Durable decisions live in `docs/decisions/`; current and future work lives
in Issues. Neither is duplicated here.

## Status

Early design phase (Phase 0 decisions locked; Phase 1 validation underway — see the development plan). Decisions made so far: modern-day era, player-created protagonist via point-buy with background packages, open city structured as map nodes with multiple concurrent intersecting cases, junction-only case decay, pre-seeded rolls, tick-on-use advancement, commercial release intent. Platform and engine undecided until the vertical slice.

## Licensing

The mechanical skeleton adapts ORC-licensed material from *Basic Roleplaying: Universal Game Engine* (Chaosium, 2023), via the freely published ORC Content Document committed to this repository. Setting, story, characters, writing, art, and code are reserved material. An ORC Notice and a "Powered by BRP" credit will be included as the project formalizes.
