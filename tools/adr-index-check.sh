#!/usr/bin/env bash
# tools/adr-index-check.sh — deterministic decision-index drift guard (Issue #189).
#
# Checks that docs/decisions/README.md's index table and the docs/decisions/
# directory agree:
#   1. no ADR number appears in the index more than once (duplicate row),
#   2. the index numbers are contiguous starting at the lowest one present
#      (no gap),
#   3. every index row has a matching docs/decisions/NNNN-*.md file, and
#   4. every docs/decisions/NNNN-*.md file has a matching index row.
#
# Detection only (Issue #185 covers allocation/collision prevention at
# authoring time). Usage:
#   tools/adr-index-check.sh [decisions-dir]
# Defaults to docs/decisions relative to the repo root.

set -uo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
DIR="${1:-$ROOT/docs/decisions}"
README="$DIR/README.md"

fail=0
ok()  { printf '  ok   %s\n' "$1"; }
bad() { printf 'FAIL   %s\n' "$1"; fail=1; }

if [ ! -f "$README" ]; then
  bad "missing index file: $README"
  echo "adr-index-check: FAIL"
  exit 1
fi

# --- Collect index-row ADR numbers ------------------------------------------
# Row shape: | [0024](0024-hit-locations.md) | ... | ... |
mapfile -t rows < <(grep -oE '^\| \[[0-9]{4}\]\([0-9]{4}-[^)]+\.md\)' "$README")

declare -A row_count=()
declare -A row_file_mismatch=()
row_numbers=()
for row in "${rows[@]}"; do
  link_num="$(printf '%s' "$row" | grep -oE '^\| \[[0-9]{4}\]' | grep -oE '[0-9]{4}')"
  file_ref="$(printf '%s' "$row" | grep -oE '\([0-9]{4}-[^)]+\.md\)' | tr -d '()')"
  file_num="${file_ref%%-*}"
  row_numbers+=("$link_num")
  row_count["$link_num"]=$(( ${row_count["$link_num"]:-0} + 1 ))
  if [ "$link_num" != "$file_num" ]; then
    row_file_mismatch["$link_num"]="$file_ref"
  fi
done

if [ "${#row_numbers[@]}" -eq 0 ]; then
  bad "no index rows found in $README (expected '| [NNNN](NNNN-slug.md) | ... |' rows)"
fi

# 1. Duplicates
for num in "${!row_count[@]}"; do
  if [ "${row_count[$num]}" -gt 1 ]; then
    bad "duplicate ADR number in index: $num appears ${row_count[$num]} times"
  fi
done

# 2. Gaps (contiguous numbering starting at the lowest index number present)
if [ "${#row_numbers[@]}" -gt 0 ]; then
  mapfile -t sorted_unique < <(printf '%s\n' "${row_numbers[@]}" | sort -un)
  lo="${sorted_unique[0]}"
  hi="${sorted_unique[-1]}"
  lo_dec=$((10#$lo))
  hi_dec=$((10#$hi))
  missing=""
  for ((n = lo_dec; n <= hi_dec; n++)); do
    padded="$(printf '%04d' "$n")"
    found=0
    for num in "${sorted_unique[@]}"; do
      if [ "$num" = "$padded" ]; then found=1; break; fi
    done
    if [ "$found" -eq 0 ]; then missing="$missing $padded"; fi
  done
  if [ -n "$missing" ]; then
    bad "gap in ADR numbering between $lo and $hi — missing:$missing"
  fi
fi

# 3. Index row -> file must exist
for num in "${row_numbers[@]}"; do
  match="$(find "$DIR" -maxdepth 1 -name "${num}-*.md" 2>/dev/null)"
  if [ -z "$match" ]; then
    bad "index row for $num has no matching file $DIR/${num}-*.md"
  fi
done
for num in "${!row_file_mismatch[@]}"; do
  bad "index row link number $num does not match its own filename reference (${row_file_mismatch[$num]})"
done

# 4. File -> index row must exist
declare -A row_present=()
for num in "${row_numbers[@]}"; do row_present["$num"]=1; done
while IFS= read -r -d '' f; do
  base="$(basename "$f")"
  [ "$base" = "README.md" ] && continue
  num="${base%%-*}"
  if [[ ! "$num" =~ ^[0-9]{4}$ ]]; then
    bad "unrecognized decision file name (expected NNNN-slug.md): $base"
    continue
  fi
  if [ -z "${row_present[$num]:-}" ]; then
    bad "$base has no matching index row for $num in README.md"
  fi
done < <(find "$DIR" -maxdepth 1 -name '*.md' -print0)

if [ "$fail" = 0 ]; then
  ok "decision index consistent: ${#row_numbers[@]} rows, no duplicates, no gaps, 1:1 with files"
  echo "adr-index-check: PASS"
else
  echo "adr-index-check: FAIL"
fi
exit "$fail"
