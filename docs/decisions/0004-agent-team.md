# 0004. Model-routed agent team with cross-vendor verification

## Status

Accepted — 2026-08-29

## Context

This project is built primarily by coding agents. Two cost drivers dominate:

1. **Context rediscovery** — agents re-deriving decisions, scope, and source-of-truth
   every session. Addressed by the GitHub workflow adoption: Issues as the only queue,
   `AGENTS.md` as the operating contract, ADRs for durable decisions.
2. **Uniform reasoning effort** — running frontier reasoning on transcription work,
   or shallow reasoning on the rules verification that most needs depth.

The second is specific to this project's shape. The engine is largely transcription
from a printed rulebook plus bounded implementation against settled Issues — cheap
work in volume. But a small fraction of it, the resolution kernel and its conformance
to printed tables, has an unusually bad failure profile: a wrong formula is plausible,
passes casual review, and silently corrupts every layer above it.

That failure mode is not hypothetical here. Two Chaosium documents were present in
the repository with different success grades and different threshold rounding, and
the design documents cited a third arrangement. Code derived from the wrong one would
have looked entirely correct.

## Decision

Define a routed agent team in `.claude/agents/`, matching model and reasoning effort
to task risk rather than applying one setting uniformly. Cheap models handle
transcription and mechanical gating; frontier models handle adversarial verification
and design review.

Add Codex as a **verification instrument only**, invoked through
`tools/codex-agent.sh` with a read-only sandbox, for independent cross-checking of
rules conformance and core-rules review.

Routing rules live in `docs/agent-team.md`.

## Alternatives considered

**One general-purpose agent at a uniform high setting.** Simplest, and correct at
small scale. Rejected because the bulk of the work is transcription, where the extra
reasoning buys nothing, and paying for it everywhere makes it harder to justify
paying for it where it matters.

**Claude-only, using a second Claude agent for verification.** Cheaper and simpler
than adding a second vendor. Rejected for rules conformance specifically: an
independent check has value in proportion to its independence, and a second agent
from the same family re-deriving the same table from the same prose tends to make the
same mistakes. Retained for everything else — `rules-conformance` is Claude, and
Codex is a cross-check on top of it, not a replacement.

**Codex as a second implementation workhorse.** Rejected. It would double the
surface needing reconciliation for no clear gain, and diffuse the one thing it is
uniquely good for here.

## Consequences

- Verification agents get read-only tools. An agent that can edit what it checks will
  eventually make the check pass rather than make the code right.
- Cheap gates run before expensive ones; `scope-warden` precedes `rules-conformance`.
- Codex requires `/usr/lib/chatgpt/resources/codex` and a configured `~/.codex/`. The
  wrapper fails loudly if absent rather than silently skipping verification.
- The expensive path is reserved for core rules. Tooling, CLI, docs, and case data
  stop after the cheap gate.
- Effectiveness must be measured, not assumed — see the measures in
  `docs/agent-team.md`. If the routing does not reduce turns-to-merged-PR, it is
  overhead and should be simplified.
