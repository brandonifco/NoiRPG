# Agent-team ledger

Research instrument: per-job telemetry for the multi-agent workflow that builds this repository, so
spend can be attributed to **work done** and compared across layers. Its schema is designed to feed
the empirical gaps identified in the peer review of `paper-kit/repository-as-engineer.md` — a
paper this repository is itself the case for. The paper's reviewer asked for exactly this
instrument: *"instrument every agent task with task, risk class, supplied context, R/A/H token
accounting, model, cost, agent role, review layer, findings, false positives, correction rounds,
human minutes, final merge SHA, deterministic rule created, repeated-error occurrence … even 30–100
instrumented tasks would transform C2."* This ledger is that log.

## Files

- [`jobs.csv`](jobs.csv) — one row per **agent job** (a single subagent dispatch).
- [`findings.csv`](findings.csv) — one row per **finding** a verification stage raised, with
  independent validation and primary-source location. Serves the paper's blocker #3 and its Table 4.
- [`human-minutes.csv`](human-minutes.csv) — one row per **merged unit**: the wall-clock human
  minutes and intervention count it cost. This is the headline optimization target and lives in no
  API — it must be logged by hand. Seeded header-only; **never add a fabricated row.**
- [`layers.md`](layers.md) — per-layer rollups: build/verify/rework shares plus the §6.5 net-value
  metric skeleton.

## Appending a row

Do not hand-edit the CSVs — one command keeps the columns aligned and the unmeasured fields set to
`NI` (never `0`):

```bash
tools/ledger-log.sh job   --layer 4 --issue 112 --pr 130 --seq 1 --phase build \
                          --agent-role engine-dev-implement --model sonnet --effort medium \
                          --tokens-total 210000 --tests-after 2260 --outcome "…"
tools/ledger-log.sh human --issue 112 --pr 130 --merge-sha <sha> --interventions 0 \
                          --note "one review pass, merged clean"
```

`ledger-log.sh human` refuses an all-`NI` row, so the file never fills with invented measurements.
Any field name is a `--kebab-case` flag of its column; omitted fields default to `NI`.

## Unit of record

One row in `jobs.csv` = one agent job. The orchestrator (main Claude Code loop) coordinates but is
not a row; its coordination tokens are out of band. Numbers come from each subagent's completion
telemetry (`subagent_tokens`, `tool_uses`, `duration_ms`) — actuals, not estimates.

## Category (`phase`) — the core build/verify/rework lens

- **build** — first-pass value creation.
- **verify** — read-only adversarial audit (scope-warden, rules-conformance). "Waste" only if it
  finds nothing; its ROI is the findings it surfaces (see `findings.csv`).
- **rework** — fixing what build got wrong, or an invariant/design nick a verify stage raised.

## How each column maps to the paper's data needs

| Column(s) | Serves | Notes |
|---|---|---|
| `layer` `issue` `pr` `commit` `merge_sha` | GitHub linkage, §8 reproducibility | `merge_sha` = PR's squash-merge SHA once merged |
| `date` | §8 date-of-execution | |
| `phase` | build/verify/rework lens (C1, §6.5) | |
| `agent_role` | reviewer's "agent role" + "review layer" | |
| `model` `effort` | reviewer's "model"; §8 model/config metadata | from `.claude/agents/*.md` frontmatter (Anthropic aliases: haiku/sonnet/opus + effort tier) |
| `risk_class` | reviewer's "risk class"; paper Table 3 (R0–R3) | property of the change; verify jobs inherit the change's risk |
| `review_layer` | reviewer's "review layer" | which verification stage (scope-gate vs conformance) |
| `tokens_total` | reviewer's "cost"; C2 total *T(t)* | actual output tokens |
| `tokens_R` `tokens_A` `tokens_H` | **C2 / §4.7 R/A/H decomposition** | **currently `NI` — see gap below** |
| `tool_uses` `duration_ms` | effort proxies | |
| `human_minutes` `cost_usd` | reviewer's "human minutes" + "cost"; §6.5 | currently `NI` — see gap |
| `defects_found` `false_positives` | reviewer's "findings"/"false positives"; §6.5 marginal yield | detail in `findings.csv` |
| `defects_fixed` | rework flow; §6.5 correction cycles | |
| `deterministic_controls_added` | reviewer's "deterministic rule created"; paper P5 | count of tests/schemas/analyzers promoted from findings |
| `repeated_error` | reviewer's "repeated-error occurrence"; paper P8 | did a previously-seen error class recur? |
| `tests_after` | full suite passing count after the job | |
| `packet_type` | §8 dispatch metadata (added #141) | e.g. `task-packet/1` — the packet schema/version the job was dispatched with, from `tools/agent-brief.py`'s frontmatter |
| `prompt_hash` | §8 reproducibility (added #141) | hash of the exact dispatch prompt text, so a job's input is reproducible/comparable; `NI` until prompts are archived+hashed at dispatch time |
| `discovery_calls` | briefing-efficiency signal (added #141) | count of **broad** context-discovery actions only (repo-wide grep/glob/history search) — not every read/test/tool call. Never fabricated or reconstructed retroactively; a job that did not measure this live logs `NI` |

## Coverage vs. instrumentation gaps (read before analysing)

The schema is complete; the **capture** is not yet. Fields currently marked `NI` (not instrumented)
need logging the present subagent-completion telemetry does not expose. This mirrors the paper's own
candor in §4.7/§8 — and pins exactly what to build before the 30–100-task study the reviewer wants:

- **`tokens_R` / `tokens_A` / `tokens_H` (the C2 centerpiece) are `NI`.** Subagent completion
  telemetry reports only a *single total* output-token figure — no per-turn input/cached/reasoning/
  tool breakdown and no turn-level R/A/H category. The R/A/H split therefore *cannot* be derived
  retrospectively for Layer 3, exactly as §4.7 admits for task #9. Capturing it needs turn-level
  provider token fields plus a predeclared R/A/H tag per turn (and a blind second annotator for
  agreement). **This is the single most important instrument to add next.**
- **`cost_usd` is `NI`** — needs input+output+cached token counts and per-model pricing; only output
  totals are currently exposed.
- **`human_minutes` is `NI`** — orchestration/governance time is not measured.
- **`model` records the agent-def alias + effort, not a resolved model-id/version string** — enough
  to reproduce the routing, short of the exact provider version §8 ultimately wants.
- **Dispatch prompts are not yet hashed/archived.** For full §8 reproducibility, each job's dispatch
  prompt should be stored and hashed; presently they live only in the session transcript.
- **`packet_type` / `prompt_hash` / `discovery_calls` (added #141) are `NI` for every row logged
  before #141**, including all 14 rows that predate this schema change — none were reconstructed
  retroactively. Log them going forward with `--packet-type`, `--prompt-hash`, `--discovery-calls`;
  omit a flag rather than guess a value.

Analysts: treat `NI` as *not measured*, never as zero. Cross-layer comparisons are agent-job
output-token spend only until the R/A/H and cost instruments land.

## Method notes

- A verify job's value is realized in the *next* rework job it triggers; pair them for ROI.
- Token cost is weakly correlated with diff size: the Layer 3 cap-wiring rework — a one-field data
  change — cost more than the original build. Semantic size ≠ token size.
- Model tier is visible in the numbers: the haiku scope-gate is cheap; opus conformance audits are
  mid; the sonnet implementer is the largest consumer because it does the writing + full test runs.
