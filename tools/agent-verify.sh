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
# Binding semantic verdicts to the head SHA + review packet (Issue #205): a gate
# supplied as a naked `--gate NAME=pass` is an unverifiable assertion — nothing
# proves that verdict was produced against THIS head and THIS review packet, so an
# accidentally reused pass could ride onto a new SHA. `--gate-evidence FILE`
# consumes a verdict file written by tools/gate-evidence.sh and REFUSES unless the
# file's recorded head SHA still equals the current PR head AND a freshly
# regenerated review packet (agent-brief.py review — deterministic) reproduces the
# recorded packet hash. Bound gates are marked `bound:true` in the evidence object;
# naked `--gate` gates are `bound:false`. `--post` will not mint `success` while any
# required non-`ci` pass gate is unbound, unless `--allow-unbound-gates` is given
# (recorded as `unboundGatesAllowed:true`). This is a tightening of the existing
# authority, not new orchestration machinery — gate-evidence.sh only builds input.
#
# Usage:
#   tools/agent-verify.sh <PR#> [--gate NAME=pass|fail|skip ...]
#                               [--gate-evidence FILE ...] [--allow-unbound-gates]
#                               [--base REF] [--post] [--wait] [--evidence]
#                               [--json] [--json-out FILE]
#
#   * `ci` is read from GitHub (the `build-and-test` check-run) — never supplied.
#   * every OTHER required gate must be supplied via --gate or --gate-evidence; a
#     missing one leaves the aggregate `pending` (success is never posted on
#     incomplete evidence). Prefer --gate-evidence: it binds the verdict to the
#     head SHA + review packet; --gate stays for dry-runs / skip / fail / override.
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

PR=""; BASE="origin/main"; POST=0; EVIDENCE=0; JSON=0; JSON_OUT=""; WAIT=0; ALLOW_UNBOUND=0
declare -A GATE=()
declare -a GATE_EVIDENCE_FILES=()
while [ $# -gt 0 ]; do
  case "$1" in
    --gate) [ $# -ge 2 ] || die "--gate needs NAME=STATE"
            n="${2%%=*}"; s="${2#*=}"
            case "$s" in pass|fail|skip) ;; *) die "gate state must be pass|fail|skip: $2" ;; esac
            GATE["$n"]="$s"; shift 2 ;;
    --gate-evidence) [ $# -ge 2 ] || die "--gate-evidence needs FILE"
            GATE_EVIDENCE_FILES+=("$2"); shift 2 ;;
    --allow-unbound-gates) ALLOW_UNBOUND=1; shift ;;
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
[ -n "$PR" ] || die "usage: agent-verify.sh <PR#> [--gate NAME=STATE ...] [--gate-evidence FILE ...] [--allow-unbound-gates] [--post] [--wait] [--evidence] [--json] [--json-out FILE]"

# agent-brief.py, used to regenerate the review packet hash for --gate-evidence
# freshness checks. Overridable (tests stub it) but defaults to the repo tool.
AGENT_BRIEF="${AGENT_VERIFY_AGENT_BRIEF:-$ROOT/tools/agent-brief.py}"

# Per-gate binding provenance (parallel to GATE/RESULT), populated from
# --gate-evidence files below. BOUND defaults to 0 for any gate not bound.
declare -A BOUND=() EV_HEADSHA=() EV_RPS=() EV_SPS=() EV_REVIEWER=() EV_MODEL=()

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

# --- consume --gate-evidence files and bind them to head + review packet --- #
# A gate-evidence file (tools/gate-evidence.sh) carries a verdict plus the head
# SHA and review-packet hash it was produced against. We accept its verdict ONLY
# if both still hold for the current PR: the recorded head equals the PR head,
# and a freshly regenerated review packet reproduces the recorded packet hash.
# This is the mechanical freshness the naked `--gate` path lacks (Issue #205).
CURRENT_RPS=""; CURRENT_RPS_COMPUTED=0
current_review_packet_sha() {
  # Regenerate the review packet for this PR and return its packet-sha256 footer.
  # agent-brief.py is deterministic (no timestamp), so the same repo/PR state
  # reproduces the same hash the reviewer's packet carried. Computed once.
  if [ "$CURRENT_RPS_COMPUTED" = 0 ]; then
    CURRENT_RPS="$("$AGENT_BRIEF" review "$PR" 2>/dev/null \
      | grep -E '^packet-sha256:[[:space:]]*[0-9a-f]+' | tail -1 | awk '{print $2}')" || true
    CURRENT_RPS_COMPUTED=1
  fi
  printf '%s' "$CURRENT_RPS"
}

for f in ${GATE_EVIDENCE_FILES[@]+"${GATE_EVIDENCE_FILES[@]}"}; do
  [ -n "$f" ] || continue
  [ -f "$f" ] || die "--gate-evidence file not found: $f"
  # Parse the file into TSV: gate, verdict, headSha, rps, sps, reviewer, model.
  IFS=$'\t' read -r eg ev eh erps esps erev emod < <(python3 - "$f" <<'PYEOF'
import json, sys
with open(sys.argv[1]) as fh:
    d = json.load(fh)
def g(k):
    v = d.get(k)
    return "" if v is None else str(v)
print("\t".join([g("gate"), g("verdict"), g("headSha"), g("reviewPacketSha256"),
                 g("sourcePacketSha256"), g("reviewer"), g("model")]))
PYEOF
  ) || die "cannot parse gate-evidence file (not valid JSON?): $f"

  [ -n "$eg" ] || die "gate-evidence $f: missing 'gate'"
  case "$ev" in pass|fail|skip) ;; *) die "gate-evidence $f: verdict must be pass|fail|skip (got '${ev:-<empty>}')" ;; esac
  printf '%s\n' "${REQUIRED[@]}" | grep -qx "$eg" || die "gate-evidence $f: gate '$eg' is not required by route '$ROUTE_NAME'"
  [ "$eg" = ci ] && die "gate-evidence $f: ci is read from GitHub, not supplied as evidence"
  [ -n "${GATE[$eg]+x}" ] && die "gate '$eg' supplied both as --gate and --gate-evidence — use one"
  [ -n "${BOUND[$eg]+x}" ] && die "gate '$eg' supplied by more than one --gate-evidence file"

  # Freshness 1: the head the verdict was bound to must still be the PR head.
  [ "$eh" = "$HEAD_SHA" ] || die "gate-evidence $f: recorded headSha ${eh:0:7} != current PR head ${HEAD_SHA:0:7} — this verdict is STALE (produced against a superseded commit). Re-run gate '$eg' on the current head and rebuild its evidence."
  # Freshness 2: the review packet the reviewer saw must still regenerate.
  cur="$(current_review_packet_sha)"
  [ -n "$cur" ] || die "cannot regenerate the review packet for PR #$PR (agent-brief.py review) to verify gate-evidence freshness"
  [ "$erps" = "$cur" ] || die "gate-evidence $f: recorded reviewPacketSha256 ${erps:0:12} != freshly regenerated ${cur:0:12} — the review packet gate '$eg' saw no longer matches this PR (diff/claim/issue changed). Re-run the gate on the current packet."

  GATE["$eg"]="$ev"
  BOUND["$eg"]=1
  EV_HEADSHA["$eg"]="$eh"; EV_RPS["$eg"]="$erps"; EV_SPS["$eg"]="$esps"
  EV_REVIEWER["$eg"]="$erev"; EV_MODEL["$eg"]="$emod"
done

# Any gate supplied via naked --gate (not evidence) is unbound.
for n in "${!GATE[@]}"; do [ -n "${BOUND[$n]+x}" ] || BOUND["$n"]=0; done

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

# Per-gate binding provenance, one TSV record per non-ci required gate, passed to
# the builder via the environment so tabs survive intact:
#   gate \t bound(0|1) \t headSha \t reviewPacketSha256 \t sourcePacketSha256 \t reviewer \t model
PROV_TSV=""
for g in "${REQUIRED[@]}"; do
  [ "$g" = ci ] && continue
  PROV_TSV+="$g"$'\t'"${BOUND[$g]:-0}"$'\t'"${EV_HEADSHA[$g]:-}"$'\t'"${EV_RPS[$g]:-}"$'\t'"${EV_SPS[$g]:-}"$'\t'"${EV_REVIEWER[$g]:-}"$'\t'"${EV_MODEL[$g]:-}"$'\n'
done

EVIDENCE_JSON="$(PROV_TSV="$PROV_TSV" python3 - "$PR" "$ISSUE_NUM" "$HEAD_SHA" "$ROUTE_NAME" "$STATE" "$GATES_KV" "$ALLOW_UNBOUND" <<'PYEOF'
import json
import os
import sys

pr, issue, head_sha, route, aggregate, gates_kv, allow_unbound = sys.argv[1:8]
gates = {}
required = []
for pair in gates_kv.split(";"):
    if not pair:
        continue
    name, state = pair.split("=", 1)
    gates[name] = state
    required.append(name)

gate_evidence = {}
for line in os.environ.get("PROV_TSV", "").splitlines():
    if not line:
        continue
    parts = line.split("\t")
    while len(parts) < 7:
        parts.append("")
    name, bound, hsha, rps, sps, reviewer, model = parts[:7]
    gate_evidence[name] = {
        "bound": bound == "1",
        "headSha": hsha or None,
        "reviewPacketSha256": rps or None,
        "sourcePacketSha256": sps or None,
        "reviewer": reviewer or None,
        "model": model or None,
    }

evidence = {
    "schemaVersion": 2,
    "pr": int(pr) if pr.isdigit() else pr,
    "issue": int(issue) if issue.isdigit() else None,
    "headSha": head_sha,
    "route": route,
    "requiredGates": required,
    "gates": gates,
    "gateEvidence": gate_evidence,
    "unboundGatesAllowed": allow_unbound == "1",
    "aggregate": aggregate,
}
print(json.dumps(evidence, indent=2))
PYEOF
)"

# Required non-ci gates that PASSED but are unbound (naked --gate). These are the
# accidental-error hole from Issue #205: a success that isn't tied to head+packet.
UNBOUND_PASS=()
for g in "${REQUIRED[@]}"; do
  [ "$g" = ci ] && continue
  [ "${RESULT[$g]}" = pass ] && [ "${BOUND[$g]:-0}" = 0 ] && UNBOUND_PASS+=("$g")
done

# --- render the plan (suppressed in --json mode so stdout is pure JSON) --- #
if [ "$JSON" = 0 ]; then
  echo "PR #$PR  head=$HEAD_SHA  route=$ROUTE_NAME"
  echo "status: $CONTEXT = $STATE  ($DESC)"
  for g in "${REQUIRED[@]}"; do
    b=""
    if [ "$g" != ci ]; then
      case "${BOUND[$g]:-0}" in 1) b="  [bound]" ;; *) [ -n "${GATE[$g]+x}" ] && b="  [unbound]" ;; esac
    fi
    printf '  %-20s %s%s\n' "$g" "${RESULT[$g]}" "$b"
  done
  if [ "${#UNBOUND_PASS[@]}" -gt 0 ] && [ "$ALLOW_UNBOUND" = 0 ]; then
    echo "WARNING: unbound pass gate(s): ${UNBOUND_PASS[*]} — --post will refuse to mint success"
    echo "         (bind them via tools/gate-evidence.sh + --gate-evidence, or pass --allow-unbound-gates)"
  fi
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
gate_ev = ev.get("gateEvidence", {})
passed = sum(1 for g in required if gates[g] == "pass")
total = len(required)


def binding(g):
    if g == "ci":
        return "GitHub `build-and-test` @ this SHA"
    ge = gate_ev.get(g)
    if ge and ge.get("bound"):
        rps = (ge.get("reviewPacketSha256") or "")[:12]
        who = ge.get("reviewer") or g
        return "bound — %s, packet `%s`" % (who, rps)
    return "unbound (`--gate` assertion)"


out = []
out.append("<!-- agent-verification:start -->")
out.append("### agent-verification — `%s` for `%s`" % (aggregate, head7))
out.append("")
out.append("| gate | result | evidence |")
out.append("|---|---|---|")
for g in required:
    out.append("| `%s` | %s | %s |" % (g, gates[g], binding(g)))
out.append("")
out.append("_%d/%d gates passed [route: %s]. Regenerated per head SHA by "
            "`tools/agent-verify.sh`; semantic verdicts bound to head + review "
            "packet via `tools/gate-evidence.sh`._" % (passed, total, route))
if ev.get("unboundGatesAllowed"):
    out.append("")
    out.append("> ⚠️ `--allow-unbound-gates` was used: one or more semantic "
               "verdicts were accepted without head/packet binding.")
out.append("<!-- agent-verification:end -->")
print("\n".join(out))
'
}

if [ "$POST" = 1 ]; then
  # F10 first: is the head we would post onto even the real branch tip?
  converged || die "refusing to post: gh reports head $HEAD_SHA for branch $HEAD_REF_NAME but git's actual tip is ${GIT_HEAD_SHA:-<unknown>} — the gh API is lagging behind git (burn-in F10); acting now would post onto a stale/superseded head. Re-run (optionally with --wait) once they converge."
  # #205 next: even on the real head, a success must be tied to {head SHA + review
  # packet} for every semantic gate, unless the operator explicitly overrides
  # (recorded as unboundGatesAllowed in the object).
  if [ "$STATE" = success ] && [ "${#UNBOUND_PASS[@]}" -gt 0 ] && [ "$ALLOW_UNBOUND" = 0 ]; then
    die "refusing to post success: gate(s) [${UNBOUND_PASS[*]}] passed but are UNBOUND — supplied via --gate, so their verdict is not mechanically tied to head ${HEAD_SHA:0:7} + the review packet (Issue #205). Re-run each gate on the current head and supply tools/gate-evidence.sh output via --gate-evidence, or pass --allow-unbound-gates to override (the override is recorded in the evidence)."
  fi
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
