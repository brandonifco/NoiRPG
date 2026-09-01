#!/usr/bin/env bash
# tests/tooling/test_agent_brief.sh — fixture tests for tools/agent-brief.py's
# packet-first dispatch contract (Issue #139).
#
# Proves: a TASK packet and a REVIEW packet are each generated in one command;
# every packet ends with a reproducible packet-sha256 (no timestamp or other
# nondeterministic value hashed in — two runs against identical state produce
# the identical hash); the packet contains its required sections; and the
# packet's predicted route agrees with tools/route.sh, the one route authority.
#
# No network `gh` access is assumed (CI has none) — `gh` is stubbed with a
# fixture script, same pattern as tests/tooling/test_agent_verify.sh. `git`
# operations run against this real checkout's own history, so no mocking of
# git is needed.
#
# Run directly:
#   tests/tooling/test_agent_brief.sh
#
# Exit: 0 if every case passes, 1 on the first failure.
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
SCRIPT="$ROOT/tools/agent-brief.py"

WORKDIR="$(mktemp -d)"
trap 'git -C "$ROOT" worktree remove --force "$WORKDIR/wt" >/dev/null 2>&1 || true; rm -rf "$WORKDIR"' EXIT

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
# Answers exactly the gh subcommands agent-brief.py's task packet issues:
# `issue view <n> --json title,body,labels,number,url` for the issue itself,
# `issue view <n> --json state,title` for a referenced dependency, and
# `issue view <n> --json labels --jq ...` (issued by tools/route.sh --issue).
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

case "$1 $2" in
  "issue view")
    num="$3"
    jsonf="$(find_flag_value --json "$@" || true)"
    case "$jsonf" in
      title,body,labels,number,url)
        if [ "$num" = "5001" ]; then
          body_json="$(python3 -c '
import json
body = """## Outcome
Make the widget reproducible.

## Acceptance Criteria
- widget is deterministic

## Out of Scope
- do not touch the gizmo

## Dependencies
#5002

## Workspace
`tools/agent-brief.py` is the file to change.

## Known Dead Ends
- tried a global counter, rejected: not reproducible"""
print(json.dumps(body))
')"
          emit "{\"title\":\"Widget packet test\",\"body\":$body_json,\"labels\":[],\"number\":5001,\"url\":\"https://example.invalid/issues/5001\"}" "$@"
        elif [ "$num" = "5003" ]; then
          # No "Likely files" / "Workspace" section at all (mirrors #112): the
          # task packet's route prediction must still honor the issue's
          # `route:formulas` floor via `--issue`, not fall back to "?" and not
          # fall back to the local checkout's dirty-worktree diff.
          body_json="$(python3 -c '
import json
body = """## Outcome
Add a numeric table with no named files."""
print(json.dumps(body))
')"
          emit "{\"title\":\"Floor packet test\",\"body\":$body_json,\"labels\":[{\"name\":\"route:formulas\"}],\"number\":5003,\"url\":\"https://example.invalid/issues/5003\"}" "$@"
        else
          echo "mock gh: unhandled issue view $num" >&2; exit 1
        fi ;;
      state,title)
        emit "{\"state\":\"OPEN\",\"title\":\"Dependency fixture issue\"}" "$@" ;;
      labels)
        if [ "$num" = "5003" ]; then
          emit "{\"labels\":[{\"name\":\"route:formulas\"}]}" "$@"
        elif [ "$num" = "6001" ]; then
          # Issue #186 fixture: an issue-level `route:architecture` intent
          # floor, carried by a docs-only PR with no project/boundary change.
          emit "{\"labels\":[{\"name\":\"route:architecture\"}]}" "$@"
        else
          emit "{\"labels\":[]}" "$@"
        fi ;;
      *) echo "mock gh: unhandled issue view --json $jsonf" >&2; exit 1 ;;
    esac ;;
  *) echo "mock gh: unhandled args: $*" >&2; exit 1 ;;
esac
MOCKGH
chmod +x "$MOCKBIN/gh"
export PATH="$MOCKBIN:$PATH"

# === TASK PACKET ============================================================ #

task1="$WORKDIR/task1.md"
task2="$WORKDIR/task2.md"
python3 "$SCRIPT" task 5001 > "$task1"
python3 "$SCRIPT" task 5001 > "$task2"

ok "task packet: generated in one command"

diff -q "$task1" "$task2" >/dev/null \
  && ok "task packet: byte-identical across two runs" \
  || fail "task packet: byte-identical across two runs"

hash1="$(grep '^packet-sha256:' "$task1" | awk '{print $2}')"
hash2="$(grep '^packet-sha256:' "$task2" | awk '{print $2}')"
[ -n "$hash1" ] && ok "task packet: packet-sha256 present" || fail "task packet: packet-sha256 present"
assert_eq "task packet: packet-sha256 reproducible across runs" "$hash1" "$hash2"
assert_eq "task packet: packet-sha256 is 64 hex chars" "64" "${#hash1}"

grep -q '^packet-version: 1$' "$task1" \
  && ok "task packet: packet-version footer present" \
  || fail "task packet: packet-version footer present"
grep -q '^packet-schema: task-packet/1$' "$task1" \
  && ok "task packet: packet-schema marker present" \
  || fail "task packet: packet-schema marker present"

for section in \
  "# TASK BRIEF" \
  "<https://example.invalid/issues/5001>" \
  "**Outcome.**" \
  "**Acceptance criteria.**" \
  "**Explicitly out of scope.**" \
  "## AUTHORITY" \
  "## DEPENDENCIES" \
  "## WORKSPACE" \
  "## REQUIRED GATES" \
  "## DO NOT REVISIT" \
  "## KNOWN DEAD ENDS" \
  ; do
  content="$(cat "$task1")"
  assert_contains "task packet: contains section [$section]" "$content" "$section"
done

content="$(cat "$task1")"
assert_contains "task packet: dependency state resolved" "$content" "Dependency fixture issue"
assert_contains "task packet: known dead end carried through" "$content" "global counter"
assert_contains "task packet: explicit exclusion carried through" "$content" "gizmo"

# --- task packet route matches tools/route.sh directly ---------------------- #
route_line="$(grep '^- route:' "$task1")"
direct_route="$(bash "$ROOT/tools/route.sh" --json tools/agent-brief.py | python3 -c 'import json,sys; print(json.load(sys.stdin)["route"])')"
assert_contains "task packet: predicted route matches tools/route.sh" "$route_line" "**$direct_route**"

# --- task packet honors the Issue's route:* floor (Issue #169) -------------- #
# #5003 carries `route:formulas` and names no likely files at all — the
# predicted route must still be raised to (at least) "formulas" by the
# issue-intent floor, matching `tools/route.sh --issue 5003` directly, not
# left at "?" and not derived from this checkout's local working-tree diff.
task3="$WORKDIR/task3.md"
python3 "$SCRIPT" task 5003 > "$task3"
floor_route_line="$(grep '^- route:' "$task3")"
direct_floor_route="$(bash "$ROOT/tools/route.sh" --json --issue 5003 | python3 -c 'import json,sys; print(json.load(sys.stdin)["route"])')"
assert_eq "task packet: issue route:* floor resolves to formulas" "formulas" "$direct_floor_route"
assert_contains "task packet: predicted route honors issue route:* floor" "$floor_route_line" "**$direct_floor_route**"
assert_contains "task packet: predicted gates honor issue route:* floor" "$floor_route_line" "codex-conformance"

# === REVIEW PACKET =========================================================== #
# Use two real commits from this checkout's own history so no `gh pr view` is
# needed (an explicit --base/--head range skips it entirely).
BASE_SHA="$(git -C "$ROOT" rev-parse HEAD~2)"
HEAD_SHA="$(git -C "$ROOT" rev-parse HEAD)"

review1="$WORKDIR/review1.md"
review2="$WORKDIR/review2.md"
python3 "$SCRIPT" review --base "$BASE_SHA" --head "$HEAD_SHA" --issue 5001 > "$review1"
python3 "$SCRIPT" review --base "$BASE_SHA" --head "$HEAD_SHA" --issue 5001 > "$review2"

ok "review packet: generated in one command"

diff -q "$review1" "$review2" >/dev/null \
  && ok "review packet: byte-identical across two runs" \
  || fail "review packet: byte-identical across two runs"

rhash1="$(grep '^packet-sha256:' "$review1" | awk '{print $2}')"
rhash2="$(grep '^packet-sha256:' "$review2" | awk '{print $2}')"
assert_eq "review packet: packet-sha256 reproducible across runs" "$rhash1" "$rhash2"
assert_eq "review packet: packet-sha256 is 64 hex chars" "64" "${#rhash1}"

grep -q '^packet-schema: review-packet/1$' "$review1" \
  && ok "review packet: packet-schema marker present" \
  || fail "review packet: packet-schema marker present"

rcontent="$(cat "$review1")"
for section in \
  "# REVIEW BRIEF" \
  "## PR" \
  "## ISSUE" \
  "## RANGE" \
  "$BASE_SHA" \
  "$HEAD_SHA" \
  "## CHANGED FILES" \
  "## AUTHORITY" \
  "## IMPLEMENTER CLAIM" \
  "## REQUIRED REVIEW" \
  "## ESCALATION REASON" \
  "## DIFF" \
  ; do
  assert_contains "review packet: contains section [$section]" "$rcontent" "$section"
done
assert_contains "review packet: source-packet reference present" "$rcontent" "agent-brief.py task 5001"

review_route_line="$(grep '^## REQUIRED REVIEW' "$review1")"
direct_review_route="$(bash "$ROOT/tools/route.sh" --json --base "$BASE_SHA" --issue 5001 | python3 -c 'import json,sys; print(json.load(sys.stdin)["route"])' 2>/dev/null || true)"
if [ -n "$direct_review_route" ]; then
  assert_contains "review packet: predicted route matches tools/route.sh" "$review_route_line" "**$direct_review_route**"
else
  fail "review packet: predicted route matches tools/route.sh (could not compute direct route)"
fi

# === ARCHITECTURE-REVIEW CHECKLIST SCOPE (Issue #186 / burn-in F11) ======= #
# The architecture-review gate lands in the gate set for two structurally
# different reasons: a path-derived project/boundary change actually in the
# diff, or an issue-level `route:architecture` intent floor with no such
# change present. The review packet must name the right scope for each — see
# tools/agent-brief.py's architecture_review_reason_and_checklist().
#
# Real git commits are needed (route.sh classifies actual diffs), so this
# stands up a disposable secondary worktree, sharing this checkout's object
# store, and advances its own HEAD independently — the primary worktree
# under test is never touched.
WT="$WORKDIR/wt"
git -C "$ROOT" worktree add -q --detach "$WT" HEAD

base_common="$(git -C "$WT" rev-parse HEAD)"

# --- case (a): docs-only change + issue `route:architecture` floor -> the
# checklist must be decision-scoped, not the Brp.* layering boilerplate. ---
mkdir -p "$WT/docs/decisions"
cat > "$WT/docs/decisions/9999-fixture-test-186.md" <<'EOF'
# 9999. Fixture decision record for test_agent_brief.sh (Issue #186)

This file exists only to give the architecture-review checklist test a
docs-only diff to classify. It is committed to a disposable worktree and
never merged.
EOF
git -C "$WT" add docs/decisions/9999-fixture-test-186.md
git -C "$WT" -c user.email=test@example.invalid -c user.name=test commit -q -m "test fixture: docs-only change for #186"
head_docs="$(git -C "$WT" rev-parse HEAD)"

# route.sh reads issue labels via `gh`, which must resolve to the mock — run
# it from within $WT so `HEAD` resolves to this worktree's own HEAD (PATH,
# with the gh stub, is already exported above).
direct_docs="$(cd "$WT" && bash tools/route.sh --json --base "$base_common" --issue 6001 2>/dev/null || true)"
assert_contains "case (a): route.sh reports architecture true (issue floor only)" "$direct_docs" '"architecture":true'
assert_contains "case (a): route.sh reports issueRaised true off the architecture floor" "$direct_docs" '"issueRaised":true'
assert_contains "case (a): route.sh reports issueRoute architecture" "$direct_docs" '"issueRoute":"architecture"'

review_docs="$WORKDIR/review_docs.md"
python3 "$WT/tools/agent-brief.py" review --base "$base_common" --head "$head_docs" --issue 6001 > "$review_docs"
docs_content="$(cat "$review_docs")"
assert_contains "case (a): checklist is decision-scoped, not layering boilerplate" "$docs_content" \
  "no project/boundary change is actually in this diff"
assert_contains "case (a): checklist tells the reviewer to check decision/boundary soundness" "$docs_content" \
  "SOUNDNESS of the recorded decision"
if [[ "$docs_content" == *"architecture-review"*"Brp.Core/Brp.Rules take no game-engine dependency"* ]]; then
  fail "case (a): checklist must NOT use the Brp.* layering boilerplate"
else
  ok "case (a): checklist does not use the Brp.* layering boilerplate"
fi
assert_contains "case (a): escalation reason cites the issue-intent floor, not a diff-touched boundary" "$docs_content" \
  "Issue intent floor \`route:architecture\`"

# --- case (b): a real .csproj / project-reference change -> the checklist
# must still name the layering / no-game-engine-dependency checks. ---
git -C "$WT" checkout -q "$base_common"
printf '  <!-- test fixture #186: benign comment -->\n' >> "$WT/tools/Brp.Cli/Brp.Cli.csproj"
git -C "$WT" add tools/Brp.Cli/Brp.Cli.csproj
git -C "$WT" -c user.email=test@example.invalid -c user.name=test commit -q -m "test fixture: project-file change for #186"
head_csproj="$(git -C "$WT" rev-parse HEAD)"

direct_csproj="$(cd "$WT" && bash tools/route.sh --json --base "$base_common" 2>/dev/null || true)"
assert_contains "case (b): route.sh reports architecture true (path-derived)" "$direct_csproj" '"architecture":true'
assert_contains "case (b): route.sh reports issueRaised false (no issue floor involved)" "$direct_csproj" '"issueRaised":false'

review_csproj="$WORKDIR/review_csproj.md"
python3 "$WT/tools/agent-brief.py" review --base "$base_common" --head "$head_csproj" > "$review_csproj"
csproj_content="$(cat "$review_csproj")"
assert_contains "case (b): checklist keeps the Brp.* layering/no-game-engine-dependency check" "$csproj_content" \
  "Brp.Core/Brp.Rules take no game-engine dependency"
assert_contains "case (b): escalation reason cites the diff, not an issue floor" "$csproj_content" \
  "the diff touches project references/layering"
if [[ "$csproj_content" == *"SOUNDNESS of the recorded decision"* ]]; then
  fail "case (b): checklist must NOT be decision-scoped for a real project-file change"
else
  ok "case (b): checklist is not decision-scoped for a real project-file change"
fi

echo
if [ "$FAILURES" -eq 0 ]; then
  echo "test_agent_brief.sh: all checks passed"
  exit 0
else
  echo "test_agent_brief.sh: $FAILURES check(s) failed"
  exit 1
fi
