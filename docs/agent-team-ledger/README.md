# Agent-team ledger

Research instrument: per-job token/effort telemetry for the multi-agent workflow that builds this
repository, so spend can be attributed to **work done** and compared across layers. Feeds research on
AI agent teams operating against GitHub.

## Unit of record

One row in [`jobs.csv`](jobs.csv) = one **agent job** (a single dispatch of a specialized subagent).
The orchestrator (main Claude Code loop) coordinates but is not itself a row; its coordination tokens
are out of band and comparatively small. Numbers come from each subagent's completion telemetry
(`subagent_tokens`, `tool_uses`, `duration_ms`) — actuals, not estimates.

## Categories (`phase`)

- **build** — first-pass value creation (implementing the specified work).
- **verify** — quality tax: read-only adversarial audits (scope-warden, rules-conformance, etc.).
  "Waste" only if it finds nothing; its ROI is the defects it surfaces.
- **rework** — defect tax: fixing what the build got wrong, plus corrections raised by verify.

The build/verify/rework split is the core lens: it separates value from the two taxes and exposes
where first-pass correctness leaks (usually rework, not scaffolding).

## Columns

| column | meaning |
|---|---|
| `layer` / `issue` / `pr` | GitHub linkage (implementation-plan layer §3, issue #, PR #) |
| `seq` | order of the job within the layer |
| `phase` | build \| verify \| rework |
| `agent_type` | the specialized subagent (engine-dev, scope-warden, rules-conformance, …) |
| `role` | what it did (implement, scope-gate, conformance-audit, fix-defects, re-audit, …) |
| `tokens` | subagent output tokens (actual) |
| `tool_uses` | tool calls made by the subagent |
| `duration_ms` | wall-clock for the job |
| `commit` | commit(s) the job produced (blank for read-only audits) |
| `defects_found` / `defects_fixed` | defect flow |
| `tests_after` | full suite passing count after the job |
| `outcome` | one-line result |

Per-layer rollups (build/verify/rework shares, rework ratio) live in [`layers.md`](layers.md).

## Method notes / caveats for analysis

- Orchestrator tokens excluded — cross-layer comparisons are agent-job spend only.
- A verify job's value is realized in the *next* rework job it triggers; pair them when computing ROI.
- Token cost of a change is weakly correlated with its diff size: a one-field data change (Layer 3
  cap-wiring) cost more than the original build, because the agent re-derived context, added tests,
  updated the ADR, and ran the full build/test cycle. Semantic size ≠ token size.
