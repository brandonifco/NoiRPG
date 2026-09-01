#!/usr/bin/env bash
# tests/tooling/test_adr_index_check.sh — fixture tests for
# tools/adr-index-check.sh (Issue #189).
#
# Proves the deterministic decision-index drift guard fails on a duplicate
# ADR number, a numbering gap, an index row with no matching file, and an
# ADR file with no index row — and passes on a clean index. Also proves it
# passes against the real docs/decisions/ on this branch.
#
# Run directly:
#   tests/tooling/test_adr_index_check.sh

set -uo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
SCRIPT="$ROOT/tools/adr-index-check.sh"

WORKDIR="$(mktemp -d)"
trap 'rm -rf "$WORKDIR"' EXIT

FAILURES=0
ok()   { printf 'ok   - %s\n' "$1"; }
fail() { printf 'FAIL - %s\n' "$1"; FAILURES=$((FAILURES + 1)); }

# ---------------------------------------------------------------------------
# Helper: build a fixture decisions dir from a heredoc README body plus a
# list of filenames to create (each gets minimal placeholder content).
# ---------------------------------------------------------------------------
make_fixture() {
  local name="$1"; shift
  local dir="$WORKDIR/$name"
  mkdir -p "$dir"
  cat > "$dir/README.md"
  for f in "$@"; do
    printf '# %s\n\nStub decision record.\n' "$f" > "$dir/$f"
  done
  printf '%s' "$dir"
}

run_check() {
  local dir="$1"
  set +e
  OUT="$("$SCRIPT" "$dir" 2>&1)"
  RC=$?
  set -e
}

# ---------------------------------------------------------------------------
# 1. Clean index (contiguous, 1:1) — must PASS.
# ---------------------------------------------------------------------------
CLEAN_DIR="$(make_fixture clean \
  0001-first.md 0002-second.md 0003-third.md <<'EOF'
| # | Decision | Status |
|---|---|---|
| [0001](0001-first.md) | First | Accepted |
| [0002](0002-second.md) | Second | Accepted |
| [0003](0003-third.md) | Third | Accepted |
EOF
)"
run_check "$CLEAN_DIR"
if [ "$RC" -eq 0 ]; then ok "clean index: passes"; else fail "clean index: expected pass, got FAIL: $OUT"; fi

# ---------------------------------------------------------------------------
# 2. Duplicate ADR number in the index — must FAIL.
# ---------------------------------------------------------------------------
DUP_DIR="$(make_fixture duplicate \
  0001-first.md 0002-second.md <<'EOF'
| # | Decision | Status |
|---|---|---|
| [0001](0001-first.md) | First | Accepted |
| [0002](0002-second.md) | Second | Accepted |
| [0002](0002-second.md) | Second (again) | Accepted |
EOF
)"
run_check "$DUP_DIR"
if [ "$RC" -ne 0 ]; then ok "duplicate ADR number: fails"; else fail "duplicate ADR number: expected FAIL, got pass"; fi
if [[ "${OUT,,}" == *duplicate* ]]; then ok "duplicate ADR number: message mentions duplicate"; else fail "duplicate ADR number: message missing 'duplicate': $OUT"; fi

# ---------------------------------------------------------------------------
# 3. Numbering gap — must FAIL.
# ---------------------------------------------------------------------------
GAP_DIR="$(make_fixture gap \
  0001-first.md 0003-third.md <<'EOF'
| # | Decision | Status |
|---|---|---|
| [0001](0001-first.md) | First | Accepted |
| [0003](0003-third.md) | Third | Accepted |
EOF
)"
run_check "$GAP_DIR"
if [ "$RC" -ne 0 ]; then ok "numbering gap: fails"; else fail "numbering gap: expected FAIL, got pass"; fi
if [[ "${OUT,,}" == *gap* ]]; then ok "numbering gap: message mentions gap"; else fail "numbering gap: message missing 'gap': $OUT"; fi

# ---------------------------------------------------------------------------
# 4. Orphan index row (no matching file) — must FAIL.
# ---------------------------------------------------------------------------
ORPHAN_ROW_DIR="$(make_fixture orphan_row \
  0001-first.md <<'EOF'
| # | Decision | Status |
|---|---|---|
| [0001](0001-first.md) | First | Accepted |
| [0002](0002-second.md) | Second | Accepted |
EOF
)"
run_check "$ORPHAN_ROW_DIR"
if [ "$RC" -ne 0 ]; then ok "orphan index row: fails"; else fail "orphan index row: expected FAIL, got pass"; fi
if [[ "${OUT,,}" == *"no matching file"* ]]; then ok "orphan index row: message mentions missing file"; else fail "orphan index row: message missing expected text: $OUT"; fi

# ---------------------------------------------------------------------------
# 5. Orphan file (no index row) — must FAIL.
# ---------------------------------------------------------------------------
ORPHAN_FILE_DIR="$(make_fixture orphan_file \
  0001-first.md 0002-second.md <<'EOF'
| # | Decision | Status |
|---|---|---|
| [0001](0001-first.md) | First | Accepted |
EOF
)"
run_check "$ORPHAN_FILE_DIR"
if [ "$RC" -ne 0 ]; then ok "orphan file: fails"; else fail "orphan file: expected FAIL, got pass"; fi
if [[ "${OUT,,}" == *"no matching index row"* ]]; then ok "orphan file: message mentions missing index row"; else fail "orphan file: message missing expected text: $OUT"; fi

# ---------------------------------------------------------------------------
# 6. The real repo's docs/decisions/ must PASS (the acceptance-critical case).
# ---------------------------------------------------------------------------
run_check "$ROOT/docs/decisions"
if [ "$RC" -eq 0 ]; then ok "real docs/decisions/: passes"; else fail "real docs/decisions/: expected pass, got FAIL: $OUT"; fi

echo
if [ "$FAILURES" -eq 0 ]; then
  echo "test_adr_index_check: ALL PASS"
  exit 0
else
  echo "test_adr_index_check: $FAILURES FAILURE(S)"
  exit 1
fi
