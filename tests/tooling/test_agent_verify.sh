#!/usr/bin/env bash
# tests/tooling/test_agent_verify.sh — fixture tests for tools/agent-verify.sh's
# canonical verification-evidence object (Issue #136).
#
# agent-verify.sh is the ONE dynamic verification-evidence authority: it reads
# the real build-and-test check-run from GitHub, combines it with the --gate
# evidence supplied for the route's other required gates, and must render the
# same {schemaVersion, pr, issue, headSha, route, requiredGates, gates, aggregate}
# object through --json/--json-out AND the human --evidence PR-body block.
#
# No `gh` CLI network access is available in this environment (or in CI), so
# this stubs `gh` with a fixture script that answers the exact calls
# agent-verify.sh makes, driven by MOCK_* environment variables.
#
# Run directly:
#   tests/tooling/test_agent_verify.sh
#
# Exit: 0 if every case passes, 1 on the first failure.
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
SCRIPT="$ROOT/tools/agent-verify.sh"

WORKDIR="$(mktemp -d)"
trap 'rm -rf "$WORKDIR"' EXIT

FAILURES=0
ok()   { printf 'ok   - %s\n' "$1"; }
fail() { printf 'FAIL - %s\n' "$1"; FAILURES=$((FAILURES + 1)); }

assert_eq() {
  local desc="$1" expected="$2" actual="$3"
  if [ "$expected" = "$actual" ]; then ok "$desc"; else
    fail "$desc (expected [$expected], got [$actual])"
  fi
}

assert_contains() {
  local desc="$1" haystack="$2" needle="$3"
  if [[ "$haystack" == *"$needle"* ]]; then ok "$desc"; else
    fail "$desc (expected to find [$needle])"
  fi
}

# --- fixture `gh` stub ------------------------------------------------------ #
# Answers exactly the gh subcommands agent-verify.sh issues: `pr view --json
# headRefOid,url,state`, `pr view --json body`, `pr diff --name-only`,
# `api .../check-runs`, `api -X POST .../statuses/...`, `api -X PATCH .../pulls/...`.
MOCKBIN="$WORKDIR/bin"
mkdir -p "$MOCKBIN"
cat > "$MOCKBIN/gh" <<'MOCKGH'
#!/usr/bin/env bash
set -euo pipefail

find_flag_value() {
  local flag="$1"; shift; local args=("$@")
  for ((i = 0; i < ${#args[@]}; i++)); do
    if [ "${args[$i]}" = "$flag" ]; then printf '%s' "${args[$((i + 1))]}"; return 0; fi
  done
  return 1
}

emit() {
  local body="$1"; shift; local jqf
  if jqf="$(find_flag_value --jq "$@")"; then printf '%s' "$body" | jq -r "$jqf"
  else printf '%s\n' "$body"; fi
}

HEAD_SHA="${MOCK_HEAD_SHA:-deadbeef1234567890deadbeef1234567890dead}"
PR_BODY="${MOCK_PR_BODY:-Closes #136}"
CI_CONCLUSION="${MOCK_CI_CONCLUSION:-success}"

case "$1 $2" in
  "pr view")
    jsonf="$(find_flag_value --json "$@" || true)"
    case "$jsonf" in
      headRefOid,url,state)
        emit "{\"headRefOid\":\"$HEAD_SHA\",\"url\":\"https://example.invalid/pull/999\",\"state\":\"OPEN\"}" "$@" ;;
      body)
        emit "{\"body\":\"$PR_BODY\"}" "$@" ;;
      *) echo "mock gh: unhandled pr view --json $jsonf" >&2; exit 1 ;;
    esac ;;
  "pr diff")
    # agent-verify.sh now fetches the FULL patch (no --name-only) and hands it
    # to route.sh --diff-file, never a path-only degrade (#137). MOCK_PR_DIFF
    # supplies a real unified diff verbatim; otherwise synthesize one non-numeric
    # hunk per MOCK_PR_FILES entry so the default fixture still behaves like a
    # boring change.
    if [ -n "${MOCK_PR_DIFF:-}" ]; then
      printf '%s\n' "$MOCK_PR_DIFF"
    else
      for f in ${MOCK_PR_FILES:-tools/agent-verify.sh}; do
        printf 'diff --git a/%s b/%s\nindex 1111111..2222222 100644\n--- a/%s\n+++ b/%s\n@@ -1,1 +1,1 @@\n-old line\n+new line\n' \
          "$f" "$f" "$f" "$f"
      done
    fi ;;
  *)
    case "$1" in
      api)
        shift
        [ "${1:-}" = "-X" ] && shift 2
        path="$1"
        case "$path" in
          repos/{owner}/{repo}/commits/*/check-runs)
            emit "{\"check_runs\":[{\"name\":\"build-and-test\",\"started_at\":\"2026-01-01T00:00:00Z\",\"conclusion\":\"$CI_CONCLUSION\"}]}" "$@" ;;
          repos/{owner}/{repo}/statuses/*)
            st=""; args=("$@")
            for ((i = 0; i < ${#args[@]}; i++)); do
              [ "${args[$i]}" = "-f" ] && [[ "${args[$((i+1))]}" == state=* ]] && st="${args[$((i+1))]#state=}"
            done
            emit "{\"context\":\"agent-verification\",\"state\":\"$st\"}" "$@" ;;
          repos/{owner}/{repo}/pulls/*) : ;;
          *) echo "mock gh: unhandled api path: $path" >&2; exit 1 ;;
        esac ;;
      *) echo "mock gh: unhandled args: $*" >&2; exit 1 ;;
    esac ;;
esac
MOCKGH
chmod +x "$MOCKBIN/gh"
export PATH="$MOCKBIN:$PATH"

run_verify() { "$SCRIPT" "$@"; }

# --- case 1: all gates pass -> aggregate success, canonical shape ---------- #
json="$(MOCK_CI_CONCLUSION=success run_verify 999 --gate scope-warden=pass --json)"
printf '%s\n' "$json" | python3 -m json.tool >/dev/null \
  && ok "case1: --json is valid JSON" || fail "case1: --json is valid JSON"

read -r schema pr issue head route agg gate_ci gate_sw <<< "$(printf '%s' "$json" | python3 -c '
import json, sys
ev = json.load(sys.stdin)
print(ev["schemaVersion"], ev["pr"], ev["issue"], ev["headSha"], ev["route"],
      ev["aggregate"], ev["gates"]["ci"], ev["gates"]["scope-warden"])
')"
assert_eq "case1: schemaVersion" "1" "$schema"
assert_eq "case1: pr number"     "999" "$pr"
assert_eq "case1: linked issue"  "136" "$issue"
assert_eq "case1: aggregate"     "success" "$agg"
assert_eq "case1: ci gate"       "pass" "$gate_ci"
assert_eq "case1: scope-warden gate" "pass" "$gate_sw"

keys="$(printf '%s' "$json" | python3 -c 'import json,sys; print(sorted(json.load(sys.stdin).keys()))')"
assert_eq "case1: exact canonical key set" \
  "['aggregate', 'gates', 'headSha', 'issue', 'pr', 'requiredGates', 'route', 'schemaVersion']" \
  "$keys"

rc=0; MOCK_CI_CONCLUSION=success run_verify 999 --gate scope-warden=pass >/dev/null || rc=$?
assert_eq "case1: exit 0 on success" "0" "$rc"

# --- case 2: a required gate never supplied -> pending, never success ------ #
json2="$(MOCK_CI_CONCLUSION=success run_verify 999 --json || true)"
agg2="$(printf '%s' "$json2" | python3 -c 'import json,sys; print(json.load(sys.stdin)["aggregate"])')"
assert_eq "case2: missing gate evidence never yields success" "pending" "$agg2"

rc2=0; MOCK_CI_CONCLUSION=success run_verify 999 >/dev/null || rc2=$?
assert_eq "case2: exit non-zero when pending" "1" "$rc2"

# --- case 3: ci fails on GitHub -> aggregate failure, ci fabricated never --- #
json3="$(MOCK_CI_CONCLUSION=failure run_verify 999 --gate scope-warden=pass --json || true)"
agg3="$(printf '%s' "$json3" | python3 -c 'import json,sys; print(json.load(sys.stdin)["aggregate"])')"
gate_ci3="$(printf '%s' "$json3" | python3 -c 'import json,sys; print(json.load(sys.stdin)["gates"]["ci"])')"
assert_eq "case3: real ci failure -> gates.ci = fail" "fail" "$gate_ci3"
assert_eq "case3: real ci failure -> aggregate failure" "failure" "$agg3"

# --- case 4: --json-out writes the same object to a file ------------------- #
OUT="$WORKDIR/evidence.json"
MOCK_CI_CONCLUSION=success run_verify 999 --gate scope-warden=pass --json-out "$OUT" >/dev/null
[ -f "$OUT" ] && ok "case4: --json-out wrote a file" || fail "case4: --json-out wrote a file"
python3 -m json.tool "$OUT" >/dev/null && ok "case4: --json-out file is valid JSON" \
  || fail "case4: --json-out file is valid JSON"

# --- case 5: --evidence PR-body block renders from the SAME object -------- #
evidence_out="$(MOCK_CI_CONCLUSION=success run_verify 999 --gate scope-warden=pass --evidence)"
assert_contains "case5: evidence block has managed start marker" \
  "$evidence_out" "<!-- agent-verification:start -->"
assert_contains "case5: evidence block has managed end marker" \
  "$evidence_out" "<!-- agent-verification:end -->"
assert_contains "case5: evidence block includes the head SHA" \
  "$evidence_out" "${MOCK_HEAD_SHA:-deadbeef1234567890deadbeef1234567890dead}"
assert_contains "case5: evidence block states the same aggregate as --json" \
  "$evidence_out" '`success` for'
assert_contains "case5: evidence block reports the same per-gate results" \
  "$evidence_out" '| `scope-warden` | pass |'

# --- case 6: --json mode leaves stdout as pure JSON, even with --evidence -- #
combined="$(MOCK_CI_CONCLUSION=success run_verify 999 --gate scope-warden=pass --json --evidence)"
printf '%s\n' "$combined" | python3 -m json.tool >/dev/null \
  && ok "case6: --json + --evidence still leaves stdout pure JSON" \
  || fail "case6: --json + --evidence still leaves stdout pure JSON"

# --- case 7: an unrequired --gate is rejected (unchanged prior behaviour) -- #
rc7=0; run_verify 999 --gate bogus-gate=pass >/dev/null 2>&1 || rc7=$?
assert_eq "case7: --gate for a non-required gate is rejected" "2" "$rc7"

# --- case 8: off-PR-head route is NOT path-only degraded (#137) ------------ #
# The local checkout here is never at MOCK_HEAD_SHA, so agent-verify.sh always
# takes the "not on PR head" branch. Feed it a numeric-threshold change to a
# rules file through the gh pr diff mock and confirm content escalation still
# fires — i.e. the required gate set is `formulas`'s (codex-conformance
# included), never degraded to the path-only `rules` set.
NUMERIC_DIFF='diff --git a/src/Brp.Rules/Combat/RangeBands.cs b/src/Brp.Rules/Combat/RangeBands.cs
index 1111111..2222222 100644
--- a/src/Brp.Rules/Combat/RangeBands.cs
+++ b/src/Brp.Rules/Combat/RangeBands.cs
@@ -1,3 +1,3 @@
 switch (r) {
-    Range.Short => 15,
+    Range.Short => 20,
 }'
json8="$(MOCK_PR_DIFF="$NUMERIC_DIFF" run_verify 999 \
  --gate scope-warden=pass --gate rules-conformance=pass --gate codex-conformance=pass --json)"
route8="$(printf '%s' "$json8" | python3 -c 'import json,sys; print(json.load(sys.stdin)["route"])')"
assert_eq "case8: off-head numeric change still content-escalates to formulas" "formulas" "$route8"
required8="$(printf '%s' "$json8" | python3 -c 'import json,sys; print(sorted(json.load(sys.stdin)["requiredGates"]))')"
assert_eq "case8: formulas required-gate set (not the path-only rules set)" \
  "['ci', 'codex-conformance', 'rules-conformance', 'scope-warden']" "$required8"

# Same patch, classified directly by route.sh (the same authority), must agree.
DIFFFILE="$WORKDIR/numeric.diff"
printf '%s\n' "$NUMERIC_DIFF" > "$DIFFFILE"
route8_direct="$("$ROOT/tools/route.sh" --json --diff-file "$DIFFFILE" | python3 -c 'import json,sys; print(json.load(sys.stdin)["route"])')"
assert_eq "case8: agrees with tools/route.sh --diff-file on the identical patch" "$route8_direct" "$route8"

echo
if [ "$FAILURES" -eq 0 ]; then
  echo "test_agent_verify.sh: all checks passed"
  exit 0
else
  echo "test_agent_verify.sh: $FAILURES check(s) failed"
  exit 1
fi
