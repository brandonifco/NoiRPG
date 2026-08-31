#!/usr/bin/env bash
# tools/dispatch-gates.sh <pr-number>
#
# The auto-dispatcher (#73): for a PR, derive the route's required verification
# gates, run each one's agent briefed with the #59 review packet, and post the
# result as a check-run via the gate-poster App (#65) — so the #62 aggregator's
# `gates-satisfied` reflects real per-PR verification with no human in the loop.
#
# Gates run cheap-first (scope-warden -> rules-conformance -> codex-conformance ->
# architecture-review); a FAIL stops the run before the more expensive gates, the
# same cheap-before-expensive principle the model routing uses.
#
#   tools/dispatch-gates.sh 76
#   DRY_RUN=1 tools/dispatch-gates.sh 76     # print what would run; post nothing
#
# Live runs need the App env (GH_APP_ID, GH_APP_PRIVATE_KEY) for posting, and the
# agent runners on PATH (`claude`; `codex-agent.sh` for the Codex gate).
set -euo pipefail
cd "$(git rev-parse --show-toplevel)"

PR="${1:?usage: dispatch-gates.sh <pr-number>}"
DRY="${DRY_RUN:-0}"

HEAD="$(gh pr view "$PR" --json headRefOid --jq .headRefOid)"
BASE_BRANCH="$(gh pr view "$PR" --json baseRefName --jq .baseRefName)"
git fetch --no-tags -q origin "refs/pull/$PR/head" "$BASE_BRANCH" 2>/dev/null || true
BASE="$(git merge-base "origin/$BASE_BRANCH" "$HEAD" 2>/dev/null || echo "origin/$BASE_BRANCH")"

ROUTE_JSON="$(tools/route.sh --base "$BASE" --json)"
GATES="$(printf '%s' "$ROUTE_JSON" | python3 -c "import sys,json; print(' '.join(json.load(sys.stdin).get('gates',[])))")"
echo "PR #$PR  head ${HEAD:0:10}  route $(printf '%s' "$ROUTE_JSON" | python3 -c 'import sys,json;print(json.load(sys.stdin).get("route"))')  gates: $GATES"

# Review packet — assembled once, shared as context by every gate. Prefer the
# rich packet from agent-brief (#59); fall back to a minimal one if it is absent.
if [ -x tools/agent-brief.py ]; then
  PACKET="$(tools/agent-brief.py review "$PR")"
else
  PACKET="$(printf '# REVIEW — PR #%s\nRoute: %s\n\n## DIFF (git diff -U1)\n```diff\n%s\n```\n' \
    "$PR" "$(printf '%s' "$ROUTE_JSON" | python3 -c 'import sys,json;print(json.load(sys.stdin).get("route"))')" \
    "$(git diff -U1 "$BASE"...HEAD)")"
fi

# Gate lenses (kept in sync with tools/agent-brief.py GATE_REVIEW).
lens_for() {
  case "$1" in
    scope-warden) echo "Check the diff against orc-scope-filter.md and docs/source-handling.md: no out-of-scope content, correct source, modern-era baselines, seeded randomness." ;;
    rules-conformance) echo "Verify every implemented value against the printed table in the cited ORC section. Assume wrong until each row proves out." ;;
    codex-conformance) echo "Independently RE-DERIVE each formula/threshold from the source text; do not reuse the implementer's reasoning." ;;
    architecture-review) echo "Review the new subsystem boundary and project references: layering holds, Brp.Core/Brp.Rules take no game-engine dependency." ;;
  esac
}

prompt_for() {
  printf '%s\n\n## YOUR GATE: %s\n%s\n\nEnd your reply with exactly one line `VERDICT: PASS` or `VERDICT: FAIL`, then a one-line summary.\n' \
    "$PACKET" "$1" "$(lens_for "$1")"
}

run_claude() { # model prompt
  if [ "$DRY" = 1 ]; then echo "[dry] claude -p --model $1 <briefed gate prompt>"; return 0; fi
  claude -p --model "$1" "$2" 2>/dev/null || true
}

run_gate() { # gate
  local gate="$1" out verdict summary conclusion
  echo; echo "== gate: $gate =="
  local prompt; prompt="$(prompt_for "$gate")"
  case "$gate" in
    scope-warden)        out="$(run_claude claude-haiku-4-5-20251001 "$prompt")" ;;
    rules-conformance)   out="$(run_claude claude-opus-4-8 "$prompt")" ;;
    architecture-review) out="$(run_claude claude-opus-4-8 "$prompt")" ;;
    codex-conformance)
      if [ "$DRY" = 1 ]; then echo "[dry] BASE=$BASE tools/codex-agent.sh conformance <briefed gate prompt>"; out=""; else
        out="$(BASE="$BASE" tools/codex-agent.sh conformance "$prompt" 2>/dev/null || true)"; fi ;;
    *) echo "unknown gate: $gate" >&2; return 0 ;;
  esac
  [ "$DRY" = 1 ] && return 0

  verdict="$(printf '%s' "$out" | grep -ioE 'VERDICT:[[:space:]]*(PASS|FAIL)' | tail -1 | grep -ioE 'PASS|FAIL' | tr 'A-Z' 'a-z')"
  summary="$(printf '%s' "$out" | grep -iA1 'VERDICT:' | tail -1 | cut -c1-280)"
  [ -z "$summary" ] && summary="$gate: see agent output"
  case "$verdict" in
    pass) conclusion=success ;;
    fail) conclusion=failure ;;
    *)    conclusion=neutral; summary="no explicit verdict parsed; $summary" ;;
  esac
  echo "verdict: ${verdict:-none} -> $conclusion"
  tools/gate-check.py --gate "$gate" --sha "$HEAD" --conclusion "$conclusion" --summary "$summary"
  [ "$conclusion" = failure ] && return 1 || return 0
}

# Cheap-first order; only run gates the route actually requires; stop on first FAIL.
for gate in scope-warden rules-conformance codex-conformance architecture-review; do
  case " $GATES " in *" $gate "*) ;; *) continue ;; esac
  if ! run_gate "$gate"; then
    echo; echo "STOP: $gate failed — skipping the remaining (more expensive) gates."
    exit 1
  fi
done
echo; echo "all required gates dispatched for PR #$PR"
