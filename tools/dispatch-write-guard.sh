#!/usr/bin/env bash
# tools/dispatch-write-guard.sh — a PreToolUse "command" hook that makes
# implementer worktree isolation a RAIL rather than a first-action instruction
# (Issue #213, burn-in F12). It mirrors tools/reviewer-bash-guard.sh: registered
# in each implementer role's frontmatter (`.claude/agents/*.md`) matched on the
# Write/Edit tools, so it runs ONLY while that subagent executes.
#
# Contract (same as reviewer-bash-guard.sh):
#   read the PreToolUse JSON payload on stdin, inspect .tool_input.file_path, and
#     exit 0  -> approve
#     exit 2  -> block; stderr is fed back to the agent as the reason
#   never exit non-{0,2}; a crash must not silently allow.
#
# Policy: BLOCK a Write/Edit whose TARGET resides in the PRIMARY checkout, so an
# agent that skipped its `dispatch-agent.sh --assert-isolated` first action still
# cannot strand the primary tree. Approve otherwise — a linked worktree, or a
# target outside any git repo (e.g. /tmp scratch). Classification is delegated to
# `dispatch-agent.sh --assert-isolated PATH` so there is one isolation detector.
set -uo pipefail

HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

payload="$(cat 2>/dev/null || true)"

# The file the tool intends to write/modify (.tool_input.file_path). Best-effort:
# if we cannot parse it, fall back to the current directory.
target="$(printf '%s' "$payload" | python3 -c '
import json, sys
try:
    d = json.load(sys.stdin)
    print((d.get("tool_input") or {}).get("file_path") or "")
except Exception:
    print("")
' 2>/dev/null || true)"

dir="."
if [ -n "$target" ]; then
  d="$(dirname "$target")"
  [ -d "$d" ] && dir="$d"
fi

# Delegate to the one isolation detector. Exit 1 means "PATH is in the primary
# checkout" — the only case we block. Exit 0 (linked/isolated) or any other
# result (target not in a repo, detector error) is allowed: block only when we
# can positively prove the target is the primary tree.
rc=0
"$HERE/dispatch-agent.sh" --assert-isolated "$dir" >/dev/null 2>&1 || rc=$?
if [ "$rc" = 1 ]; then
  {
    echo "dispatch-write-guard: BLOCKED — this Write/Edit targets the PRIMARY checkout."
    echo "Implementer agents must run in a dedicated worktree (burn-in F12): a write here"
    echo "would strand the primary tree on a feature branch. Stop and return a dispatch"
    echo "error. The orchestrator creates your workspace with 'tools/dispatch-agent.sh"
    echo "<issue#>' and dispatches you there. (Backstop for the --assert-isolated first step.)"
  } >&2
  exit 2
fi
exit 0
