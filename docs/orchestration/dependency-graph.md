# Dependency graph and the NoiRPG Project

Issue dependencies are a **native GitHub graph**, not prose. The orchestrator can
therefore ask a deterministic scheduling question — *which open issues have no
unresolved blockers?* — and those are the only candidates it may start. This is what
makes several agents actual parallel engineering instead of conflicting workers: the
scheduler picks independent leaves of the graph.

## The graph

Each issue's blockers are recorded as native **`blocked_by`** dependencies, and the
epic's children as native **sub-issues**. For epic #53:

```
#54 route metadata ──┬── #58 pr-policy ──┐
                     ├── #59 agent-brief │
                     ├── #60 dep DAG ─────┼── #63 metrics
                     └──────────┐         │
#56 protect main ──────────────┼── #62 state machine ──┐
#65 gate App ──────────────────┘                       ├── #73 auto-post gates
#55 policy CI ── #61 CI tiering        #62, #65 ────────┘
#57 dependabot (no deps)
```

## The scheduler query

```bash
tools/ready-issues.sh            # classify every open issue READY / blocked
tools/ready-issues.sh --ready    # just the ready issue numbers, one per line
```

An issue is **READY** when none of its `blocked_by` dependencies are still open. The
orchestrator can feed `--ready` straight into work selection; `BLOCKED` is derived
from the graph, never a manual judgment.

Editing the graph (ids are the REST database id, not the issue number):

```bash
# add a blocker: <n> is blocked by <m>
gh api --method POST repos/{owner}/{repo}/issues/<n>/dependencies/blocked_by \
  -F issue_id="$(gh api repos/{owner}/{repo}/issues/<m> --jq .id)"

# make <n> a sub-issue of epic <e>
gh api --method POST repos/{owner}/{repo}/issues/<e>/sub_issues \
  -F sub_issue_id="$(gh api repos/{owner}/{repo}/issues/<n> --jq .id)"
```

## The NoiRPG Project

A GitHub Project (v2) gives the graph a board with orchestration fields. Creating it
needs the **`project` write scope**, which the orchestrator's token does not carry
(read-only), so it is a one-time operator step:

```bash
gh auth refresh -s project
tools/setup-project.sh
```

`setup-project.sh` creates the project (idempotently) with these fields:

Workflow state uses GitHub's **built-in `Status`** field (Todo · In Progress · Done),
because the default project workflows maintain it automatically — a custom field
would not be touched by them. `setup-project.sh` adds these extra fields:

| Field | Type | Values |
|---|---|---|
| Layer | single-select | L0–L4 · Orchestration |
| Subsystem | text | — |
| Risk | single-select | low · medium · high |
| Verification Route | single-select | docs · tooling · rules · formulas · architecture (mirrors `tools/route.sh`) |
| Agent Role | single-select | engine-dev · case-author · scope-warden · rules-conformance · design-critic · rules-extractor · codex |
| Source-Conformance Required | single-select | yes · no |

then adds the epic plus its children and sets `Status = Done` on the already-closed
ones (the close-event workflow does not fire retroactively).

### Automations

The default Project workflows are **already enabled** and act on `Status`, so the
loop is maintained without code: **item closed → Done**, **pull request merged →
Done**, **item added → Todo**, and **sub-issues auto-added** to the board.

One workflow the Projects v2 API cannot configure (UI-only, and optional): **Project
→ ⋯ → Workflows → Auto-add to project**, filtered to repo items labelled
`orchestration` — needed only for orchestration issues that are not sub-issues of the
epic (those already auto-add). `BLOCKED` comes from the native dependency state above.
