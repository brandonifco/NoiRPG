#!/usr/bin/env bash
# tools/ready-issues.sh — the scheduler query: which open issues are actually
# DISPATCHABLE for autonomous implementation (the "ready leaves").
#
#   tools/ready-issues.sh           classify every open issue, with the reason
#   tools/ready-issues.sh --ready   print only the dispatchable issue numbers
#
# Two independent concepts, deliberately kept apart:
#
#   * mechanically unblocked — none of the issue's native `blocked_by`
#     dependencies are still open. This is a GitHub-graph fact.
#   * approved for autonomous work — the issue carries the `ready` label and is
#     not held by a human gate (`blocked` / `needs-design`) and is not an epic
#     (an umbrella that is never itself implementable).
#
# An issue is DISPATCHABLE only when BOTH hold. The old query consulted only
# `blocked_by` and so reported epics, human-gated design issues, and even a
# `blocked`-labelled issue as ready — an autonomous dispatcher would then pick up
# an epic or a design issue first. See issue #124.
set -euo pipefail
cd "$(dirname "${BASH_SOURCE[0]}")/.."

# A human gate: an issue carrying any of these labels is NOT for autonomous work,
# even if its label:ready and its blockers are closed. `epic` is handled
# separately (it also fails the label:ready requirement in practice, but we never
# want an umbrella dispatched even if someone mislabels it `ready`).
HUMAN_GATE_LABELS=(blocked needs-design)

ready_only=0
[ "${1:-}" = "--ready" ] && ready_only=1

# One list call carries number, title, and labels — we only hit the per-issue
# dependencies API for candidates that already pass the label filter.
issues_json="$(gh issue list --state open --limit 200 \
  --json number,title,labels --jq 'sort_by(.number)')"

# Does this issue's label set contain $1 ?
has_label() { printf '%s' "$2" | tr ',' '\n' | grep -qx "$1"; }

open_blockers_of() {
  gh api "repos/{owner}/{repo}/issues/$1/dependencies/blocked_by" \
    --jq '[.[] | select(.state=="open") | .number] | join(" ")' 2>/dev/null || true
}

# Walk issues via a stable, newline-delimited projection (no subshell-per-issue).
while IFS=$'\t' read -r n title labels; do
  [ -z "$n" ] && continue

  # --- label gate: cheap, deterministic, no API call ---------------------- #
  reason=""
  is_epic=0
  # Epic by label, or by the "Epic:" title convention as a fallback.
  if has_label epic "$labels" || [[ "$title" =~ ^Epic: ]]; then
    is_epic=1
    reason="epic (umbrella, not implementable)"
  fi
  if [ -z "$reason" ]; then
    for g in "${HUMAN_GATE_LABELS[@]}"; do
      if has_label "$g" "$labels"; then reason="human-gated (label:$g)"; break; fi
    done
  fi
  if [ -z "$reason" ] && ! has_label ready "$labels"; then
    reason="not approved (no label:ready)"
  fi

  # --- dependency gate: only for label-clean candidates ------------------- #
  open_blockers=""
  if [ -z "$reason" ]; then
    open_blockers="$(open_blockers_of "$n")"
    [ -n "$open_blockers" ] && reason="blocked_by: $open_blockers"
  fi

  if [ -z "$reason" ]; then
    if [ "$ready_only" = 1 ]; then
      echo "$n"
    else
      printf 'READY      #%-4s %s\n' "$n" "$title"
    fi
  elif [ "$ready_only" = 0 ]; then
    printf 'not-ready  #%-4s %-40s %s\n' "$n" "$reason" "$title"
  fi
done < <(printf '%s\n' "$issues_json" \
  | jq -r '.[] | [(.number|tostring), .title, ([.labels[].name]|join(","))] | @tsv')
