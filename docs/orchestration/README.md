# Orchestration — start here

GitHub operates the NoiRPG agent organization: it routes work by risk, schedules
ready work from a dependency graph, and merges when its policy and CI checks pass.
This directory is the operating manual. Epic:
[#53](https://github.com/brandonifco/NoiRPG/issues/53).

> **Removed:** the model-driven verification gates (`gates-satisfied`, the
> `route-gates` aggregator, the `dispatch-gates.sh` dispatcher, and the gate-poster
> App) were taken out in #90/#91 — they never posted a gate on any PR. Route
> derivation and labelling survive; enforcement is `build-and-test` + `pr-policy` +
> `orchestration-policy`.

## The loop

```
product intent → dependency DAG → ready-leaf scheduler → route + label
   → CI + policy checks → auto-merge → main → DAG unlocks next work
```

## Map of the tooling

| Concern | Tool / workflow | Doc |
|---|---|---|
| Which gates a change needs | `.github/route-map`, `tools/route.sh` | [routing.md](routing.md) |
| PR is machine-readable | `.github/workflows/pr-policy.yml` (+ `pr_policy.py`) | — |
| Invariants as CI | `tools/orchestration-policy.sh`, `.github/workflows/orchestration-policy.yml` | — |
| Context / review packets | `tools/agent-brief.py` | [agent-brief.md](agent-brief.md) |
| Dependency graph + board | `tools/ready-issues.sh`, `tools/setup-project.sh` | [dependency-graph.md](dependency-graph.md) |
| Metrics | `tools/orchestration-metrics.py` | [metrics.md](metrics.md) |
| Agent roster & routing policy | — | [../agent-team.md](../agent-team.md) |

Everything above is committed — a fresh `git clone` has the whole system.

## Resuming on another device

`git pull` brings all code and docs. Two things live **outside** the repo and must
be set up once per device:

1. **`gh` auth with the right scopes.**
   ```bash
   gh auth login
   gh auth refresh -s project        # needed only for tools/setup-project.sh
   gh auth status                    # want: repo, workflow, project, read:org
   ```
2. **Merging into `main`** is by PR only (protected). Push a branch, open a PR; when
   a PR is behind, `gh api --method PUT repos/{owner}/{repo}/pulls/<n>/update-branch`.

Then, to see what to work on next:
```bash
tools/ready-issues.sh            # READY vs blocked, from the native dependency graph
tools/ready-issues.sh --ready    # just the ready issue numbers
```

## Current state (2026-08-31)

- **Shipped:** #54 routing, #55 policy gate, #56 protected main + auto-merge, #57
  Dependabot, #58 pr-policy + evidence, #59 agent-brief, #60 dependency graph +
  Project, #63 metrics.
- **Removed:** #62 routing state machine, #65 gate App, #73 dispatcher (the
  `gates-satisfied` verification-gate system) — never posted a gate; taken out in
  #90/#91.
- **Deferred:** #61 (CI tiering) — until CI latency actually bites.
- **Still manual (by design):**
  - The Project's **label-based "Auto-add to project"** workflow — UI-only (no
    Projects v2 API for workflows). Sub-issue auto-add is already on.
- **Project board:** https://github.com/users/brandonifco/projects/1 (workflow state
  in the built-in `Status` field, maintained by the default automations).
