# agent-brief

`tools/agent-brief.py` assembles the minimal context an agent needs, from what the
repo already knows, so the agent spends its token budget reasoning about the
problem instead of rediscovering it. On #9 that rediscovery was ~40k of ~52k
tokens ([`docs/agent-team.md`](../agent-team.md), "Briefing agents efficiently").

**Packet-first is mandatory.** Every implementation and review agent role
(`.claude/agents/*.md`) treats a missing packet as a process error, not something to
work around by re-deriving context itself. A missing TASK packet before
implementation, or a missing REVIEW packet before review, means stop and say so.
Agent roles also carry a broad-discovery budget: named files and their necessary
one-hop neighbors are normal reads; more than five broad repo-wide discovery
operations (grep/glob/history searches) means the packet was deficient, and the
agent should stop and return `BRIEF DEFICIENCY` rather than quietly widening its
own search.

## Task packet — before implementation

```bash
tools/agent-brief.py task 52
```

Emits, as markdown ready to paste into an implementer prompt:

- **TASK** — outcome, acceptance criteria, explicit exclusions (parsed from the Issue;
  a stable placeholder when the Issue states none).
- **AUTHORITY** — `AGENTS.md`, the scope filter, source-handling, plus the ADRs whose
  titles best match the Issue title/labels/rules-source (foundational ADRs 0001–0003
  always included), and the cited rules source.
- **DEPENDENCIES** — every `#n` the Issue references, with its open/closed state.
- **WORKSPACE** — files/modules named in the Issue (and `Brp.X.Y` module tokens in the
  body) mapped to `src/…` directories and their neighbouring `tests/…` dirs.
- **REQUIRED GATES** — the route predicted from those paths via `tools/route.sh`.
- **DO NOT REVISIT** — the matched ADRs as locked decisions.
- **KNOWN DEAD ENDS** — rejected approaches recorded on the Issue, or a stable
  placeholder when none are recorded.

## Review packet — before review

```bash
tools/agent-brief.py review 74                 # a PR
tools/agent-brief.py review --base <ref> --head <ref> --issue 74
```

Emits:

- **PR / ISSUE** — the PR number, the linked Issue, and a reference to that Issue's
  own task packet (`tools/agent-brief.py task <issue>`) when one applies.
- **RANGE / CHANGED FILES** — the exact base and head commit SHAs and the file list.
- **AUTHORITY** — the same authority documents and matched ADRs as the task packet,
  derived from the PR title and changed files.
- **IMPLEMENTER CLAIM** — the PR's "Exact behavioral claim" (to verify, not assume).
- **REQUIRED REVIEW** — the route (via `tools/route.sh`) turned into a per-gate review
  checklist: what `scope-warden` / `rules-conformance` / `codex-conformance` /
  `architecture-review` must each check.
- **ESCALATION REASON** — why the route is what it is (content escalation, an
  issue-intent floor, an architecture addition, or none).
- **DIFF** — `git diff -U1` for the range.

## Reproducible packet hash

Every packet ends with a footer:

```
---
packet-schema: task-packet/1
packet-version: 1
packet-sha256: <hex>
```

The hash covers only the packet's semantic content — never a timestamp or other
wall-clock value — so running the same command twice against identical repo/issue/PR
state reproduces the identical `packet-sha256`. This is what lets a consumer treat
two packets as "the same brief" without re-diffing the markdown.

## Notes

- Uses `gh` and `git`; run it where the orchestrator runs.
- Issue templates vary; the parser matches section headers by known aliases and
  degrades gracefully when a section is absent.
- The predicted task route is a hint; the authoritative route is computed from the
  actual diff at PR time by [`pr-policy`](routing.md) (via `tools/route.sh`).
