# agent-verification — one required status for the whole gate set

[`tools/route.sh`](../../tools/route.sh) already says which reviewers a change
needs. But after #90/#91 removed the gate-poster App and the `gates-satisfied`
aggregator, that derivation was *computed and then ignored*: the model-driven gates
(`scope-warden`, `rules-conformance`, `codex-conformance`, `architecture-review`)
posted nothing on a PR, so GitHub could only gate on `build-and-test`, `pr-policy`,
and `orchestration-policy`.

Codex presence (needed for the `codex-conformance` gate) is checked via
`CODEX_BIN` — run `tools/codex-agent.sh --check` (or `preflight`) — never via
`which codex`, which does not find it (see burn-in finding F5(a)).

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

# 2. For each semantic gate, record a verdict BOUND to the head + review packet.
#    (build the review packet the reviewer actually saw first)
tools/agent-brief.py review <PR#> > /tmp/review.md
tools/gate-evidence.sh --pr <PR#> --gate scope-warden --verdict pass \
  --review-packet /tmp/review.md --model haiku --out /tmp/sw.json
tools/gate-evidence.sh --pr <PR#> --gate rules-conformance --verdict pass \
  --review-packet /tmp/review.md --model opus --out /tmp/rc.json

# 3. Post the aggregate status + a per-gate evidence block in the PR body.
tools/agent-verify.sh <PR#> \
  --gate-evidence /tmp/sw.json \
  --gate-evidence /tmp/rc.json \
  --post --evidence
```

- `ci` is **read from GitHub** (the `build-and-test` check-run on the head SHA), never
  supplied by hand.
- Every *other* required gate must be supplied via `--gate-evidence FILE` (preferred)
  or `--gate NAME=pass|fail|skip`. A gate the route does not require is rejected (a
  typo or a stale assumption); a required gate left unsupplied leaves the aggregate
  `pending`, so **success is never posted on incomplete evidence**.
- Default is a dry run. `--post` posts the status; `--evidence` also writes a managed
  `<!-- agent-verification -->` block into the PR body for humans (now with a per-gate
  binding column).

## Binding a semantic verdict to the head + review packet (#205)

`agent-verification` is bound to the SHA by construction, but a verdict fed in as a
naked `--gate scope-warden=pass` is an *unverifiable assertion*: nothing proves it was
produced against **this** head and **this** review packet, so an accidentally reused
pass could ride onto a new commit. That was the review's remaining accidental-error
hole — a tired orchestrator, not a malicious one, is enough to trigger it.

[`tools/gate-evidence.sh`](../../tools/gate-evidence.sh) closes it by recording a
verdict as `{gate, verdict, headSha, reviewPacketSha256, sourcePacketSha256, reviewer,
model}`, and `agent-verify.sh --gate-evidence FILE` **refuses** the verdict unless:

- `headSha` still equals the current PR head; and
- a freshly regenerated `agent-brief.py review <pr>` reproduces the recorded
  `reviewPacketSha256` (agent-brief is deterministic — no timestamp — and the appended
  `<!-- agent-verification -->` body block is section-less, so it does not perturb the
  hash). A changed diff/claim/issue changes the hash, which correctly forces a re-review.

`sourcePacketSha256` (codex conformance) is **recorded** for provenance but not
re-validated: the source PDF is pinned by `orchestration-policy`, but the page range
is not recoverable at verify time. Bound gates are `bound:true` in the evidence object;
naked `--gate` gates are `bound:false`. `--post` will **not** mint `success` while any
required non-`ci` pass gate is unbound, unless `--allow-unbound-gates` is passed —
which is itself recorded as `unboundGatesAllowed:true`, so the override is never silent.

`gate-evidence.sh` builds input only; it mints nothing. `agent-verify.sh` remains the
one dynamic verification-evidence authority (#136) — this is a tightening of that
interface, not a second authority or new control plane.
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
