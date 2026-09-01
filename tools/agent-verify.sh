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
# This script is the ONE dynamic verification-evidence authority in the repo (see
# #136). pr_policy.py emits static PR metadata (route, required gates, body
# policy) and never claims a gate passed; the canonical {gates, aggregate} record
# for a head SHA is built here, once, and both the machine (--json) and human
# (--evidence) renderings come from that same in-memory object.
#
# Usage:
#   tools/agent-verify.sh <PR#> [--gate NAME=pass|fail|skip ...] [--base REF]
#                               [--post] [--wait] [--evidence] [--json] [--json-out FILE]
#
#   * `ci` is read from GitHub (the `build-and-test` check-run) — never supplied.
#   * every OTHER required gate must be supplied via --gate; a missing one leaves
#     the aggregate `pending` (success is never posted on incomplete evidence).
#   * default is a DRY RUN that prints the plan; --post actually posts the status;
#     --evidence additionally writes the per-gate block into the PR body.
#   * --json prints the canonical evidence object to stdout as JSON (nothing else
#     is written to stdout in that mode); --json-out FILE writes it to a file.
#   * --post refuses (clear message, non-zero) if the gh-reported PR head
#     disagrees with the branch's actual git ref — the gh API can lag several
#     polls behind git after a push/update-branch (burn-in F10), and acting on
#     that stale read risks posting the status onto a superseded head.
#     --wait polls (gh, then git) until the two converge before proceeding; the
#     dry-run path (no --post) is unaffected by the guard, though it still runs
#     under --wait if requested.
#
# Exit: 0 if the aggregate is success, 1 otherwise (so a caller can branch on it).
set -euo pipefail

CONTEXT="agent-verification"
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
die() { echo "agent-verify: $*" >&2; exit 2; }

PR=""; BASE="origin/main"; POST=0; EVIDENCE=0; JSON=0; JSON_OUT=""; WAIT=0
declare -A GATE=()
while [ $# -gt 0 ]; do
  case "$1" in
    --gate) [ $# -ge 2 ] || die "--gate needs NAME=STATE"
            n="${2%%=*}"; s="${2#*=}"
            case "$s" in pass|fail|skip) ;; *) die "gate state must be pass|fail|skip: $2" ;; esac
            GATE["$n"]="$s"; shift 2 ;;
    --base) BASE="${2:-}"; shift 2 ;;
    --post) POST=1; shift ;;
    --wait) WAIT=1; shift ;;
    --evidence) EVIDENCE=1; shift ;;
    --json) JSON=1; shift ;;
    --json-out) [ $# -ge 2 ] || die "--json-out needs FILE"; JSON_OUT="$2"; shift 2 ;;
    -*) die "unknown option: $1" ;;
    *)  [ -z "$PR" ] || die "unexpected argument: $1"; PR="$1"; shift ;;
  esac
done
[ -n "$PR" ] || die "usage: agent-verify.sh <PR#> [--gate NAME=STATE ...] [--post] [--wait] [--evidence] [--json] [--json-out FILE]"

# --- resolve the PR ------------------------------------------------------- #
read_pr_head() {
  read -r HEAD_SHA HEAD_REF_NAME PR_URL PR_STATE < <(gh pr view "$PR" \
    --json headRefOid,headRefName,url,state \
    --jq '[.headRefOid, .headRefName, .url, .state] | @tsv') \
    || die "cannot read PR #$PR"
}
read_pr_head
[ -n "$HEAD_SHA" ] || die "PR #$PR has no head SHA"
[ -n "$HEAD_REF_NAME" ] || die "PR #$PR has no head branch name"

# --- converged-head guard (burn-in F10) ------------------------------------ #
# `gh pr view --json headRefOid` can lag several polls behind the branch's
# real git tip right after a push/update-branch. Acting on that stale read —
# posting `agent-verification`, or merging — risks silently targeting a
# SUPERSEDED head. Treat git, the actual mover of the ref, as ground truth:
# re-derive the branch's tip locally and refuse unless it agrees with gh.
#
# Prefers an already-known remote-tracking ref (no network) and only falls
# back to a fetch if the ref is not present locally yet, mirroring the BASE
# resolution above.
GIT_HEAD_SHA=""
git_head_for_ref() {
  local ref="$1" sha
  sha="$(git -C "$ROOT" rev-parse --quiet --verify "origin/$ref" 2>/dev/null || true)"
  if [ -z "$sha" ]; then
    git -C "$ROOT" fetch -q origin "$ref" >/dev/null 2>&1 || true
    sha="$(git -C "$ROOT" rev-parse --quiet --verify "origin/$ref" 2>/dev/null || true)"
  fi
  printf '%s' "$sha"
}
converged() {
  GIT_HEAD_SHA="$(git_head_for_ref "$HEAD_REF_NAME")"
  [ -n "$GIT_HEAD_SHA" ] && [ "$GIT_HEAD_SHA" = "$HEAD_SHA" ]
}

if [ "$WAIT" = 1 ]; then
  POLL_ATTEMPTS="${AGENT_VERIFY_POLL_ATTEMPTS:-12}"
  POLL_INTERVAL="${AGENT_VERIFY_POLL_INTERVAL:-5}"
  attempt=1
  until converged; do
    [ "$attempt" -ge "$POLL_ATTEMPTS" ] && \
      die "gave up waiting for gh/git head convergence on PR #$PR after $POLL_ATTEMPTS polls (gh head=$HEAD_SHA, git head=${GIT_HEAD_SHA:-<none>} for branch $HEAD_REF_NAME); the gh API is still lagging behind git (see burn-in F10) — refusing to proceed"
    sleep "$POLL_INTERVAL"
    attempt=$((attempt + 1))
    read_pr_head
  done
fi

# --- resolve the linked issue (best-effort; used both for the route's issue-
# intent floor and for the evidence object) -------------------------------- #
# Same "Closes/Fixes/Resolves #N" convention pr_policy.py enforces on the body;
# this gh CLI version has no closingIssuesReferences JSON field to read instead.
PR_BODY="$(gh pr view "$PR" --json body --jq '.body // ""' 2>/dev/null || true)"
ISSUE_NUM="$(printf '%s' "$PR_BODY" | python3 -c '
import re, sys
m = re.search(r"(?i)\b(clos|fix|resolv)(e|es|ed)?\s+#(\d+)", sys.stdin.read())
print(m.group(3) if m else "")
')"

# --- derive the required gate set from the route -------------------------- #
# The route must reflect the PR's OWN diff, not whatever is checked out. When
# the working tree is on the PR head we use route.sh --base (full, incl. the
# numeric content-escalation). Otherwise we fetch the actual PR patch with
# `gh pr diff` and classify THAT through route.sh --diff-file — the same
# content-escalation and issue-intent logic route.sh applies to a local diff,
# never a path-only degrade (see #137: route.sh is the one route authority).
git -C "$ROOT" rev-parse --verify --quiet "$BASE" >/dev/null 2>&1 \
  || git -C "$ROOT" fetch -q origin "${BASE#origin/}" 2>/dev/null || true
LOCAL_HEAD="$(git -C "$ROOT" rev-parse --quiet --verify HEAD 2>/dev/null || echo none)"
PR_DIFF_FILE=""
cleanup_diff_file() { [ -n "$PR_DIFF_FILE" ] && rm -f "$PR_DIFF_FILE"; }
trap cleanup_diff_file EXIT
ROUTE_ARGS=(--json)
if [ "$LOCAL_HEAD" = "$HEAD_SHA" ]; then
  ROUTE_ARGS+=(--base "$BASE")
else
  PR_DIFF_FILE="$(mktemp)"
  gh pr diff "$PR" > "$PR_DIFF_FILE" 2>/dev/null || die "cannot fetch PR #$PR diff (and HEAD is not the PR head)"
  [ -s "$PR_DIFF_FILE" ] || die "PR #$PR diff is empty (and HEAD is not the PR head)"
  ROUTE_ARGS+=(--diff-file "$PR_DIFF_FILE")
fi
[ -n "$ISSUE_NUM" ] && ROUTE_ARGS+=(--issue "$ISSUE_NUM")
ROUTE_JSON="$(cd "$ROOT" && tools/route.sh "${ROUTE_ARGS[@]}" 2>/dev/null || echo '{}')"
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

# --- build the ONE canonical evidence object ------------------------------ #
# Both --json/--json-out and the human --evidence PR-body block render from this
# exact object — neither reconstructs gate state independently (see #136).
GATES_KV=""
for g in "${REQUIRED[@]}"; do GATES_KV+="$g=${RESULT[$g]};"; done
EVIDENCE_JSON="$(python3 - "$PR" "$ISSUE_NUM" "$HEAD_SHA" "$ROUTE_NAME" "$STATE" "$GATES_KV" <<'PYEOF'
import json
import sys

pr, issue, head_sha, route, aggregate, gates_kv = sys.argv[1:7]
gates = {}
required = []
for pair in gates_kv.split(";"):
    if not pair:
        continue
    name, state = pair.split("=", 1)
    gates[name] = state
    required.append(name)

evidence = {
    "schemaVersion": 1,
    "pr": int(pr) if pr.isdigit() else pr,
    "issue": int(issue) if issue.isdigit() else None,
    "headSha": head_sha,
    "route": route,
    "requiredGates": required,
    "gates": gates,
    "aggregate": aggregate,
}
print(json.dumps(evidence, indent=2))
PYEOF
)"

# --- render the plan (suppressed in --json mode so stdout is pure JSON) --- #
if [ "$JSON" = 0 ]; then
  echo "PR #$PR  head=$HEAD_SHA  route=$ROUTE_NAME"
  echo "status: $CONTEXT = $STATE  ($DESC)"
  for g in "${REQUIRED[@]}"; do printf '  %-20s %s\n' "$g" "${RESULT[$g]}"; done
fi

# Renders the human PR-body block FROM $EVIDENCE_JSON (via stdin), not by
# re-walking REQUIRED/RESULT a second time.
evidence_block() {
  printf '%s' "$EVIDENCE_JSON" | python3 -c '
import json
import sys

ev = json.load(sys.stdin)
aggregate = ev["aggregate"]
head7 = ev["headSha"][:7]
route = ev["route"]
required = ev["requiredGates"]
gates = ev["gates"]
passed = sum(1 for g in required if gates[g] == "pass")
total = len(required)

out = []
out.append("<!-- agent-verification:start -->")
out.append("### agent-verification — `%s` for `%s`" % (aggregate, head7))
out.append("")
out.append("| gate | result |")
out.append("|---|---|")
for g in required:
    out.append("| `%s` | %s |" % (g, gates[g]))
out.append("")
out.append("_%d/%d gates passed [route: %s]. Regenerated per head SHA by "
            "`tools/agent-verify.sh`._" % (passed, total, route))
out.append("<!-- agent-verification:end -->")
print("\n".join(out))
'
}

if [ "$POST" = 1 ]; then
  converged || die "refusing to post: gh reports head $HEAD_SHA for branch $HEAD_REF_NAME but git's actual tip is ${GIT_HEAD_SHA:-<unknown>} — the gh API is lagging behind git (burn-in F10); acting now would post onto a stale/superseded head. Re-run (optionally with --wait) once they converge."
  gh api -X POST "repos/{owner}/{repo}/statuses/$HEAD_SHA" \
    -f state="$STATE" -f context="$CONTEXT" -f description="$DESC" -f target_url="$PR_URL" \
    --jq '"posted: \(.context) = \(.state)"' || die "failed to post status"
  if [ "$EVIDENCE" = 1 ]; then
    # Strip any prior block, then append the fresh one. Update the body via the
    # REST API rather than `gh pr edit`, which fails on repos still carrying a
    # projects-classic deprecation (it queries projectCards and errors out).
    clean="$(printf '%s' "$PR_BODY" | sed '/<!-- agent-verification:start -->/,/<!-- agent-verification:end -->/d')"
    newbody="$(printf '%s\n\n%s' "$clean" "$(evidence_block)")"
    gh api -X PATCH "repos/{owner}/{repo}/pulls/$PR" -f body="$newbody" >/dev/null \
      && echo "evidence: PR #$PR body updated"
  fi
elif [ "$JSON" = 0 ]; then
  echo "(dry run — pass --post to post the commit status; --evidence to update the PR body)"
  [ "$EVIDENCE" = 1 ] && { echo "--- evidence block that would be written ---"; evidence_block; }
fi

[ -n "$JSON_OUT" ] && printf '%s\n' "$EVIDENCE_JSON" > "$JSON_OUT"
[ "$JSON" = 1 ] && printf '%s\n' "$EVIDENCE_JSON"

[ "$STATE" = success ] && exit 0 || exit 1
