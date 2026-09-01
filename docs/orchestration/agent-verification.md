# agent-verification — one required status for the whole gate set

[`tools/route.sh`](../../tools/route.sh) already says which reviewers a change
needs. But after #90/#91 removed the gate-poster App and the `gates-satisfied`
aggregator, that derivation was *computed and then ignored*: the model-driven gates
(`scope-warden`, `rules-conformance`, `codex-conformance`, `architecture-review`)
posted nothing on a PR, so GitHub could only gate on `build-and-test`, `pr-policy`,
and `orchestration-policy`.

[`tools/agent-verify.sh`](../../tools/agent-verify.sh) closes that loop without
rebuilding the App fan-out. The **local orchestrator** runs the route's gates for the
PR head SHA and posts a single `agent-verification` **commit status** — success only
if `ci` passed *and* every other required gate was supplied as `pass`. GitHub then
gates on the *contract* ("did every required gate pass for this exact SHA?"), not on
how any model produced the answer.

## Why a commit status, not a check-run

A commit status binds to a SHA. Push a new commit and the `agent-verification`
status no longer exists for the new head, so a stale approval can never ride along
with changed code — the gate re-runs by construction. Branch protection can require
a status context exactly as it requires a check-run, so `agent-verification` can
become the single required external gate for the model-driven review set.

## How the orchestrator uses it

After running the gates for a PR (with its branch checked out, so the route — and
its numeric content-escalation — is derived from the PR's own diff):

```bash
# 1. See what the route requires and what's missing (dry run).
tools/agent-verify.sh <PR#>

# 2. Post the aggregate status + a per-gate evidence block in the PR body.
tools/agent-verify.sh <PR#> \
  --gate scope-warden=pass \
  --gate rules-conformance=pass \
  --post --evidence
```

- `ci` is **read from GitHub** (the `build-and-test` check-run on the head SHA), never
  supplied by hand.
- Every *other* required gate must be supplied via `--gate NAME=pass|fail|skip`. A
  gate the route does not require is rejected (a typo or a stale assumption); a
  required gate left unsupplied leaves the aggregate `pending`, so **success is never
  posted on incomplete evidence**.
- Default is a dry run. `--post` posts the status; `--evidence` also writes a managed
  `<!-- agent-verification -->` block into the PR body for humans.
- Works from any branch. When the working tree is on the PR head it routes from the
  local diff (`route.sh --base`); otherwise it fetches the full PR patch with
  `gh pr diff` and classifies it via `route.sh --diff-file`, so the `rules → formulas`
  content-escalation is applied either way — never a path-only degrade (see #137).

## Making it required (burned in, then flipped)

Posting the status is safe and additive; **requiring** it is a separate decision with
real blast radius — once `agent-verification` is a required check, every PR blocks
until the orchestrator has run the gates and posted the status for that exact head
SHA. Before requiring it, the status was burned in on real, normally-scoped PRs to
confirm the orchestrator posts reliably and the aggregate behaves correctly (pending
on missing gates, failure on a failed gate, invalidated by new commits). See
[`agent-verification-burn-in.md`](agent-verification-burn-in.md) for that evidence.

`agent-verification` is now in the `main` ruleset's `required_status_checks`,
alongside — not instead of — `build-and-test` / `pr-policy` / `orchestration-policy`:

```bash
# contexts must match byte-for-byte; confirm on a live PR first
gh api repos/:owner/:repo/rulesets/<id> --jq '.rules[]|select(.type=="required_status_checks")'
```

A PR now merges only when all four are green for its current head SHA — the route's
model-driven gate set is enforced, not just computed.

## Trust root and triage policy

Who is authorized to mint the `agent-verification` status, and how disagreeing or
adversarial semantic gates get triaged, is recorded in
[`docs/decisions/0025-agent-verification-trust-root.md`](../decisions/0025-agent-verification-trust-root.md).
In short: the local orchestrator's credential is the trust root (accidental-error
protection, not adversarial-agent protection, under the current solo threat model),
and value/behavior conformance defects block merge while unreachable-robustness,
style, and already-adjudicated findings are logged rather than blocking, with the
orchestrator adjudicating gate-vs-gate disagreement.
