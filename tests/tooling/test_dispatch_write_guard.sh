#!/usr/bin/env bash
# tests/tooling/test_dispatch_write_guard.sh — fixture tests for the PreToolUse
# Write/Edit isolation hook (Issue #213, burn-in F12).
#
# The guard reads a PreToolUse payload on stdin and BLOCKS (exit 2) a Write/Edit
# whose target is in the PRIMARY checkout, approving (exit 0) a linked worktree or
# a target outside any repo. All cases run in a throwaway repo + linked worktree,
# so the real repository is never touched.
#
# Run directly:
#   tests/tooling/test_dispatch_write_guard.sh
# Exit: 0 if every case passes, 1 on the first failure.
set -uo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
GUARD="$ROOT/tools/dispatch-write-guard.sh"

FAILURES=0
ok()   { printf 'ok   - %s\n' "$1"; }
fail() { printf 'FAIL - %s\n' "$1"; FAILURES=$((FAILURES + 1)); }
assert_eq() { local d="$1" e="$2" a="$3"; [ "$e" = "$a" ] && ok "$d" || fail "$d (expected [$e], got [$a])"; }

WORKDIR="$(mktemp -d)"
trap 'rm -rf "$WORKDIR"' EXIT

# throwaway repo (primary) + a linked worktree
REPO="$WORKDIR/repo"
git init -q -b main "$REPO"
git -C "$REPO" config user.email t@t; git -C "$REPO" config user.name t
echo seed > "$REPO/seed.txt"; git -C "$REPO" add -A; git -C "$REPO" commit -qm seed
WT="$WORKDIR/wt"
git -C "$REPO" worktree add -q -b feature "$WT" HEAD

# run the guard: $1 = cwd, $2 = payload on stdin ; echoes the exit code
guard_rc() {
  local cwd="$1" payload="$2" rc=0
  ( cd "$cwd" && printf '%s' "$payload" | bash "$GUARD" ) >/dev/null 2>&1 || rc=$?
  echo "$rc"
}
payload_for() { printf '{"tool_input":{"file_path":"%s"}}' "$1"; }

# === 1. target in the PRIMARY checkout -> BLOCK (exit 2) ==================== #
assert_eq "case1: write into primary tree is blocked" \
  "2" "$(guard_rc "$REPO" "$(payload_for "$REPO/newfile.cs")")"

# === 2. target in a LINKED worktree -> ALLOW (exit 0) ====================== #
assert_eq "case2: write into a linked worktree is allowed" \
  "0" "$(guard_rc "$REPO" "$(payload_for "$WT/newfile.cs")")"

# === 3. target outside any repo (e.g. /tmp scratch) -> ALLOW =============== #
assert_eq "case3: write outside any repo is allowed" \
  "0" "$(guard_rc "$REPO" "$(payload_for "$WORKDIR/scratch.txt")")"

# === 4. no file_path, cwd is the primary tree -> BLOCK ===================== #
assert_eq "case4: no target + cwd primary is blocked" \
  "2" "$(guard_rc "$REPO" '{}')"

# === 5. no file_path, cwd is a linked worktree -> ALLOW ==================== #
assert_eq "case5: no target + cwd worktree is allowed" \
  "0" "$(guard_rc "$WT" '{}')"

# === 6. malformed payload does not crash (fail-open to cwd classification) = #
assert_eq "case6: malformed payload, cwd worktree -> allow" \
  "0" "$(guard_rc "$WT" 'not json at all')"
assert_eq "case6: malformed payload, cwd primary -> block" \
  "2" "$(guard_rc "$REPO" 'not json at all')"

# === 7. the reason is fed back on block ==================================== #
err="$( ( cd "$REPO" && printf '%s' "$(payload_for "$REPO/x.cs")" | bash "$GUARD" ) 2>&1 1>/dev/null )"
case "$err" in *"PRIMARY checkout"*) ok "case7: block reason names the primary checkout" ;;
  *) fail "case7: block reason missing (got [$err])" ;; esac

git -C "$REPO" worktree remove --force "$WT" >/dev/null 2>&1 || true
echo
if [ "$FAILURES" -eq 0 ]; then echo "test_dispatch_write_guard.sh: all checks passed"; exit 0
else echo "test_dispatch_write_guard.sh: $FAILURES check(s) failed"; exit 1; fi
