#!/usr/bin/env bash
# tools/route.sh — derive the verification route and required gates for a change.
#
# The single source of truth for "which reviewers does this change need" so the
# orchestrator (and #62's state machine) never has to remember it. Reads
# .github/route-map for the coarse path baseline, then applies a content
# escalation that promotes a `rules` change to `formulas` when the diff actually
# touches numeric tables / thresholds.
#
# Usage:
#   tools/route.sh [--base <ref> | --diff-file <path>] [--json] [--issue <n>] [file ...]
#
# File selection (first that applies):
#   explicit [file ...]   classify exactly those paths
#   --diff-file <path>    a unified diff (e.g. `gh pr diff` or `git diff` output,
#                          not necessarily generated in this checkout) — content
#                          escalation is read from the patch itself, so route
#                          derivation away from the diff's own branch is identical
#                          to running on it. This is the ONE route authority: other
#                          tools that need to classify a patch they didn't generate
#                          locally (agent-verify.sh, pr_policy.py, agent-brief.py)
#                          call this, they do not reimplement classification.
#   --base <ref>          git diff --name-only <ref>...HEAD
#   (neither)             git diff --name-only HEAD   (working tree + staged)
#
# Output (text): the route and the ordered required gate set.
# Output (--json): {"route","architecture","escalated","gates":[...],"files":[...]}
#
# See docs/orchestration/routing.md for the route -> gates table.
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
MAP="$ROOT/.github/route-map"
[ -f "$MAP" ] || { echo "route-map not found: $MAP" >&2; exit 2; }

JSON=0
BASE=""
DIFF_FILE=""       # classify a patch that isn't necessarily the local checkout's diff
ISSUE_ROUTE=""     # an explicit intent floor, e.g. --issue-route formulas
ISSUE_NUM=""       # or --issue N, from which we read the issue's route:* label
FILES=()
while [ $# -gt 0 ]; do
  case "$1" in
    --json)        JSON=1; shift ;;
    --base)        BASE="${2:-}"; shift 2 ;;
    --diff-file)   DIFF_FILE="${2:-}"; shift 2 ;;
    --issue-route) ISSUE_ROUTE="${2:-}"; shift 2 ;;
    --issue)       ISSUE_NUM="${2:-}"; shift 2 ;;
    --)     shift; while [ $# -gt 0 ]; do FILES+=("$1"); shift; done ;;
    -*)     echo "unknown option: $1" >&2; exit 2 ;;
    *)      FILES+=("$1"); shift ;;
  esac
done

# Issue-intent escalation is ASYMMETRIC: a change's declared intent may RAISE its
# route but never lower it. The diff sees only filenames; an issue's `route:*`
# label carries the author's knowledge that a change is riskier than it looks.
# `--issue N` reads that label; `--issue-route R` supplies it directly.
iprec() { case "$1" in docs) echo 1 ;; tooling) echo 2 ;; rules) echo 3 ;; formulas) echo 4 ;; architecture) echo 5 ;; *) echo 0 ;; esac; }
if [ -n "$ISSUE_NUM" ] && [ -z "$ISSUE_ROUTE" ]; then
  # Take the highest-precedence route:* label if the issue carries more than one.
  while IFS= read -r r; do
    [ -z "$r" ] && continue
    [ "$(iprec "$r")" -gt "$(iprec "${ISSUE_ROUTE:-}")" ] && ISSUE_ROUTE="$r"
  done < <(gh issue view "$ISSUE_NUM" --json labels \
             --jq '.labels[].name | select(startswith("route:")) | ltrimstr("route:")' \
             2>/dev/null || true)
fi
case "${ISSUE_ROUTE:-}" in
  ""|docs|tooling|rules|formulas|architecture) ;;
  *) echo "invalid --issue-route: $ISSUE_ROUTE (docs|tooling|rules|formulas|architecture)" >&2; exit 2 ;;
esac

[ -n "$DIFF_FILE" ] && [ -n "$BASE" ] && { echo "--diff-file and --base are mutually exclusive" >&2; exit 2; }
[ -n "$DIFF_FILE" ] && [ ! -f "$DIFF_FILE" ] && { echo "diff file not found: $DIFF_FILE" >&2; exit 2; }

# Parse a unified diff (git diff / gh pr diff format) for the paths it touches.
# Uses the `diff --git a/X b/Y` header, which names both sides regardless of
# add/delete/rename, so it does not depend on --- / +++ (which go /dev/null on
# add or delete). Reports the "b" (current/new) path, falling back to "a".
files_from_diff_file() {
  awk '
    /^diff --git a\// {
      line = $0
      sub(/^diff --git a\//, "", line)
      idx = index(line, " b/")
      if (idx > 0) {
        apath = substr(line, 1, idx - 1)
        bpath = substr(line, idx + 3)
        print (bpath != "" ? bpath : apath)
      }
    }
  ' "$1"
}

# Extract only the hunks for the given paths out of a unified diff file, so
# content escalation can be evaluated against an externally supplied patch the
# exact same way it is against a local `git diff`.
diff_hunks_for_files_in_file() {
  local diff_file="$1"; shift
  local wanted; wanted="$(printf '%s\n' "$@")"
  awk -v wanted="$wanted" '
    BEGIN {
      n = split(wanted, arr, "\n")
      for (i = 1; i <= n; i++) if (arr[i] != "") want[arr[i]] = 1
    }
    /^diff --git a\// {
      line = $0
      sub(/^diff --git a\//, "", line)
      idx = index(line, " b/")
      keep = 0
      if (idx > 0) {
        apath = substr(line, 1, idx - 1)
        bpath = substr(line, idx + 3)
        if ((apath in want) || (bpath in want)) keep = 1
      }
      next
    }
    { if (keep) print }
  ' "$diff_file"
}

if [ ${#FILES[@]} -eq 0 ]; then
  if [ -n "$DIFF_FILE" ]; then
    mapfile -t FILES < <(files_from_diff_file "$DIFF_FILE")
  elif [ -n "$BASE" ]; then
    mapfile -t FILES < <(git -C "$ROOT" diff --name-only "$BASE"...HEAD)
  else
    mapfile -t FILES < <(git -C "$ROOT" diff --name-only HEAD)
  fi
fi

# Convert a glob to an anchored ERE. '*' stays within a segment; '**' crosses
# segments; '**/' matches zero or more leading directories; other metacharacters
# are escaped literally.
glob_to_regex() {
  local g="$1"
  local i c out="" n
  n=${#g}
  if [ "$g" = "*" ]; then printf '^.*$'; return; fi
  for ((i = 0; i < n; i++)); do
    c=${g:i:1}
    if [ "$c" = "*" ]; then
      if [ "${g:i+1:1}" = "*" ]; then
        if [ "${g:i+2:1}" = "/" ]; then out+='(.*/)?'; i=$((i + 2));
        else out+='.*'; i=$((i + 1)); fi
      else
        out+='[^/]*'
      fi
    else
      case "$c" in
        [a-zA-Z0-9_/-]) out+="$c" ;;
        *) out+="\\$c" ;;
      esac
    fi
  done
  printf '^%s$' "$out"
}

# Last matching route-map rule wins for a file.
route_for_file() {
  local f="$1" raw pat rt re matched=""
  while IFS= read -r raw || [ -n "$raw" ]; do
    raw="${raw%%#*}"
    read -r pat rt <<< "$raw" || true
    [ -z "${pat:-}" ] && continue
    [ -z "${rt:-}" ] && continue
    re="$(glob_to_regex "$pat")"
    [[ "$f" =~ $re ]] && matched="$rt"
  done < "$MAP"
  printf '%s' "$matched"
}

prec() { case "$1" in docs) echo 1 ;; tooling) echo 2 ;; rules) echo 3 ;; formulas) echo 4 ;; *) echo 0 ;; esac; }

base="tooling"; basep=0; arch=0
for f in "${FILES[@]}"; do
  [ -z "$f" ] && continue
  r="$(route_for_file "$f")"; [ -z "$r" ] && r="tooling"
  if [ "$r" = "architecture" ]; then arch=1; continue; fi
  p="$(prec "$r")"
  if [ "$p" -gt "$basep" ]; then basep="$p"; base="$r"; fi
done

# Content escalation: a `rules` change that touches numeric tables / thresholds
# becomes `formulas`. A changed line counts if it adds/removes a value in a
# table/threshold shape — see docs/orchestration/routing.md for the exact set.
escalated=0
if [ "$base" = "rules" ]; then
  rf=()
  for f in "${FILES[@]}"; do
    [ -z "$f" ] && continue
    r="$(route_for_file "$f")"
    { [ "$r" = "rules" ] || [ "$r" = "formulas" ]; } && rf+=("$f")
  done
  if [ ${#rf[@]} -gt 0 ]; then
    if [ -n "$DIFF_FILE" ]; then
      d="$(diff_hunks_for_files_in_file "$DIFF_FILE" "${rf[@]}")"
    elif [ -n "$BASE" ]; then
      d="$(git -C "$ROOT" diff --unified=0 "$BASE"...HEAD -- "${rf[@]}" 2>/dev/null || true)"
    else
      d="$(git -C "$ROOT" diff --unified=0 HEAD -- "${rf[@]}" 2>/dev/null || true)"
    fi
    if printf '%s' "$d" | grep -Eq '^[+-][^+-].*(=>[[:space:]]*-?[0-9]|:[[:space:]]*-?[0-9]+|\[[[:space:]]*-?[0-9]|(>=|<=|>|<)[[:space:]]*-?[0-9]|,[[:space:]]*-?[0-9]+)'; then
      base="formulas"; escalated=1
    fi
  fi
fi

# Issue-intent floor (asymmetric): raise the route to the declared intent, never
# lower it. `architecture` intent adds the architecture review rather than
# replacing the base route (it composes, exactly like a changed .csproj would).
issue_raised=0
if [ -n "$ISSUE_ROUTE" ]; then
  if [ "$ISSUE_ROUTE" = "architecture" ]; then
    [ "$arch" = 0 ] && { arch=1; issue_raised=1; }
  elif [ "$(prec "$ISSUE_ROUTE")" -gt "$(prec "$base")" ]; then
    base="$ISSUE_ROUTE"; issue_raised=1
  fi
fi

case "$base" in
  docs | tooling) gates="ci" ;;
  rules)          gates="ci scope-warden rules-conformance" ;;
  formulas)       gates="ci scope-warden rules-conformance codex-conformance" ;;
esac
[ "$arch" = 1 ] && gates="$gates architecture-review"

if [ "$JSON" = 1 ]; then
  gj=""; for x in $gates; do gj+="\"$x\","; done; gj="${gj%,}"
  fj=""; for x in "${FILES[@]}"; do [ -z "$x" ] && continue; fj+="\"$x\","; done; fj="${fj%,}"
  printf '{"route":"%s","architecture":%s,"escalated":%s,"issueRoute":%s,"issueRaised":%s,"gates":[%s],"files":[%s]}\n' \
    "$base" "$([ "$arch" = 1 ] && echo true || echo false)" \
    "$([ "$escalated" = 1 ] && echo true || echo false)" \
    "$([ -n "$ISSUE_ROUTE" ] && echo "\"$ISSUE_ROUTE\"" || echo null)" \
    "$([ "$issue_raised" = 1 ] && echo true || echo false)" "$gj" "$fj"
else
  suffix=""; [ "$arch" = 1 ] && suffix=" (+architecture)"; [ "$escalated" = 1 ] && suffix="$suffix (content-escalated)"
  [ "$issue_raised" = 1 ] && suffix="$suffix (issue-intent: route:$ISSUE_ROUTE)"
  echo "route: ${base}${suffix}"
  echo "gates: ${gates// /, }"
fi
