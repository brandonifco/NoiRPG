#!/usr/bin/env bash
# tests/tooling/test_reviewer_bash_guard.sh — fixture tests for
# tools/reviewer_bash_guard.py / tools/reviewer-bash-guard.sh, the read-only
# Bash allowlist for the verification reviewer subagents (Issue #170,
# docs/decisions/0026-reviewer-mechanical-read-only.md).
#
# Drives the guard exactly as Claude Code would: a PreToolUse JSON payload on
# stdin, exit code as the verdict (0 = approve, 2 = deny).
#
# Run directly:
#   tests/tooling/test_reviewer_bash_guard.sh
#
# Exit: 0 if every case passes, 1 on the first failure.
set -uo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
GUARD="$ROOT/tools/reviewer-bash-guard.sh"

FAILURES=0
ok()   { printf 'ok   - %s\n' "$1"; }
fail() { printf 'FAIL - %s\n' "$1"; FAILURES=$((FAILURES + 1)); }

# run_guard COMMAND -> sets GUARD_EXIT to the guard's exit code for that
# Bash tool_input.command, run from ROOT (so relative paths in commands like
# "tools/reviewer-bash-guard.sh" itself resolve the way they do in a real
# session).
run_guard() {
  local cmd="$1"
  local payload
  payload="$(python3 -c 'import json,sys; print(json.dumps({"tool_name":"Bash","tool_input":{"command":sys.argv[1]}}))' "$cmd")"
  printf '%s' "$payload" | (cd "$ROOT" && "$GUARD") >/tmp/reviewer-bash-guard.out 2>/tmp/reviewer-bash-guard.err
  GUARD_EXIT=$?
}

assert_allowed() {
  local desc="$1" cmd="$2"
  run_guard "$cmd"
  if [ "$GUARD_EXIT" -eq 0 ]; then
    ok "$desc"
  else
    fail "$desc (expected allow/exit 0, got exit $GUARD_EXIT; stderr: $(cat /tmp/reviewer-bash-guard.err))"
  fi
}

assert_denied() {
  local desc="$1" cmd="$2"
  run_guard "$cmd"
  if [ "$GUARD_EXIT" -eq 2 ]; then
    ok "$desc"
  else
    fail "$desc (expected deny/exit 2, got exit $GUARD_EXIT)"
  fi
}

# --- allowed: the read-only commands a reviewer actually needs -------------- #
assert_allowed "git show of a committed revision"        "git show HEAD:AGENTS.md"
assert_allowed "git diff between two revisions"            "git diff HEAD~1 HEAD"
assert_allowed "git log"                                    "git log -3"
assert_allowed "git rev-parse"                              "git rev-parse HEAD"
assert_allowed "grep within a file"                         "grep -n Bash AGENTS.md"
assert_allowed "ripgrep within a file"                       "rg Bash AGENTS.md"
assert_allowed "pdftotext to stdout for a page range"        "pdftotext -f 1 -l 1 BasicRoleplaying-ORC-Content-Document.pdf -"
assert_allowed "sed display-only (no -i)"                    "sed -n '1,5p' AGENTS.md"
assert_allowed "dotnet build (no write outside obj/bin)"     "dotnet build"
assert_allowed "dotnet test"                                 "dotnet test"
assert_allowed "ls"                                          "ls tools"
assert_allowed "find without an executing/mutating flag"      "find tools -name '*.py'"

# --- denied: mutation ---------------------------------------------------------- #
assert_denied "rm is not on the allowlist at all"              "rm -rf /tmp/whatever"
assert_denied "git commit mutates"                              "git commit -am oops"
assert_denied "git push mutates the remote"                     "git push origin main"
assert_denied "git checkout mutates the working tree"            "git checkout ."
assert_denied "git add stages a mutation"                        "git add -A"
assert_denied "sed -i mutates a file"                            "sed -i 's/a/b/' AGENTS.md"
assert_denied "pdftotext writing to a file, not stdout"           "pdftotext BasicRoleplaying-ORC-Content-Document.pdf out.txt"
assert_denied "find -delete mutates"                              "find . -name '*.tmp' -delete"
assert_denied "dotnet nuget push is network egress"                "dotnet nuget push pkg.nupkg"
assert_denied "curl is not on the allowlist (network egress)"      "curl https://example.invalid"
assert_denied "pip install is a package install"                    "pip install requests"

# --- denied: the named evasion surface --------------------------------------- #
assert_denied "chained commands via ;"                           "git show HEAD:AGENTS.md; rm -rf /"
assert_denied "chained commands via &&"                           "git status && rm -rf /"
assert_denied "piping to disguise a second command"                 "git show HEAD:AGENTS.md | sh"
assert_denied "command substitution \$(...)"                        "git show \$(echo HEAD):AGENTS.md"
assert_denied "backtick command substitution"                        'git show `echo HEAD`:AGENTS.md'
assert_denied "output redirection writes a file without a shell op" "cat AGENTS.md > /tmp/exfil.txt"
assert_denied "process substitution"                                 "diff <(git show HEAD:AGENTS.md) AGENTS.md"
assert_denied "backgrounding with &"                                  "git status &"
assert_denied "bash -c wraps an arbitrary command"                     "bash -c 'rm -rf /'"
assert_denied "xargs turns a read into an executor"                     "find . -name '*.cs' | xargs rm"
assert_denied "leading env-var assignment"                               "GIT_PAGER=x git show HEAD"
assert_denied "git -c reconfigures on the fly"                            "git -c core.pager=x show HEAD"
assert_denied "git --output writes a file with no shell redirection"       "git log --output=x.txt"
assert_denied "an unbalanced-quote command is denied, not best-effort parsed" "git show 'unterminated"

echo
if [ "$FAILURES" -eq 0 ]; then
  echo "test_reviewer_bash_guard.sh: all checks passed"
  exit 0
else
  echo "test_reviewer_bash_guard.sh: $FAILURES check(s) failed"
  exit 1
fi
