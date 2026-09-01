#!/usr/bin/env bash
# tests/tooling/test_dispatch_agent.sh — fixture tests for tools/dispatch-agent.sh,
# the worktree-isolation rail (Issue #203, burn-in F12).
#
# Every case runs inside a THROWAWAY git repo in a tempdir, so the test never
# touches the real repository's worktrees or branches. `origin/main` is not
# available there, so BASE_REF is pointed at a local commit-ish; the tool's
# `git fetch` is best-effort and simply no-ops. `gh` is stubbed on PATH to
# exercise the title→slug branch-naming path deterministically.
#
# Run directly:
#   tests/tooling/test_dispatch_agent.sh
#
# Exit: 0 if every case passes, 1 on the first failure.
set -uo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
SCRIPT="$ROOT/tools/dispatch-agent.sh"

FAILURES=0
ok()   { printf 'ok   - %s\n' "$1"; }
fail() { printf 'FAIL - %s\n' "$1"; FAILURES=$((FAILURES + 1)); }

assert_eq() {
  local desc="$1" expected="$2" actual="$3"
  if [ "$expected" = "$actual" ]; then ok "$desc"; else
    fail "$desc (expected [$expected], got [$actual])"
  fi
}

WORKDIR="$(mktemp -d)"
BINDIR="$WORKDIR/bin"
mkdir -p "$BINDIR"
trap 'rm -rf "$WORKDIR"' EXIT

# gh stub: answers `gh issue view N --json title --jq .title` with a fixed title
# so the slug path is deterministic. GH_TITLE controls the answer; empty/unset
# makes the stub print nothing (exercises the no-title fallback).
cat > "$BINDIR/gh" <<'STUB'
#!/usr/bin/env bash
if [ "${1:-}" = "issue" ] && [ "${2:-}" = "view" ]; then
  printf '%s' "${GH_TITLE:-}"
  exit 0
fi
exit 0
STUB
chmod +x "$BINDIR/gh"

# --- a fresh throwaway repo per invocation ---------------------------------- #
new_repo() {
  local d; d="$(mktemp -d "$WORKDIR/repo.XXXXXX")"
  git -C "$d" init -q -b main
  git -C "$d" config user.email t@t; git -C "$d" config user.name t
  echo seed > "$d/seed.txt"
  git -C "$d" add -A; git -C "$d" commit -qm seed
  echo "$d"
}

# Run dispatch-agent from within repo $1 with our gh stub on PATH and a local BASE_REF.
run() {
  local repo="$1"; shift
  ( cd "$repo" && PATH="$BINDIR:$PATH" BASE_REF="main" "$SCRIPT" "$@" )
}

# === 1. --assert-isolated: primary fails, linked passes ===================== #
repo="$(new_repo)"
run "$repo" --assert-isolated >/dev/null 2>&1
assert_eq "--assert-isolated exits non-zero in the primary worktree" "1" "$?"

# create a worktree, then assert-isolated inside it
out="$(run "$repo" 7 2>/dev/null)"
wt="$(printf '%s\n' "$out" | sed -n 's/^path=//p')"
[ -n "$wt" ] && [ -d "$wt" ] && ok "create: worktree directory exists ($wt)" \
  || fail "create: worktree directory missing (out=$out)"
( cd "$wt" && PATH="$BINDIR:$PATH" "$SCRIPT" --assert-isolated >/dev/null 2>&1 )
assert_eq "--assert-isolated exits zero inside a linked worktree" "0" "$?"

# === 2. branch naming: no title -> issue-N ================================== #
assert_eq "no-title branch name is issue-7" "issue-7" \
  "$(printf '%s\n' "$out" | sed -n 's/^branch=//p')"
assert_eq "worktree path is <root>/.worktrees/issue-7" "$repo/.worktrees/issue-7" "$wt"

# === 3. branch naming: title -> issue-N-slug ================================ #
repo2="$(new_repo)"
out2="$(cd "$repo2" && PATH="$BINDIR:$PATH" GH_TITLE="Fix the Overpass Case!" BASE_REF=main "$SCRIPT" 42 2>/dev/null)"
assert_eq "titled branch name is slugified" "issue-42-fix-the-overpass-case" \
  "$(printf '%s\n' "$out2" | sed -n 's/^branch=//p')"

# === 4. --print-path has no side effects ==================================== #
repo3="$(new_repo)"
pp="$(run "$repo3" --print-path 5)"
assert_eq "--print-path prints the path" "$repo3/.worktrees/issue-5" "$pp"
[ ! -d "$repo3/.worktrees/issue-5" ] && ok "--print-path created nothing" \
  || fail "--print-path created a worktree (should not)"

# === 5. reuse: second create for the same issue reuses the worktree ========= #
run "$repo3" 5 >/dev/null 2>&1
out5="$(run "$repo3" 5 2>/dev/null)"
assert_eq "reuse: prints the same path" "$repo3/.worktrees/issue-5" \
  "$(printf '%s\n' "$out5" | sed -n 's/^path=//p')"

# === 6. cleanup: clean worktree removed; missing is a no-op ================= #
run "$repo3" --cleanup 5 >/dev/null 2>&1
assert_eq "cleanup of a clean worktree succeeds" "0" "$?"
[ ! -d "$repo3/.worktrees/issue-5" ] && ok "cleanup removed the directory" \
  || fail "cleanup left the directory behind"
run "$repo3" --cleanup 5 >/dev/null 2>&1
assert_eq "cleanup of a missing worktree is a no-op (exit 0)" "0" "$?"

# === 7. cleanup refuses a DIRTY worktree (never --force) ==================== #
repo4="$(new_repo)"
out4="$(run "$repo4" 9 2>/dev/null)"
wt4="$(printf '%s\n' "$out4" | sed -n 's/^path=//p')"
echo "uncommitted" > "$wt4/dirty.txt"
run "$repo4" --cleanup 9 >/dev/null 2>&1
assert_eq "cleanup refuses a dirty worktree (exit 2)" "2" "$?"
[ -d "$wt4" ] && ok "dirty worktree preserved" || fail "dirty worktree was removed"

# === 8. bad input ============================================================ #
run "$repo4" abc >/dev/null 2>&1
assert_eq "non-numeric issue rejected" "2" "$?"

echo
if [ "$FAILURES" -eq 0 ]; then echo "all dispatch-agent tests passed"; exit 0
else echo "$FAILURES dispatch-agent test(s) failed"; exit 1; fi
