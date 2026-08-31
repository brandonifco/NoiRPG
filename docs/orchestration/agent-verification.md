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
- Run it with the PR branch checked out. From another branch it degrades to a
  path-only route (GitHub's changed-file list) and says so — the `rules → formulas`
  content-escalation can't be seen without the diff.

## Making it required (a deliberate flip)

Posting the status is safe and additive. **Requiring** it is a separate decision with
real blast radius: once `agent-verification` is a required check, every PR blocks
until the orchestrator has run the gates and posted the status. Do it only once the
orchestrator reliably posts on every PR (otherwise merges hang). To require it, add
the context to the branch ruleset's `required_status_checks` alongside
`build-and-test` / `pr-policy` / `orchestration-policy`:

```bash
# contexts must match byte-for-byte; confirm on a live PR first
gh api repos/:owner/:repo/rulesets/<id> --jq '.rules[]|select(.type=="required_status_checks")'
```

Until then it is an informative, non-blocking status — the enforcement half of the
route derivation, ready to be switched on.
