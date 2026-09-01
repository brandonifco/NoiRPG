# Orchestration metrics

[`tools/orchestration-metrics.py`](../../tools/orchestration-metrics.py) measures the
**orchestration system itself**, against one target:

> **minimum human attention per correctly-merged unit of capability**

Not lines of code. Not raw PR count. A change that merges fast but needs three rounds of
human correction is worse than a slow one that merges untouched. This tool exists to make
that target visible — including the parts of it that are not yet instrumented, which it
labels honestly rather than filling with invented numbers.

## Running it

```bash
tools/orchestration-metrics.py                 # last 20 merged PRs, markdown
tools/orchestration-metrics.py --limit 50      # wider window
tools/orchestration-metrics.py --since 2026-08-01
tools/orchestration-metrics.py --json          # same data, JSON
```

Pure stdlib; it shells out to `gh` (authenticated) and reads the ledger CSVs. If `gh`
is unavailable or a CSV is missing, the affected metric degrades to a note — the tool
never crashes and never fabricates a value.

## Reading the labels

Every metric carries its epistemic status:

- **exact** — computed directly from a reliable field (e.g. PR `mergedAt − createdAt`).
- **approximate** — derived from a signal that may be missing or noisy; the report says why.
- **HEURISTIC** — a proxy for something not directly measured; the heuristic is stated inline.
- **(not tracked / needs manual input)** — no reliable data source yet. Treat as *not
  measured*, **never as zero** (same convention as the ledger's `NI`).

## Metrics, sources, and status

### Cycle time

| Metric | Source | Status |
|---|---|---|
| PR opened → merged | `gh pr list --state merged --json createdAt,mergedAt` | **exact** |
| Issue READY → PR opened | `ready`-label `labeled` event on the closing issue's `gh api .../issues/{n}/timeline`, vs PR `createdAt` | **approximate** |

READY→PR is approximate because it depends on the closing issue actually having been
labelled `ready` *before* the PR was opened. PRs whose closing issue can't be resolved
(no `Closes #n` in the body and no `(#n)` title tail), or that have no such label event,
are counted as **skipped** and excluded from the statistic rather than guessed.

### Throughput & quality

| Metric | Source | Status |
|---|---|---|
| Merged PRs in window | `gh pr list --state merged` | **exact** |
| First-try vs needed-correction | CI run history per branch + review count | **HEURISTIC** |
| CI failure rate | `gh run list --workflow ci.yml` | **exact** (for the window) |

The first-try heuristic marks a PR as *needed-correction* when its head branch had at
least one **failed `build-and-test` run** before merge, **or** it carried ≥1 review.
This is a proxy for rework, not a measurement of it: a PR that was fixed by force-push
before its first CI run, or reworked purely in the working tree, reads as first-try. A
branch with no CI run inside the fetched window is reported as **undetermined**, not
first-try, so an aged-out window can't inflate the success count.

### Verification effectiveness — current architecture (`agent-verification`)

| Metric | Source | Status |
|---|---|---|
| `agent-verification` commit-status state per PR head | `gh api repos/{slug}/commits/{sha}/status`, context `agent-verification` (posted by `tools/agent-verify.sh --post`, #131) | **exact when present** |
| Per-gate pass/fail | the canonical evidence block `tools/agent-verify.sh --evidence` writes into the PR body (#136), parsed from its `<!-- agent-verification:start/end -->` markers | **exact when present** |
| Route distribution | the `[route: ...]` tag inside that same evidence block | **exact when present** |
| % PRs needing semantic AI review / % PRs with zero AI verification beyond `ci` | evidence block's gate set: any gate besides `ci` = semantic review required | **exact, over PRs carrying an evidence block** |
| Codex invocation rate | fraction of evidenced PRs whose required gates include `codex-conformance` | **exact, over PRs carrying an evidence block** |

This is what actually gates a merge today (see `docs/orchestration/agent-verification.md`).
A PR with neither a status nor an evidence block predates #131/#136, or never had
`agent-verify.sh` run against it — that is reported plainly, never folded into a
count that implies verification failed.

### Verification effectiveness — HISTORICAL (pre-#90/#91 check-runs)

| Metric | Source | Status |
|---|---|---|
| Gate `failure` counts | `check_run` conclusions on each merged PR's head commit, for `scope-warden`, `rules-conformance`, `codex-conformance`, `architecture-review` | **HISTORICAL-COMPAT** |

These check-runs were posted by the gate-poster App, which was **removed in #90/#91**
along with the rest of the verification-gate system (it never posted a result). No gate
check-runs are produced on PR heads under the current architecture, so this section
reports **0 catches on any current PR** — that zero is an artifact of the old system's
removal, **not** a measurement that the current architecture's verification caught
nothing. Read the section above for the current architecture instead. This one is kept
only so a PR merged before #90/#91 can still be inspected; `orchestration-metrics.py`
labels its function `gate_catch_metrics` as HISTORICAL-COMPAT and its markdown section
header says so explicitly. The ledger's `findings.csv` records the catches the
conformance stages actually made; see Cost / Job telemetry below.

### Cost (agent tokens)

| Metric | Source | Status |
|---|---|---|
| Output tokens per layer / per phase (Claude subagent jobs) | [`docs/agent-team-ledger/jobs.csv`](../agent-team-ledger/jobs.csv) `tokens_total` | **exact for logged jobs** |
| Input+output tokens per Codex run | same, `tokens_total` on `agent_role`-`codex-*` rows | **exact for logged Codex runs, added #190** |
| Findings by detecting stage | [`docs/agent-team-ledger/findings.csv`](../agent-team-ledger/findings.csv) | **exact for logged findings** |

Claude subagent jobs still expose only **output-token totals**. The R/A/H decomposition
(`tokens_R/A/H`) and `cost_usd` are `NI` in the ledger — see the ledger
[`README.md`](../agent-team-ledger/README.md) gap list. The tool reports the totals it has
and repeats the `NI` caveat rather than implying a full cost accounting exists.

**Codex runs (#190 spike outcome).** `codex exec --json` emits one `turn.completed` event
per run carrying a structured `usage` object: `input_tokens`, `cached_input_tokens`,
`cache_write_input_tokens`, `output_tokens`, `reasoning_output_tokens`. `tools/codex-agent.sh`
now parses that event and logs `tokens_total = input_tokens + output_tokens` on every
`codex-*` ledger row where it was parseable, instead of an unconditional `NI`. Two things
were spiked and found **not** obtainable, and remain hard `NI` gaps:

- **`cost_usd` for Codex runs.** The binary reports no dollar figure at any verbosity
  (`--json` or otherwise), and this repo has no authoritative per-model Codex pricing table
  to multiply the token counts by without guessing. Fabricating one would violate the
  ledger's no-invented-numbers rule, so `cost_usd` stays `NI`.
- **The human-readable "tokens used" line** that `codex exec` prints without `--json` is
  *not* a usable substitute for the structured figure above — on an equivalent prompt in
  this spike it disagreed with `usage.input_tokens + usage.output_tokens` by roughly 3x,
  and the binary documents no definition of what it counts (cumulative session? current
  turn only? cache-adjusted?). It is not parsed for that reason.

A side effect: `codex-agent.sh` now always passes `--json`, so its live terminal transcript
is one JSON object per turn event rather than prose. `--output-last-message` still writes
the plain-text final answer to the `OUT` file for anyone reading the result, and a raw copy
of the JSONL event stream is kept at a sibling temp file (printed at the end of the run) so
the usage figure is independently checkable.

### Job telemetry (briefing efficiency, build/verify/rework)

| Metric | Source | Status |
|---|---|---|
| Agent tokens per merged Issue | `jobs.csv` `tokens_total` grouped by `issue` | **exact for logged jobs** |
| Build/verify/rework token proportions | `jobs.csv` `phase` × `tokens_total` | **exact for logged jobs** |
| Median tool uses / job | `jobs.csv` `tool_uses` | **exact for logged jobs** |
| Median discovery calls / job | `jobs.csv` `discovery_calls` (added #141) | **exact where measured — `NI` for every job logged before #141** |
| Codex invocation rate (ledger) | share of `jobs.csv` rows whose `agent_role` names Codex | **exact for logged jobs** |
| Verification tokens per confirmed defect | verify-phase `tokens_total` ÷ Σ(`defects_found` − `false_positives`) | **exact for logged jobs** |

`discovery_calls` counts only **broad** context-discovery actions (repo-wide grep/glob/
history search) — not every read/test/tool call — and is never reconstructed
retroactively; see the ledger README. `packet_type` and `prompt_hash` (also added #141)
are dispatch metadata, not yet surfaced as a metric on their own.

### Human attention — the headline

| Metric | Source | Status |
|---|---|---|
| Human minutes / merged issue | optional `docs/agent-team-ledger/human-minutes.csv` | **manual input** |
| Human interventions / PR | optional `docs/agent-team-ledger/human-minutes.csv` | **manual input** |

This is the optimization target, and it is **not in any API** — GitHub does not record how
many minutes a human spent, or how many times they had to step in. The tool reads an
optional log if present, and otherwise prints `(not tracked / needs manual input)` so the
target stays visible on every report even before it is instrumented.

## The `human-minutes.csv`

The headline metric is captured one row per merged issue/PR in
`docs/agent-team-ledger/human-minutes.csv` (seeded header-only). **Do not commit
fabricated rows** — add a row only when you have actually measured the time. Append
with the ledger helper rather than editing the CSV by hand:

```bash
tools/ledger-log.sh human --issue 112 --pr 130 --merge-sha <sha> \
                          --human-minutes 6 --interventions 0 --note "merged clean"
```

The helper refuses an all-`NI` row, so a real measurement (`--human-minutes` and/or
`--interventions`) is required. The header is:

```csv
issue,pr,merge_sha,human_minutes,interventions,note
```

| Column | Meaning |
|---|---|
| `issue` | issue number the merged unit closed |
| `pr` | PR number |
| `merge_sha` | squash-merge SHA (ties the row to a specific merged unit) |
| `human_minutes` | wall-clock minutes a human spent on this unit (triage + review + correction + merge) |
| `interventions` | number of distinct times a human had to step in (clarifications answered, manual fixes, re-dispatches) |
| `note` | free text — what the human time went to |

Use `NI` for a field you haven't measured yet; the tool skips `NI`/blank values rather
than treating them as zero, matching the ledger convention.
