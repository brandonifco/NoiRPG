# agent-brief

`tools/agent-brief.py` assembles the minimal context an agent needs, from what the
repo already knows, so the agent spends its token budget reasoning about the
problem instead of rediscovering it. On #9 that rediscovery was ~40k of ~52k
tokens ([`docs/agent-team.md`](../agent-team.md), "Briefing agents efficiently").

## Task packet — before implementation

```bash
tools/agent-brief.py task 52
```

Emits, as markdown ready to paste into an implementer prompt:

- **TASK** — outcome, acceptance criteria, explicit exclusions (parsed from the Issue).
- **AUTHORITY** — `AGENTS.md`, the scope filter, source-handling, plus the ADRs whose
  titles best match the Issue title/labels/rules-source (foundational ADRs 0001–0003
  always included), and the cited rules source.
- **DEPENDENCIES** — every `#n` the Issue references, with its open/closed state.
- **WORKSPACE** — files/modules named in the Issue (and `Brp.X.Y` module tokens in the
  body) mapped to `src/…` directories and their neighbouring `tests/…` dirs.
- **REQUIRED GATES** — the route predicted from those paths via `tools/route.sh`.
- **DO NOT REVISIT** — the matched ADRs as locked decisions, plus any rejected approaches.

## Review packet — before review

```bash
tools/agent-brief.py review 74                 # a PR
tools/agent-brief.py review --base <ref> --head <ref> --issue 74
```

Emits:

- **RANGE / CHANGED FILES** — base…head and the file list.
- **IMPLEMENTER CLAIM** — the PR's "Exact behavioral claim" (to verify, not assume).
- **REQUIRED REVIEW** — the route (via `tools/route.sh`) turned into a per-gate review
  checklist: what `scope-warden` / `rules-conformance` / `codex-conformance` /
  `architecture-review` must each check.
- **DIFF** — `git diff -U1` for the range.

## Notes

- Uses `gh` and `git`; run it where the orchestrator runs.
- Issue templates vary; the parser matches section headers by known aliases and
  degrades gracefully when a section is absent.
- The predicted task route is a hint; the authoritative route is computed from the
  actual diff at PR time by [`pr-policy`](routing.md) (via `tools/route.sh`).
