# agent-verification burn-in — the evidence behind requiring it

Issue #144 asked for `agent-verification` to be burned in on real PRs before it
becomes a required `main` status. This is that record: what ran, what it proved,
and what it did not (yet) prove.

## Real merged PRs that ran the full path

| PR | Route | `agent-verification` | Semantic AI reviewer used |
|----|-------|-----------------------|----------------------------|
| #145 | docs | success | none (ci only) |
| #146 | tooling | success | none (ci only) |
| #147 | tooling | success | none (ci only) |
| #148 | tooling | success | none (ci only) |
| #150 | docs | success | none (ci only) |
| #151 | tooling | success | none (ci only) |
| #152 | tooling | success | none (ci only) |
| #153 | docs | success (status posted) | none (ci only) |
| #154 | tooling | success | none (ci only) |
| #155 | tooling | success | none (ci only) |
| #157 | tooling | success (status posted) | none (ci only) |
| #158 | tooling | success | none (ci only) |

Twelve real PRs (docs + tooling — normal remediation work, no manufactured dummy
changes), all merged clean through: route derivation -> deterministic gates
(`build-and-test`, `pr-policy`, `orchestration-policy`) -> route-required semantic
gate (none demanded for docs/tooling post-#138) -> the `agent-verify` SHA-bound
aggregate -> merge.

## Route-class gate derivation

`tools/route.sh` is the single authority `tools/agent-verify.sh` consumes for which
gates a change's route requires:

| Route | Required gates |
|---|---|
| docs | `[ci]` — no semantic reviewer |
| tooling | `[ci]` — no semantic reviewer |
| gameplay | `[ci]` — no semantic reviewer |
| scenario | `[ci]` — no semantic reviewer |
| rules | `[ci, scope-warden, rules-conformance]` |
| formulas | `[ci, scope-warden, rules-conformance, codex-conformance]` |
| architecture | `[ci, architecture-review]` (composes on top of the base route) |

## Behaviors demonstrated

1. **docs/tooling invoke no semantic LLM review** — the 12 merged PRs above, all
   with gate set `[ci]`.
2. **rules get the proper semantic gates** — derivation table above; enforced by
   `tests/tooling/test_route.sh`.
3. **formulas get Codex** (`codex-conformance`) — derivation table above; enforced
   by `tests/tooling/test_route.sh`.
4. **architecture gets `architecture-review`** — derivation table above; naturally
   demonstrated on #158, where the (then-mislabeled `route:architecture`) linked
   issue #143 raised the route's floor and `agent-verify` required
   `architecture-review`.
5. **An omitted required gate leaves the aggregate `pending`, never `success`** —
   demonstrated on #158: with `architecture-review` unmet, `agent-verify` posted
   `pending`, and the PR did not merge on that status (it merged only because
   `agent-verification` was not yet a required check at the time). Also covered by
   `tests/tooling/test_agent_verify.sh`.
6. **A failed semantic gate produces `failure`** — covered by
   `tests/tooling/test_agent_verify.sh` (failure-aggregate cases).
7. **A new PR commit invalidates the previous head-SHA verification** — the status
   is a commit status bound to the head SHA; any new push moves the head, and the
   old status no longer applies to it (by design of `agent-verify.sh`; documented
   in its header).
8. **Re-verification succeeds only after gates re-run on the new head** —
   demonstrated on #158: after correcting the issue label, re-running
   `agent-verify` on the same head posted a fresh `success` bound to that SHA.
9. **Auto-merge does not outrun verification** — once `agent-verification` is a
   required check, a pending or absent status blocks merge (the ruleset gate); the
   pending-blocks behavior in (5) is the mechanism that makes this hold.

## Coverage honesty

This is strong, real, merged coverage on **docs + tooling** (12 PRs). It does not
include a live merged **rules-engine** PR: `rules`, `formulas`, `gameplay`, and
`scenario` routes are proven by (a) `route.sh`'s derivation, (b) the test suite
(`tests/tooling/test_route.sh`, `tests/tooling/test_agent_verify.sh`), and (c) for
`architecture`, a natural end-to-end pending demonstration on #158 — but not by a
live merged rules/formulas PR, because no rules-engine work was in this
remediation's scope and manufacturing dummy changes to force one would defeat the
point of a burn-in. The first real rules or formulas PR after `agent-verification`
becomes required will be the first time `scope-warden` / `rules-conformance` /
`codex-conformance` are exercised end-to-end under the required check, not merely
derived and unit-tested. That is a known, accepted gap in this burn-in, not a
hidden one.
