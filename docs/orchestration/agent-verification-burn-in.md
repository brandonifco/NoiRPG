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
| #179 | formulas + architecture | success (5/5) | scope-warden + rules-conformance + independent Codex conformance + architecture-review |

Twelve real docs/tooling PRs (normal remediation work, no manufactured dummy
changes), all merged clean through: route derivation -> deterministic gates
(`build-and-test`, `pr-policy`, `orchestration-policy`) -> route-required semantic
gate (none demanded for docs/tooling post-#138) -> the `agent-verify` SHA-bound
aggregate -> merge. PR #179 (below) is the first real PR to exercise the full
semantic chain — see "First formulas-route PR (#112 / PR #179)" further down.

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
point of a burn-in. At the time this section was written, the first real rules or
formulas PR after `agent-verification` becomes required had not yet run — that gap
is now closed; see "First formulas-route PR (#112 / PR #179)" below, including its
honest account of what still required a human and an orchestrator in the loop.

## First formulas-route PR (#112 / PR #179)

Date: 2026-09-01. Subject: Issue #112 (hit locations — Ch 6), PR #179, route
`formulas` + `architecture` (the `Brp.Data.csproj` embed pulled in
`architecture-review`). Squash-merged to `main` as commit `3440130`. This closes the
gap named above: the first live `formulas`-route PR to run the **full** required
gate set — `ci` + `scope-warden` + `rules-conformance` + independent
`codex-conformance` + `architecture-review` — end to end under the enforcing
ruleset, and merge on a `success` `agent-verification` status (5/5) bound to the
final head SHA.

### What ran, in order

1. Task packet generated (`tools/agent-brief.py task 112`); engine-dev implemented
   on branch `112-hit-locations` (2359 tests); the orchestrator opened PR #179 with
   a formulas-route body.
2. Round 1 gates: `scope-warden` PASS; `rules-conformance` FAIL; independent Codex
   (`tools/codex-agent.sh conformance`) NOT-CONFIRMED.
3. Rework 1 (engine-dev, real defects) -> round 2 gates: `rules-conformance` PASS;
   `architecture-review` (design-critic) ran, pulled in by the `Brp.Data.csproj`
   embed, PASS; Codex round 2 pass.
4. Two more small reworks (a recorded design-contract packet block; an overflow
   fix) resolving round-3 and round-4 Codex findings, each followed by a fresh
   Codex pass.
5. `agent-verification` posted `success` (5/5) on head `c4cf378`; PR squash-merged
   as `3440130`.

Ledger rows for every job in this sequence are in
[`docs/agent-team-ledger/jobs.csv`](../agent-team-ledger/jobs.csv) (issue 112, PR
179, seq 1–12) and [`human-minutes.csv`](../agent-team-ledger/human-minutes.csv).

### Findings

**F1 — Independent Codex earned its place.** Codex caught real defects the
implementer's own 2359–2370 passing tests missed: (a) `ArmorCoverage` threw on the
printed `"All"` / `"All but head"` coverage labels (a crash), and (b) layered armor
used `max` instead of the printed *total* rule (p.209), justified in the first
draft by a fabricated citation. `rules-conformance` independently corroborated (b).
Model-family independence surfaced a class of error the same author's own tests did
not.

**F2 — Dual gates resolve each other's blind spots.** `rules-conformance` (full PDF
+ errata discipline) overruled Codex on the D20 `8–11` -> `9–11` misprint; Codex
caught the armor-label crash that `rules-conformance` missed, because
`rules-conformance` had only checked the *shipped* armor data, not the full printed
label vocabulary. Neither gate alone was sufficient.

**F3 — The Codex gate plus an adversarial "falsify" prompt produces
diminishing-returns nitpicks every pass**, so a strict "Codex must say CONFIRMED"
gate is effectively unsatisfiable on its own terms. Severity decayed across passes:
round 1 = a real crash and a real layering bug; round 3 = a legitimate design-contract
question (per-blow vs. cumulative damage banding); round 4 = an unreachable Int32
overflow plus a data-boundary point (`x2`/`x3` band constants) that `scope-warden`
had already ruled structural. This requires an explicit **triage policy**:
value/behavior conformance defects block; unreachable-robustness findings and
already-adjudicated points are logged, not blocking; the orchestrator (or a human)
adjudicates. This is the single most important process finding from this run, and
is direct input to #171.

**F4 — Documented deviations and deferred scope need packet-level authority (the
durable, reusable part of this finding).** A source-literal independent gate
rejects *any* departure from printed text unless the authority for that departure
is in its packet. Two patterns worked here and did not blunt independence — both
are legitimate recorded authority, analogous in kind to `orc-scope-filter.md`, and
both should be **reused on the next formulas PR rather than reinvented**:

- **Errata-authority packet block.** Quote the ADR's recorded misprint correction
  directly into the gate's source packet, so the independent gate verifies the
  implementation against the *recorded* erratum rather than flagging every
  deviation from the raw printed text as unsupported. (Used here for the D20
  `8–11` -> `9–11` table misprint, per `docs/decisions/0024-hit-locations.md`.)
- **Recorded-design-contract packet block.** State the component's contract
  explicitly in the packet (here: `HitLocationDamageResolver` is a stateless
  single-blow classifier; accumulation and downstream effects are the caller's
  job), so the gate verifies conformance-to-contract instead of assuming a
  different contract and flagging the difference as a defect.

**F5 — Tooling/hygiene gaps.** (a) A naive `which codex` reports "not installed"
because the binary lives at the hardcoded `CODEX_BIN` path, not on `PATH` —
availability checks must probe `CODEX_BIN` directly. (b) A truncated source
citation ("p.14") propagated into the first Codex source packet and caused a
coverage gap (0/45 HP cells verifiable) until the packet was rebuilt with the
correct pages — source-slice/citation hygiene matters, and the orchestrator must
sanity-check that a packet actually contains the table it cites.

**F6 — Branch-currency churn is real, at scale.** The strict
`orchestration-policy` rule (a branch must be current with `main` to merge) meant
every merge re-dated every other open PR, forcing an update-branch ->
re-CI -> re-post-`agent-verification` cycle per PR. Observed concretely across the
five PRs open in this session. This confirms the "one orchestrator, no merge queue"
scale finding already on record.

**F7 — Not hands-free.** The chain operated end to end and merged, but it was
**not** hands-free. It required orchestrator-built source packets (including the
errata and design-contract authority blocks in F4), triage of Codex findings
against the F3 policy, adjudication between gates that disagreed (F2), and driving
the branch-currency updates in F6. Three decisions were escalated to, and made by,
the human: re-running Codex with the errata packet; running `architecture-review`;
and the stateless-classifier design-contract scope call. Recorded honestly against
the "operate without manual reconstruction" bar named in the #174 outcome: it did
**not** — the chain requires informed orchestration, not unattended automation.

### Telemetry and its gaps

Per-job figures are in `docs/agent-team-ledger/jobs.csv` (issue 112, PR 179). Known
gaps, left `NI` rather than guessed (consistent with `docs/orchestration/metrics.md`):

- The engine-dev reworks' token counts were reported by subagent telemetry as
  cumulative-per-resume totals (~178k, ~231k, ~272k, ~285k across implement +
  three reworks), not as additive per-job deltas. Only the first (~178k, the
  initial implementation) is a clean per-job figure; the other three are recorded
  as `NI` in the ledger rather than mis-derived by subtraction.
- Codex conformance ran four times across this PR; per-run token counts were not
  captured by the completion telemetry the same way Claude subagent tokens are —
  all four Codex ledger rows carry `tokens_total = NI`.
- `cost_usd`, `human_minutes`, and the R/A/H token split remain uninstrumented for
  every job in this run, matching the pre-existing gaps `docs/agent-team-ledger/README.md`
  already documents.

## Second batch — orchestration-hardening PRs (#168–#174, #170/#171)

The run that produced the orchestration fixes (one-closing-Issue enforcement,
task-packet route floor, doc drift, project schema, this burn-in record, the
reviewer read-only layer, and the trust-root/triage ADR) surfaced further process
findings — mostly about the *merge machinery* rather than conformance. They extend,
not replace, F1–F7.

**F8 — Parallel agents collide on ADR numbers.** #170 and #171 ran concurrently,
each read `0024` as the latest decision record, and each allocated `0025` for its new
ADR. The orchestrator had to renumber one (#170 → `0026-reviewer-mechanical-read-only.md`,
updating every reference) after the other merged. ADR-number allocation is not
serialized; concurrent design-decision work needs either a reservation step or a
rule that the number is assigned at merge time, not authoring time.

**F9 — Off-old-base branches silently re-introduce settled index rows.** Both the
#170 and #171 branches were cut from a `main` that predated #112, so each "helpfully"
re-added the `docs/decisions/README.md` row for ADR `0024` that already existed on
current `main`. `update-branch` merges main's *content* but does not reconcile a
logical duplicate like two `0024` rows — the duplication only surfaced at
`architecture-review` (design-critic caught it on #171). Lesson: branch from current
`main`, and treat append-only index/registry files as a known merge-hazard to check
explicitly.

**F10 — gh API head-lag vs. git compounds F6.** After an `update-branch`, the REST/
GraphQL PR head (`gh pr view --json headRefOid`) lagged the git ref by several polls.
Because `tools/agent-verify.sh` derives the head via `gh`, it briefly operated on a
stale SHA — once posting `agent-verification = success` onto a *superseded* head,
after which the merge was correctly rejected as out-of-date. Mitigation that worked:
never act on a single read; poll until the git ref and the `gh` head **converge** on
the same SHA (and CI is green for it) before posting the status or merging. Every
merge in this batch was keyed to a git-confirmed, converged head.

**F11 — The route-intent floor propagates through the whole chain (a positive).**
#171 carried an issue-level `route:architecture` label. Plain `tools/route.sh` on the
diff reported `ci`-only, but `agent-verify.sh` (which passes `--issue`) correctly
raised the required set to include `architecture-review` on an otherwise docs-only
PR — and the gate earned its place by catching F9's duplicate-index drift. The
asymmetric floor and gate-set composition work end to end, not just at PR-policy time.
Caveat (tooling follow-up): the `agent-brief.py` review packet's architecture-review
checklist is templated BRP-layering boilerplate ("Brp.Core/Brp.Rules take no
game-engine dependency") that does not adapt when the gate is pulled in via an issue
floor on a docs change; design-critic flagged the mismatch and reviewed against the
correct scope anyway.

**F12 — Non-isolated subagents mutate the primary worktree.** The #174 writer was
dispatched without worktree isolation; it created and left the primary checkout on
its feature branch, which had to be manually restored (`checkout main` +
fast-forward) during cleanup. Dispatch doc/tooling agents with worktree isolation, or
expect to restore the primary tree afterward.
