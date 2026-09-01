#!/usr/bin/env bash
# tools/assign-adr-number.sh — merge-time ADR-number assignment (Issue #185, F8).
#
# Authors write a design-decision record with a placeholder number:
#   docs/decisions/NNNN-slug.md
# with an "NNNN" placeholder token wherever the number is referenced (the
# `# NNNN. Title` header, a `Status`/`Supersedes` line, and its own
# docs/decisions/README.md index row). Two authors on parallel branches can
# both write "NNNN" without colliding, because no number is actually chosen
# until this tool runs, once, at merge time, against the merged tree — see
# docs/decisions/0027-adr-number-allocation.md for the full rationale.
#
# What it does, for each docs/decisions/NNNN-*.md placeholder file found:
#   1. Computes the next free four-digit ADR number from the existing
#      docs/decisions/NNNN-*.md files and README.md index rows.
#   2. Renames the placeholder file to that number.
#   3. Rewrites the "NNNN" token to the assigned number everywhere inside
#      that file (header, self-references, its own filename mentioned in
#      prose all share the literal "NNNN" placeholder).
#   4. Rewrites the matching docs/decisions/README.md row(s) (identified by
#      the placeholder's old filename, so unrelated rows are untouched even
#      if more than one placeholder is in flight).
#   5. Rewrites any other tracked *.md file under the repo root (excluding
#      .git) that links to the placeholder's exact old filename.
#
# Multiple placeholders in the same directory are assigned sequential
# numbers in filename order, deterministically.
#
# Usage:
#   tools/assign-adr-number.sh [decisions-dir] [scan-root]
# Defaults: decisions-dir = docs/decisions, scan-root = repo root (used only
# for step 5, rewriting external references).
#
# Refuses (exit 1) if no NNNN-*.md placeholder exists in decisions-dir —
# there is nothing to assign. Running it again after a successful run is
# therefore a safe no-op: the placeholder is gone, so the second invocation
# refuses cleanly without touching any file a second time.

set -uo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
DIR="${1:-$ROOT/docs/decisions}"
SCAN_ROOT="${2:-$ROOT}"
README="$DIR/README.md"

die() { printf 'assign-adr-number: %s\n' "$1" >&2; exit 1; }

[ -d "$DIR" ] || die "no such decisions directory: $DIR"

mapfile -t placeholders < <(find "$DIR" -maxdepth 1 -name 'NNNN-*.md' | sort)
if [ "${#placeholders[@]}" -eq 0 ]; then
  die "no placeholder ADR found in $DIR (expected NNNN-slug.md) — nothing to assign"
fi

# --- Determine the current high-water mark -----------------------------
# Union of digits seen in docs/decisions/NNNN-*.md filenames and README.md
# index rows, so a drifted README (or a directory missing a file) still
# never causes a re-issued number.
highest=0
while IFS= read -r -d '' f; do
  base="$(basename "$f")"
  [ "$base" = "README.md" ] && continue
  num="${base%%-*}"
  [[ "$num" =~ ^[0-9]{4}$ ]] || continue
  n=$((10#$num))
  [ "$n" -gt "$highest" ] && highest=$n
done < <(find "$DIR" -maxdepth 1 -name '*.md' -print0)

if [ -f "$README" ]; then
  while IFS= read -r num; do
    [[ "$num" =~ ^[0-9]{4}$ ]] || continue
    n=$((10#$num))
    [ "$n" -gt "$highest" ] && highest=$n
  done < <(grep -oE '^\| \[[0-9]{4}\]' "$README" 2>/dev/null | grep -oE '[0-9]{4}')
fi

next="$highest"

for old_path in "${placeholders[@]}"; do
  next=$((next + 1))
  num="$(printf '%04d' "$next")"

  old_name="$(basename "$old_path")"      # NNNN-slug.md
  slug_suffix="${old_name#NNNN-}"         # slug.md
  new_name="${num}-${slug_suffix}"        # 0027-slug.md
  new_path="$DIR/$new_name"

  [ -e "$new_path" ] && die "refusing to overwrite existing file: $new_path"

  mv -- "$old_path" "$new_path"

  # Step 3: rewrite the literal placeholder token inside the renamed file.
  # "NNNN" is not a real word and appears only as this placeholder, so a
  # global replace is safe and also correctly updates any prose mention of
  # the file's own filename (which shares the same "NNNN-slug.md" text).
  sed -i "s/NNNN/${num}/g" "$new_path"

  # Step 4: rewrite only the README row(s) that reference this placeholder's
  # old filename, so a second in-flight placeholder with a different slug
  # (and therefore a different old_name) is left untouched.
  if [ -f "$README" ]; then
    esc_old_name="$(printf '%s' "$old_name" | sed 's/[.[\*^$]/\\&/g')"
    sed -i "/${esc_old_name}/s/NNNN/${num}/g" "$README"
  fi

  # Step 5: rewrite any other tracked markdown file that links to the exact
  # old filename (e.g. a cross-reference from another doc).
  if [ -d "$SCAN_ROOT" ]; then
    esc_old_name_lit="$(printf '%s' "$old_name" | sed 's/[.[\*^$]/\\&/g')"
    while IFS= read -r -d '' f; do
      [ "$f" = "$new_path" ] && continue
      [ "$f" = "$README" ] && continue
      grep -q "$old_name" "$f" 2>/dev/null || continue
      sed -i "s/${esc_old_name_lit}/${new_name}/g" "$f"
    done < <(find "$SCAN_ROOT" -name '*.md' -not -path '*/.git/*' -print0 2>/dev/null)
  fi

  printf 'assign-adr-number: %s -> %s\n' "$old_name" "$new_name"
done
