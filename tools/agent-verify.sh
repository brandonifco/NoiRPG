#!/usr/bin/env bash
# tools/agent-verify.sh — collapse a PR's whole required-gate set into ONE commit
# status the branch protection can gate on: `agent-verification`.
#
# The route (tools/route.sh) already says which reviewers a change needs, but
# GitHub ignored that after #90/#91 removed the gate-poster App — the model-driven
# gates (scope-warden, rules-conformance, codex-conformance, architecture-review)
# were computed and never enforced. Rebuilding the App fan-out is the wrong shape:
# GitHub should gate on the *contract* (did every required gate pass for this exact
# SHA?), not on how any model produced the answer.
#
# So the local orchestrator runs the route's gates, and this script posts a single
# `agent-verification` commit status on the PR head SHA — success only if `ci`
# passed AND every other required gate was supplied as `pass`. Because a commit
# status binds to the SHA, any new push invalidates it automatically. Per-gate
# evidence is written into the PR body for humans.
#
# Usage:
#   tools/agent-verify.sh <PR#> [--gate NAME=pass|fail|skip ...] [--base REF]
#                               [--post] [--evidence]
#
#   * `ci` is read from GitHub (the `build-and-test` check-run) — never supplied.
#   * every OTHER required gate must be supplied via --gate; a missing one leaves
#     the aggregate `pending` (success is never posted on incomplete evidence).
#   * default is a DRY RUN that prints the plan; --post actually posts the status;
#     --evidence additionally writes the per-gate block into the PR body.
#
# Exit: 0 if the aggregate is success, 1 otherwise (so a caller can branch on it).
set -euo pipefail

CONTEXT="agent-verification"
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
die() { echo "agent-verify: $*" >&2; exit 2; }

PR=""; BASE="origin/main"; POST=0; EVIDENCE=0
declare -A GATE=()
while [ $# -gt 0 ]; do
  case "$1" in
    --gate) [ $# -ge 2 ] || die "--gate needs NAME=STATE"
            n="${2%%=*}"; s="${2#*=}"
            case "$s" in pass|fail|skip) ;; *) die "gate state must be pass|fail|skip: $2" ;; esac
            GATE["$n"]="$s"; shift 2 ;;
    --base) BASE="${2:-}"; shift 2 ;;
    --post) POST=1; shift ;;
    --evidence) EVIDENCE=1; shift ;;
    -*) die "unknown option: $1" ;;
    *)  [ -z "$PR" ] || die "unexpected argument: $1"; PR="$1"; shift ;;
  esac
done
[ -n "$PR" ] || die "usage: agent-verify.sh <PR#> [--gate NAME=STATE ...] [--post] [--evidence]"

# --- resolve the PR ------------------------------------------------------- #
read -r HEAD_SHA PR_URL PR_STATE < <(gh pr view "$PR" \
  --json headRefOid,url,state --jq '[.headRefOid, .url, .state] | @tsv') \
  || die "cannot read PR #$PR"
[ -n "$HEAD_SHA" ] || die "PR #$PR has no head SHA"

# --- derive the required gate set from the route -------------------------- #
# The route must reflect the PR's OWN diff, not whatever is checked out. When the
# working tree is on the PR head we use route.sh --base (full, incl. the numeric
# content-escalation); otherwise we degrade to GitHub's changed-file list, which
# is path-only — the rules->formulas content escalation cannot be seen — and say so.
git -C "$ROOT" rev-parse --verify --quiet "$BASE" >/dev/null 2>&1 \
  || git -C "$ROOT" fetch -q origin "${BASE#origin/}" 2>/dev/null || true
LOCAL_HEAD="$(git -C "$ROOT" rev-parse --quiet --verify HEAD 2>/dev/null || echo none)"
ROUTE_CAVEAT=""
if [ "$LOCAL_HEAD" = "$HEAD_SHA" ]; then
  ROUTE_JSON="$(cd "$ROOT" && tools/route.sh --json --base "$BASE" 2>/dev/null || echo '{}')"
else
  mapfile -t PR_FILES < <(gh pr diff "$PR" --name-only 2>/dev/null || true)
  [ "${#PR_FILES[@]}" -gt 0 ] || die "cannot list PR #$PR changed files (and HEAD is not the PR head)"
  ROUTE_JSON="$(cd "$ROOT" && tools/route.sh --json "${PR_FILES[@]}" 2>/dev/null || echo '{}')"
  ROUTE_CAVEAT=" (path-only: check out the PR head for content-escalation)"
fi
mapfile -t REQUIRED < <(printf '%s' "$ROUTE_JSON" | \
  python3 -c 'import sys,json;print("\n".join(json.load(sys.stdin).get("gates",[])))' 2>/dev/null || true)
ROUTE_NAME="$(printf '%s' "$ROUTE_JSON" | python3 -c 'import sys,json;print(json.load(sys.stdin).get("route","?"))' 2>/dev/null || echo '?')"
[ "${#REQUIRED[@]}" -gt 0 ] || die "could not derive required gates (route: $ROUTE_NAME)"

# Reject a --gate that the route does not require (a typo or a stale assumption).
for n in "${!GATE[@]}"; do
  printf '%s\n' "${REQUIRED[@]}" | grep -qx "$n" || die "--gate $n is not required by route '$ROUTE_NAME'"
  [ "$n" = ci ] && die "ci is read from GitHub, not supplied via --gate"
done

# --- ci: read the build-and-test check-run on the head SHA ---------------- #
ci_conclusion() {
  gh api "repos/{owner}/{repo}/commits/$HEAD_SHA/check-runs" \
    --jq '[.check_runs[] | select(.name=="build-and-test")]
          | sort_by(.started_at) | last | .conclusion // "pending"' 2>/dev/null || echo "unknown"
}

# --- assemble per-gate results -------------------------------------------- #
declare -A RESULT=()
overall="success"
for g in "${REQUIRED[@]}"; do
  if [ "$g" = ci ]; then
    c="$(ci_conclusion)"
    case "$c" in success) RESULT[$g]=pass ;; failure|cancelled|timed_out) RESULT[$g]=fail ;; *) RESULT[$g]=pending ;; esac
  else
    RESULT[$g]="${GATE[$g]:-pending}"
  fi
  case "${RESULT[$g]}" in
    pass) ;;
    fail) overall="failure" ;;
    *)    [ "$overall" = "failure" ] || overall="pending" ;;
  esac
done

passed=0; for g in "${REQUIRED[@]}"; do [ "${RESULT[$g]}" = pass ] && passed=$((passed+1)); done
total=${#REQUIRED[@]}
DESC="$passed/$total gates passed [route: $ROUTE_NAME]"

# GitHub commit-status states are: success | failure | pending | error.
STATE="$overall"

# --- render the plan ------------------------------------------------------ #
echo "PR #$PR  head=$HEAD_SHA  route=$ROUTE_NAME$ROUTE_CAVEAT"
echo "status: $CONTEXT = $STATE  ($DESC)"
for g in "${REQUIRED[@]}"; do printf '  %-20s %s\n' "$g" "${RESULT[$g]}"; done

evidence_block() {
  printf '<!-- agent-verification:start -->\n'
  printf '### agent-verification — `%s` for `%s`\n\n' "$STATE" "${HEAD_SHA:0:7}"
  printf '| gate | result |\n|---|---|\n'
  for g in "${REQUIRED[@]}"; do printf '| `%s` | %s |\n' "$g" "${RESULT[$g]}"; done
  printf '\n_%s. Regenerated per head SHA by `tools/agent-verify.sh`._\n' "$DESC"
  printf '<!-- agent-verification:end -->\n'
}

if [ "$POST" = 1 ]; then
  gh api -X POST "repos/{owner}/{repo}/statuses/$HEAD_SHA" \
    -f state="$STATE" -f context="$CONTEXT" -f description="$DESC" -f target_url="$PR_URL" \
    --jq '"posted: \(.context) = \(.state)"' || die "failed to post status"
  if [ "$EVIDENCE" = 1 ]; then
    body="$(gh pr view "$PR" --json body --jq '.body // ""')"
    # Strip any prior block, then append the fresh one. Update the body via the
    # REST API rather than `gh pr edit`, which fails on repos still carrying a
    # projects-classic deprecation (it queries projectCards and errors out).
    clean="$(printf '%s' "$body" | sed '/<!-- agent-verification:start -->/,/<!-- agent-verification:end -->/d')"
    newbody="$(printf '%s\n\n%s' "$clean" "$(evidence_block)")"
    gh api -X PATCH "repos/{owner}/{repo}/pulls/$PR" -f body="$newbody" >/dev/null \
      && echo "evidence: PR #$PR body updated"
  fi
else
  echo "(dry run — pass --post to post the commit status; --evidence to update the PR body)"
  [ "$EVIDENCE" = 1 ] && { echo "--- evidence block that would be written ---"; evidence_block; }
fi

[ "$STATE" = success ] && exit 0 || exit 1
