#!/usr/bin/env bash
# tools/ready-issues.sh — the scheduler query: which open issues have no unresolved
# blockers (the "ready leaves"). This is the deterministic question the orchestrator
# asks to pick the next work, now that dependencies are a native GitHub graph rather
# than prose.
#
#   tools/ready-issues.sh           classify every open issue as READY or blocked
#   tools/ready-issues.sh --ready   print only the ready issue numbers (one per line)
#
# An issue is READY when none of its native `blocked_by` dependencies are still open.
set -euo pipefail
cd "$(dirname "${BASH_SOURCE[0]}")/.."

ready_only=0
[ "${1:-}" = "--ready" ] && ready_only=1

mapfile -t open < <(gh issue list --state open --limit 200 --json number --jq '.[].number' | sort -n)

for n in "${open[@]}"; do
  open_blockers="$(gh api "repos/{owner}/{repo}/issues/$n/dependencies/blocked_by" \
    --jq '[.[] | select(.state=="open") | .number] | join(" ")' 2>/dev/null || true)"
  if [ -z "$open_blockers" ]; then
    if [ "$ready_only" = 1 ]; then
      echo "$n"
    else
      title="$(gh issue view "$n" --json title --jq .title)"
      printf 'READY    #%-4s %s\n' "$n" "$title"
    fi
  elif [ "$ready_only" = 0 ]; then
    printf 'blocked  #%-4s waiting on: %s\n' "$n" "$open_blockers"
  fi
done
