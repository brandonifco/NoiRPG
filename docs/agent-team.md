# Agent Team

Token cost is dominated by running expensive reasoning on cheap problems. This file
routes work to the smallest model that can do it correctly, and reserves expensive
reasoning for the two places where being wrong is costly and hard to detect.

## Roster

| Agent | Model | Effort | Use for | Cost posture |
|---|---|---|---|---|
| `rules-extractor` | Haiku | medium | Transcribing tables and stat blocks from the source PDF into ruleset JSON | Cheap, high volume |
| `scope-warden` | Haiku | low | One checklist against a diff — out-of-scope content, wrong source, era baselines, determinism | Cheapest gate; run first |
| `engine-dev` | Sonnet | medium | Implementing one settled Issue in C# | Workhorse |
| `orchestration-dev` | Sonnet | medium | Implementing one settled Issue in the orchestration/tooling layer — workflows, Bash/Python tools, evidence schemas, metrics, packets, orchestration docs | Workhorse (control plane) |
| `case-author` | Sonnet | medium | Case YAML, Three Doors compliance, build coverage | Workhorse |
| `rules-conformance` | Opus | high | Adversarially verifying implemented mechanics against printed tables | Expensive, narrow |
| `design-critic` | Opus | xhigh | Phase-gate design review | Expensive, rare |
| Codex `conformance` | GPT (configured default) | high | **Independent** second verification of rules tables | Expensive, reserved |
| Codex `review` | GPT | high | Fresh-context review of a core-rules diff | Expensive, reserved |
| Codex `simcheck` | GPT | high | Independently re-deriving simulation math | Expensive, rare |

## Why Codex is in the team at all

A second Claude agent re-deriving a table tends to reproduce the first one's
reasoning, including its mistakes. A different model family is far less likely to.
That independence is the only thing Codex is here for, so it is used only where a
silent error would be expensive and hard to catch:

- **Rules conformance.** This project has already demonstrated the failure mode. Two
  Chaosium books were present in the repo with different success grades and different
  threshold rounding, and code derived from the wrong one looks entirely correct.
- **Core-rules review.** The guide's fresh-context review advice, applied to the one
  subsystem where independence is worth the second context load.

Do not use Codex for bulk implementation, transcription, or anything a Haiku agent
handles. It is a verification instrument, not a second workhorse.

## Invoking Codex

There is one documented Codex path, and it is packet-first: build the bounded
packet(s) first, then hand the file(s) to `tools/codex-agent.sh`. Codex never
assembles its own context — no whole-repo survey, no independent page-hunting when
a source packet is supplied, and (for `conformance`) no exposure to any other
verifier's notes or conclusions.

```bash
# conformance — independent second verification of rules tables
tools/agent-brief.py review <pr> > /tmp/review.md
tools/source-slice.py --pages 130-132 --output /tmp/source.txt
tools/codex-agent.sh conformance --review-packet /tmp/review.md --source-packet /tmp/source.txt

# review — fresh-context review of a core-rules diff (the review packet already
# carries the diff, route, and required-gate checklist)
tools/agent-brief.py review <pr> > /tmp/review.md
tools/codex-agent.sh review --review-packet /tmp/review.md

# simcheck — independently re-deriving simulation math from a bounded packet
tools/codex-agent.sh simcheck --packet /tmp/some-packet.txt

DRY_RUN=1 tools/codex-agent.sh conformance --review-packet /tmp/review.md --source-packet /tmp/source.txt
```

Sandbox is `read-only` for every role — the verification agents must not be able to
"fix" what they are checking, and Codex never implements. `DRY_RUN=1` prints the
composed command and prompt (including a `prompt-sha256` of the packet content fed
in) without invoking the real Codex binary. Set `LEDGER_LOG=1` (plus whatever of
`ISSUE`/`PR`/`SEQ`/`LAYER`/`PHASE` are actually known) to append one job-telemetry
row via `tools/ledger-log.sh` on a real run.

## Routing rules

**Run cheap gates before expensive ones.** `scope-warden` costs almost nothing and
catches the errors most likely to appear. It runs before `rules-conformance`, never
after.

**Escalate on risk, not on size.** A three-line change to a threshold formula deserves
`rules-conformance` and a Codex cross-check. A three-hundred-line change to CLI
formatting deserves neither.

**Route by layer, not by who is holding the context.** BRP C# implementation goes to
`engine-dev`; orchestration/tooling implementation — workflows, Bash/Python tools,
evidence schemas, metrics, packet tooling, orchestration docs — goes to
`orchestration-dev`; case/scenario content goes to `case-author`. The main
orchestrator loop is the *dispatcher*, not the default implementer: it should not
spend its own high-capability context writing routine Bash, Python, YAML, or docs when
a Sonnet worker can. Do not route routine implementation to Opus.

**Never route an open design question to an implementation agent.** `engine-dev` and
`orchestration-dev` both stop and report when they hit one. That is correct behavior,
not a failure — deciding it inside an implementation PR is how decisions get buried
where no later agent will find them.

**`needs-design` is a scheduler stop, not a suggestion.** `tools/ready-issues.sh`
treats `needs-design` as a human gate, the same as `blocked`: an unresolved
original-design Issue must never be converted into implementation work — Sonnet or
otherwise — merely because its dependencies have closed. Route/gate derivation (below)
tells verification what a *settled* Layer 5 change will need; it is not permission to
treat an unsettled one as ready.

**Layer 5 (`gameplay`/`scenario`/`presentation`) is design-led, not source-conformance.**
`tools/route.sh` gives the noir game layer on top of BRP its own routes —
`gameplay` (original mechanics, `src/Noir.Rules/**`), `scenario` (authored case
content and its schema engine, `cases/**` and `src/Noir.Scenario/**`), and
`presentation` (game engine / client code, `src/Noir.Game/**` /
`src/Noir.Client/**`) — each with a `ci`-only gate set. None of them is checked
against a printed BRP table, so none of them gets `rules-conformance` or
`codex-conformance`; instead each relies on a deterministic, code-level gate:
CI plus the layer's own tests for `gameplay`, and `tools/case_validator.py` (schema,
the Three Doors rule, junction budget, canonical skills) for `scenario` — the latter
run over every `cases/*.yaml` inside the required `build-and-test` job, so a malformed
case fails a required check rather than relying on anyone to remember to validate it. `design-critic`
sits at the design/phase gate where a mechanic is *decided*, not on every routine PR
that implements an already-settled one, and `case-author` is a content-producing role
— it is not paired with an Opus review on ordinary case YAML. See
[`docs/orchestration/routing.md`](orchestration/routing.md) for the full route table.

**Verification agents get read-only tools.** An agent that can edit what it is checking
will eventually make the check pass instead of making the code right.

**Dispatch implementer and writeup agents through `tools/dispatch-agent.sh` — worktree
isolation is a rail, not a reminder.** Burn-in finding F12
(`docs/orchestration/agent-verification-burn-in.md`): the #174 writeup agent was
dispatched without worktree isolation, created its feature branch in the primary
checkout, and left it there — the primary tree had to be manually restored
(`git checkout main` plus a fast-forward) during cleanup. Rather than remembering to
"give the agent a worktree", stand its workspace up mechanically:

```bash
tools/dispatch-agent.sh <issue#>     # prints  path=<abs worktree>  branch=<issue-n-slug>
```

Dispatch the implementer/writeup agent with that `path` as its working directory, and
`--cleanup <issue#>` the worktree after merge. The three implementer-facing rails:

- `tools/dispatch-agent.sh <issue#>` never checks the feature branch out into the
  primary tree, so using it cannot reproduce F12.
- Every implementer role (`engine-dev`, `orchestration-dev`, `case-author`,
  `rules-extractor`) runs `tools/dispatch-agent.sh --assert-isolated` as its first
  action and **stops with a dispatch error** if it finds itself in the primary
  checkout — a mechanical self-check (git's own worktree metadata), so a forgotten
  option fails fast instead of stranding the primary tree.
- Those same roles register a PreToolUse `Write|Edit` hook,
  [`tools/dispatch-write-guard.sh`](../tools/dispatch-write-guard.sh), so that even
  if the agent skips its first-action check, the **first mutation whose target is in
  the primary checkout is denied** (exit 2, reason fed back). This is the same
  mechanical-hook pattern that makes reviewers read-only
  ([`tools/reviewer-bash-guard.sh`](../tools/reviewer-bash-guard.sh), ADR 0026).

A shell tool cannot intercept the harness's Agent dispatch itself; but between the
preflight, the self-check, and the write-time hook, an ordinary implementer role can
no longer mutate the primary tree through the normal path — which is what F12 needs.
Reviewer (read-only) roles do not write and need no worktree.

## Authoring a design-decision record (ADR) — the `NNNN` placeholder

Never pick a `docs/decisions/NNNN-slug.md` number by reading the highest one
committed on your own branch. Two authors on parallel branches off the same base can
both see the same "latest" number and both claim the next one — exactly what happened
on #170/#171 (burn-in finding **F8**: both authored `0025`; the collision only
surfaced at merge time and had to be renumbered by hand).

Instead, write the record with the literal placeholder token `NNNN` everywhere its
number would appear: the filename (`docs/decisions/NNNN-slug.md`), the `# NNNN. Title`
header, any self-reference in the body, and the `docs/decisions/README.md` row you add
for it. Do not guess a number and do not reserve one in a shared file — either still
races under true parallelism (see `docs/decisions/0027-adr-number-allocation.md` for
why a `next-adr.sh`-style helper and a reservation registry were both rejected).

The number is assigned exactly once, by `tools/assign-adr-number.sh`, run as a
pre-merge step against the tree about to be merged — today that means whoever is
merging the PR runs it by hand before merging, the same trust level as every other
`tools/*.sh` gate in this repo. It finds the placeholder, computes the next free
number from `docs/decisions/`, renames the file, and rewrites every reference
(header, README row, any other Markdown file linking to the placeholder's old
filename). It is safe to run more than once — a second run after a successful
assignment finds no placeholder left and refuses cleanly rather than reassigning.
See `docs/decisions/0027-adr-number-allocation.md` for the full mechanism and the
rejected alternatives, and `tests/tooling/test_assign_adr_number.sh` for the
concurrent-authoring case this fixes.

`tools/adr-index-check.sh` (run by `tools/orchestration-policy.sh`) still checks the
resulting index for a duplicate number, a gap, or a row/file mismatch — that guard is
unchanged by this convention and continues to run as the after-the-fact drift check,
not a replacement for assigning the number correctly in the first place.

## What verification passes should look for

`docs/source-handling.md` lists the defect classes that have actually bitten this
project — unchecked assertions, misattributed citations, contaminated inheritance,
implementing prose over a printed table, and silently matching a misprint. Every one has
occurred at least once. Brief verification agents against that list rather than leaving
them to invent their own.

## The pipeline for a rules-engine change

```
Issue (ready)
  -> engine-dev            implement, one concern
  -> scope-warden          cheap checklist gate
  -> rules-conformance     verify every printed row
  -> codex conformance     independent cross-check   [core rules only]
  -> PR review + merge
```

For non-rules work — tooling, CLI, docs, case data — the route is `ci`-only: no
semantic reviewer runs at all, not even `scope-warden` (it is a `rules`/`formulas`
gate). The expensive verification layers exist for one specific risk and buy nothing
elsewhere. See [`docs/orchestration/routing.md`](orchestration/routing.md) for the
route → required-gates table.

## Briefing agents efficiently

Routing to the right model is only half the saving. The other half is not making the
agent spend its context rediscovering things you already know.

Measured on #9, where `scope-warden` spent ~52k tokens on a diff worth ~12k. The
remaining ~40k went to *locating* the change across 28 tool calls and re-reading its
own contract — not to reading the code.

**Hand the agent the diff. Do not make it assemble one.** Put the output of
`git diff` (or the file list) directly in the prompt. Discovery turns are the single
largest avoidable cost in a review agent.

**Use `-U1` for review diffs.** It is free and it trims four lines of context per
hunk on edit-heavy changes. Do not expect much on commits that are mostly new files —
git prints added files in full regardless of the context setting, so `-U1` saved
0.24% on #9 and would have saved far more on a scattered refactor. Reach for `-U0`
only when the agent is pattern-matching rather than reading for meaning.

**For a pure pattern checklist, skip the diff entirely.** `scope-warden` answers
questions of the form "does this banned token appear". `git grep` over the changed
paths answers those far more cheaply than reading any diff, and without the risk of a
hunk boundary hiding a match.

**Scope what the agent must read.** Name the files it needs. An agent told to "check
the change" will read the contract, the scope filter, the plan, and then go looking;
an agent given the diff and one checklist reads two things.

**Say what is already done.** Listing the completed pieces of an Issue, and stating
plainly not to restructure them, prevents the agent from re-deriving decisions that
are already settled — and from politely rewriting working code.

## What this is expected to save

The guide this workflow follows estimates 35-60% token reduction per completed task
at full adoption. The model-routing layer is additive to that: the bulk of engine
work is transcription and bounded implementation, which does not need frontier
reasoning, while the small fraction that does gets more of it than a uniform setting
would allow.

Measure it rather than assuming it. Track, over ten tasks: agent turns from task
selection to merged PR, clarification questions asked, and tasks reopened because
acceptance criteria were incomplete.

### Datapoints so far

| Task | Agents | Subagent tokens | Outcome |
|---|---|---:|---|
| #9 entropy and dice | `engine-dev` (Sonnet), `scope-warden` (Haiku) | ~128k + ~52k | Merged first try; 144 tests; no rework |

Both would otherwise have run at Opus in the main loop. Note the `scope-warden` cost
is inflated by the briefing problem described above and should fall substantially
once the diff is passed in rather than discovered.
