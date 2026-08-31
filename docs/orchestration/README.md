# Orchestration — start here

GitHub operates the NoiRPG agent organization: it routes work by risk, schedules
ready work from a dependency graph, dispatches verification gates, and merges when
satisfied. This directory is the operating manual. Epic:
[#53](https://github.com/brandonifco/NoiRPG/issues/53).

## The loop

```
product intent → dependency DAG → ready-leaf scheduler → route → gates dispatched
   → App posts results → gates-satisfied aggregates → CI + policy checks → auto-merge
   → main → DAG unlocks next work
```

## Map of the tooling

| Concern | Tool / workflow | Doc |
|---|---|---|
| Which gates a change needs | `.github/route-map`, `tools/route.sh` | [routing.md](routing.md) |
| PR is machine-readable | `.github/workflows/pr-policy.yml` (+ `pr_policy.py`) | — |
| Invariants as CI | `tools/orchestration-policy.sh`, `.github/workflows/orchestration-policy.yml` | — |
| Routing state machine | `.github/workflows/route-gates.yml` (+ `route_gates.py`) → `gates-satisfied` | [routing.md](routing.md) |
| Post a gate result | `tools/gate-check.py` (GitHub App) | [github-app.md](github-app.md) |
| Run + post gates per PR | `tools/dispatch-gates.sh` | [auto-dispatch.md](auto-dispatch.md) |
| Context / review packets | `tools/agent-brief.py` | [agent-brief.md](agent-brief.md) |
| Dependency graph + board | `tools/ready-issues.sh`, `tools/setup-project.sh` | [dependency-graph.md](dependency-graph.md) |
| Metrics | `tools/orchestration-metrics.py` | [metrics.md](metrics.md) |
| Agent roster & routing policy | — | [../agent-team.md](../agent-team.md) |

Everything above is committed — a fresh `git clone` has the whole system.

## Resuming on another device

`git pull` brings all code and docs. Four things live **outside** the repo and must
be set up once per device:

1. **`gh` auth with the right scopes.**
   ```bash
   gh auth login
   gh auth refresh -s project        # needed only for tools/setup-project.sh
   gh auth status                    # want: repo, workflow, project, read:org
   ```
2. **The gate-poster GitHub App key** (a secret, never committed). App id `4776197`.
   Copy `~/.config/noirpg/gate-poster.pem` from the old device (securely), or
   generate a fresh private key on the App's settings page. Then:
   ```bash
   export GH_APP_ID=4776197
   export GH_APP_PRIVATE_KEY=~/.config/noirpg/gate-poster.pem
   tools/gate-check.py --gate scope-warden --sha "$(git rev-parse HEAD)" \
     --conclusion success --dry-run     # confirms the key + id are wired
   ```
   See [github-app.md](github-app.md).
3. **Agent runners for live gate dispatch** (only if running `dispatch-gates.sh`):
   the `claude` CLI logged in, and Codex configured (`CODEX_BIN`). See
   [auto-dispatch.md](auto-dispatch.md).
4. **Merging into `main`** is by PR only (protected). Push a branch, open a PR; when
   a PR is behind, `gh api --method PUT repos/{owner}/{repo}/pulls/<n>/update-branch`.

Then, to see what to work on next:
```bash
tools/ready-issues.sh            # READY vs blocked, from the native dependency graph
tools/ready-issues.sh --ready    # just the ready issue numbers
```

## Current state (2026-08-31)

- **Shipped:** #54 routing, #55 policy gate, #56 protected main + auto-merge, #57
  Dependabot, #58 pr-policy + evidence, #59 agent-brief, #60 dependency graph +
  Project, #62 routing state machine, #63 metrics, #65 gate App, #73 dispatcher.
- **Deferred:** #61 (CI tiering) — until CI latency actually bites.
- **Still manual (by design):**
  - The Project's **label-based "Auto-add to project"** workflow — UI-only (no
    Projects v2 API for workflows). Sub-issue auto-add is already on.
  - **Phase-2 enforcement:** once `dispatch-gates.sh` posts gates reliably, add
    `gates-satisfied` to the `main` ruleset's required checks — the switch is in
    [auto-dispatch.md](auto-dispatch.md).
- **Project board:** https://github.com/users/brandonifco/projects/1 (workflow state
  in the built-in `Status` field, maintained by the default automations).
