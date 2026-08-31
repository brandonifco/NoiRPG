# Auto-dispatching verification gates

`tools/dispatch-gates.sh <pr>` is the piece that turns the routing state machine
from advisory into enforcing: for a PR it derives the route's required gates, runs
each gate's agent, and posts the result as a check-run via the gate-poster App —
so [`gates-satisfied`](routing.md) reflects real per-PR verification with no human
in the loop.

```bash
tools/dispatch-gates.sh 76
DRY_RUN=1 tools/dispatch-gates.sh 76      # print what would run; post nothing
```

## What it does

1. Resolves the PR head/base and derives the route via `tools/route.sh`.
2. Assembles the review packet once (via [`agent-brief`](agent-brief.md); falls back
   to a minimal title+route+diff packet if agent-brief is absent).
3. Runs each required non-`ci` gate, **cheap-first**, stopping on the first FAIL
   (the same cheap-before-expensive principle as the model routing):

   | Gate | Runner | Model |
   |---|---|---|
   | `scope-warden` | `claude -p` | Haiku |
   | `rules-conformance` | `claude -p` | Opus |
   | `codex-conformance` | `tools/codex-agent.sh conformance` | Codex (cross-vendor) |
   | `architecture-review` | `claude -p` | Opus (design-critic lens) |

4. Parses the agent's trailing `VERDICT: PASS|FAIL` and posts a check-run named
   after the gate via `tools/gate-check.py` (success / failure / neutral).

## Requirements for a live run

- **App env** for posting: `GH_APP_ID`, `GH_APP_PRIVATE_KEY` (see [github-app.md](github-app.md)).
- **Authenticated runners on the host**: the `claude` CLI logged in, and `codex-agent.sh`
  configured (`CODEX_BIN`). These run where the orchestrator runs, not in Actions.
- `DRY_RUN=1` needs none of the above — it prints the plan and posts nothing.

## Closing the loop (phase-2 enforcement)

Once this dispatcher posts gate results reliably on every PR, make `gates-satisfied`
a **required** status check on `main` (it is currently reported-only) so a PR cannot
merge until its route's gates are green:

```bash
# add gates-satisfied alongside build-and-test in the main ruleset
gh api repos/{owner}/{repo}/rulesets/21893600 --jq '.rules[] | select(.type=="required_status_checks")'
# then PUT the ruleset with {"context":"gates-satisfied"} added to required_status_checks
```

At that point the loop is fully closed: Issue ready → PR → route → gates dispatched →
posted → aggregated → auto-merge, with the human only setting product intent.
