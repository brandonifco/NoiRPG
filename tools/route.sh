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
#   tools/route.sh [--base <ref>] [--json] [file ...]
#
# File selection (first that applies):
#   explicit [file ...]   classify exactly those paths
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
FILES=()
while [ $# -gt 0 ]; do
  case "$1" in
    --json) JSON=1; shift ;;
    --base) BASE="${2:-}"; shift 2 ;;
    --)     shift; while [ $# -gt 0 ]; do FILES+=("$1"); shift; done ;;
    -*)     echo "unknown option: $1" >&2; exit 2 ;;
    *)      FILES+=("$1"); shift ;;
  esac
done

if [ ${#FILES[@]} -eq 0 ]; then
  if [ -n "$BASE" ]; then
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
    if [ -n "$BASE" ]; then
      d="$(git -C "$ROOT" diff --unified=0 "$BASE"...HEAD -- "${rf[@]}" 2>/dev/null || true)"
    else
      d="$(git -C "$ROOT" diff --unified=0 HEAD -- "${rf[@]}" 2>/dev/null || true)"
    fi
    if printf '%s' "$d" | grep -Eq '^[+-][^+-].*(=>[[:space:]]*-?[0-9]|:[[:space:]]*-?[0-9]+|\[[[:space:]]*-?[0-9]|(>=|<=|>|<)[[:space:]]*-?[0-9]|,[[:space:]]*-?[0-9]+)'; then
      base="formulas"; escalated=1
    fi
  fi
fi

case "$base" in
  docs | tooling) gates="ci scope-warden" ;;
  rules)          gates="ci scope-warden rules-conformance" ;;
  formulas)       gates="ci scope-warden rules-conformance codex-conformance" ;;
esac
[ "$arch" = 1 ] && gates="$gates architecture-review"

if [ "$JSON" = 1 ]; then
  gj=""; for x in $gates; do gj+="\"$x\","; done; gj="${gj%,}"
  fj=""; for x in "${FILES[@]}"; do [ -z "$x" ] && continue; fj+="\"$x\","; done; fj="${fj%,}"
  printf '{"route":"%s","architecture":%s,"escalated":%s,"gates":[%s],"files":[%s]}\n' \
    "$base" "$([ "$arch" = 1 ] && echo true || echo false)" \
    "$([ "$escalated" = 1 ] && echo true || echo false)" "$gj" "$fj"
else
  suffix=""; [ "$arch" = 1 ] && suffix=" (+architecture)"; [ "$escalated" = 1 ] && suffix="$suffix (content-escalated)"
  echo "route: ${base}${suffix}"
  echo "gates: ${gates// /, }"
fi
