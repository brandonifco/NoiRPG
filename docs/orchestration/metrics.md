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

### Verification effectiveness (gate catches)

| Metric | Source | Status |
|---|---|---|
| Gate `failure` counts | `check_run` conclusions on each merged PR's head commit, for `scope-warden`, `rules-conformance`, `codex-conformance`, `architecture-review` | **exact when present** |

These check-runs were posted by the gate-poster App, which was **removed in #90/#91**
along with the rest of the verification-gate system (it never posted a result). No gate
check-runs are produced on PR heads, so this section reports **0 catches** — read it as
*not instrumented*, never as a claim that verification caught nothing. The ledger's
`findings.csv` records the catches the conformance stages actually made; see Cost below.
`orchestration-metrics.py` still queries for these names and degrades to the same note.

### Cost (agent tokens)

| Metric | Source | Status |
|---|---|---|
| Output tokens per layer / per phase | [`docs/agent-team-ledger/jobs.csv`](../agent-team-ledger/jobs.csv) `tokens_total` | **exact for logged jobs** |
| Findings by detecting stage | [`docs/agent-team-ledger/findings.csv`](../agent-team-ledger/findings.csv) | **exact for logged findings** |

Only **output-token totals** are available. The R/A/H decomposition (`tokens_R/A/H`) and
`cost_usd` are `NI` in the ledger — see the ledger
[`README.md`](../agent-team-ledger/README.md) gap list. The tool reports the totals it has
and repeats the `NI` caveat rather than implying a full cost accounting exists.

### Human attention — the headline

| Metric | Source | Status |
|---|---|---|
| Human minutes / merged issue | optional `docs/agent-team-ledger/human-minutes.csv` | **manual input** |
| Human interventions / PR | optional `docs/agent-team-ledger/human-minutes.csv` | **manual input** |

This is the optimization target, and it is **not in any API** — GitHub does not record how
many minutes a human spent, or how many times they had to step in. The tool reads an
optional log if present, and otherwise prints `(not tracked / needs manual input)` so the
target stays visible on every report even before it is instrumented.

## The optional `human-minutes.csv`

To start capturing the headline metric, create
`docs/agent-team-ledger/human-minutes.csv` with this header (one row per merged
issue/PR). **Do not commit fabricated rows** — add a row only when you have actually
measured the time:

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
