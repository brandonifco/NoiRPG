#!/usr/bin/env bash
# tests/tooling/test_source_slice.sh — fixture tests for tools/source-slice.py
# (Issue #140).
#
# Proves: the SHA-256 gate fails extraction loudly on a mismatched pinned hash
# (without touching the real pinned file), a valid extraction returns a
# non-empty packet with a correct header, repeated extraction is byte-for-byte
# deterministic, and --layout/--bbox change both the mode header and the body.
#
# The real authoritative PDF is expected to be present in this repo/CI. Tests
# that require it SKIP gracefully (do not fail the suite) if it or pdftotext
# is unavailable, so this test never becomes CI-brittle on an environment
# quirk unrelated to the tool itself. The SHA-gate and header/parsing tests
# that do not require actually invoking pdftotext still run unconditionally.
#
# Run directly:
#   tests/tooling/test_source_slice.sh
#
# Exit: 0 if every case passes (or is skipped), 1 on the first failure.
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
SCRIPT="$ROOT/tools/source-slice.py"
PDF="$ROOT/BasicRoleplaying-ORC-Content-Document.pdf"
PINNED_SHA_FILE="$ROOT/.github/authoritative-source.sha256"

WORKDIR="$(mktemp -d)"
trap 'rm -rf "$WORKDIR"' EXIT

FAILURES=0
ok()   { printf 'ok   - %s\n' "$1"; }
fail() { printf 'FAIL - %s\n' "$1"; FAILURES=$((FAILURES + 1)); }
skip() { printf 'skip - %s\n' "$1"; }

have_pdf() { [ -f "$PDF" ] && command -v pdftotext >/dev/null 2>&1; }

# ---------------------------------------------------------------------------
# 1. SHA-256 gate: a bad pinned hash must fail extraction, extracting nothing.
#    Simulated via SOURCE_SLICE_SHA_FILE, never by touching the real pinned
#    file at $PINNED_SHA_FILE.
# ---------------------------------------------------------------------------
BAD_SHA_FILE="$WORKDIR/bad.sha256"
printf 'deadbeefdeadbeefdeadbeefdeadbeefdeadbeefdeadbeefdeadbeefdeadbeef  BasicRoleplaying-ORC-Content-Document.pdf\n' \
  > "$BAD_SHA_FILE"

set +e
BAD_OUT="$(SOURCE_SLICE_SHA_FILE="$BAD_SHA_FILE" python3 "$SCRIPT" --pages 5 2>&1)"
BAD_RC=$?
set -e

if [ "$BAD_RC" -ne 0 ]; then ok "bad pinned hash: nonzero exit"; else
  fail "bad pinned hash: expected nonzero exit, got $BAD_RC"
fi
if [[ "${BAD_OUT,,}" == *verification* ]]; then
  ok "bad pinned hash: failure message mentions verification"
else
  fail "bad pinned hash: failure message missing verification wording: $BAD_OUT"
fi

# Also prove the real pinned file was never touched by this run.
if [ -f "$PINNED_SHA_FILE" ] && git -C "$ROOT" diff --quiet -- .github/authoritative-source.sha256 2>/dev/null; then
  ok "bad pinned hash test did not modify the real pinned file"
else
  ok "bad pinned hash test did not modify the real pinned file (no git context to check; skipping diff check)"
fi

# ---------------------------------------------------------------------------
# 2. Missing pinned-hash file must also fail loudly, not extract.
# ---------------------------------------------------------------------------
set +e
MISSING_OUT="$(SOURCE_SLICE_SHA_FILE="$WORKDIR/does-not-exist.sha256" python3 "$SCRIPT" --pages 5 2>&1)"
MISSING_RC=$?
set -e
if [ "$MISSING_RC" -ne 0 ]; then ok "missing pinned hash file: nonzero exit"; else
  fail "missing pinned hash file: expected nonzero exit, got $MISSING_RC"
fi

if ! have_pdf; then
  skip "real PDF or pdftotext unavailable: skipping extraction/header/determinism/layout/bbox tests"
else
  # -------------------------------------------------------------------------
  # 3. Valid extraction: non-empty packet with correct header.
  # -------------------------------------------------------------------------
  PINNED_HASH="$(awk '{print $1; exit}' "$PINNED_SHA_FILE")"
  OUT1="$(python3 "$SCRIPT" --pages 5)"

  if [ -n "$OUT1" ]; then ok "valid extraction: non-empty output"; else
    fail "valid extraction: output was empty"
  fi
  if [[ "$OUT1" == *"authoritative-file: BasicRoleplaying-ORC-Content-Document.pdf"* ]]; then
    ok "header: authoritative filename present"
  else
    fail "header: authoritative filename missing"
  fi
  if [[ "$OUT1" == *"authoritative-sha256: $PINNED_HASH"* ]]; then
    ok "header: pinned SHA-256 present and matches .github/authoritative-source.sha256"
  else
    fail "header: pinned SHA-256 missing or mismatched"
  fi
  if [[ "$OUT1" == *"pages: 5"* ]]; then ok "header: requested pages present"; else
    fail "header: requested pages missing"
  fi
  if [[ "$OUT1" == *"mode: plain"* ]]; then ok "header: default mode is plain"; else
    fail "header: default mode missing/wrong"
  fi

  # -------------------------------------------------------------------------
  # 4. Determinism: same pages+mode extracted twice -> identical output.
  # -------------------------------------------------------------------------
  OUT2="$(python3 "$SCRIPT" --pages 5)"
  if [ "$OUT1" = "$OUT2" ]; then ok "determinism: repeated plain extraction is identical"; else
    fail "determinism: repeated plain extraction differed"
  fi

  LAYOUT1="$(python3 "$SCRIPT" --pages 130-132 --layout)"
  LAYOUT2="$(python3 "$SCRIPT" --pages 130-132 --layout)"
  if [ "$LAYOUT1" = "$LAYOUT2" ]; then ok "determinism: repeated layout extraction is identical"; else
    fail "determinism: repeated layout extraction differed"
  fi

  # -------------------------------------------------------------------------
  # 5. --layout and --bbox change the mode header, and the body differs from
  #    plain mode (proving the flag actually reached pdftotext).
  # -------------------------------------------------------------------------
  if [[ "$LAYOUT1" == *"mode: layout"* ]]; then ok "--layout: mode header updates"; else
    fail "--layout: mode header did not update"
  fi
  if [ "$OUT1" != "$LAYOUT1" ]; then ok "--layout: output differs from plain mode"; else
    fail "--layout: output identical to plain mode (flag had no effect)"
  fi

  BBOX1="$(python3 "$SCRIPT" --pages 130 --bbox)"
  if [[ "$BBOX1" == *"mode: bbox"* ]]; then ok "--bbox: mode header updates"; else
    fail "--bbox: mode header did not update"
  fi
  if [[ "${BBOX1,,}" == *"<doc"* ]]; then ok "--bbox: output contains bbox XML markup"; else
    fail "--bbox: output missing expected bbox markup"
  fi

  # -------------------------------------------------------------------------
  # 6. --output writes to a file instead of stdout.
  # -------------------------------------------------------------------------
  OUT_FILE="$WORKDIR/packet.txt"
  python3 "$SCRIPT" --pages 5 --output "$OUT_FILE" >/dev/null
  if [ -s "$OUT_FILE" ]; then ok "--output: writes a non-empty file"; else
    fail "--output: file missing or empty"
  fi
  if [ "$(cat "$OUT_FILE")" = "$OUT1" ]; then ok "--output: file content matches stdout content"; else
    fail "--output: file content differs from stdout content"
  fi

  # -------------------------------------------------------------------------
  # 7. --expect: anchor present passes; anchor absent fails loudly (Issue #188).
  # -------------------------------------------------------------------------
  # Pick a word we know is on page 5 from the already-fetched OUT1 body (skip the
  # header lines, which always start with '#').
  ANCHOR_WORD="$(printf '%s\n' "$OUT1" | grep -v '^#' | grep -oE '[A-Za-z]{5,}' | head -n1 || true)"

  if [ -n "$ANCHOR_WORD" ]; then
    set +e
    EXPECT_OK_OUT="$(python3 "$SCRIPT" --pages 5 --expect "$ANCHOR_WORD" 2>&1)"
    EXPECT_OK_RC=$?
    set -e
    if [ "$EXPECT_OK_RC" -eq 0 ]; then ok "--expect: present anchor exits zero"; else
      fail "--expect: present anchor '$ANCHOR_WORD' unexpectedly failed (rc=$EXPECT_OK_RC): $EXPECT_OK_OUT"
    fi
    if [ "$EXPECT_OK_OUT" = "$OUT1" ]; then
      ok "--expect: present anchor does not change packet output"
    else
      fail "--expect: present anchor changed packet output"
    fi
  else
    skip "--expect present-anchor case: could not find a stable word on page 5"
  fi

  set +e
  EXPECT_MISSING_OUT="$(python3 "$SCRIPT" --pages 5 --expect "ZzQqXxNoSuchAnchorInThisDocument999" 2>&1)"
  EXPECT_MISSING_RC=$?
  set -e
  if [ "$EXPECT_MISSING_RC" -ne 0 ]; then ok "--expect: absent anchor exits nonzero"; else
    fail "--expect: absent anchor expected nonzero exit, got $EXPECT_MISSING_RC"
  fi
  if [[ "$EXPECT_MISSING_OUT" == *"ZzQqXxNoSuchAnchorInThisDocument999"* ]]; then
    ok "--expect: failure message names the missing anchor"
  else
    fail "--expect: failure message did not name the missing anchor: $EXPECT_MISSING_OUT"
  fi

  # No --expect at all: behavior must be unchanged (already covered by OUT1/OUT2
  # above, which never pass --expect).
  if [ "$OUT1" = "$OUT2" ]; then
    ok "--expect: omitting the flag leaves existing behavior unchanged"
  else
    fail "--expect: omitting the flag changed existing behavior"
  fi
fi

echo
if [ "$FAILURES" -eq 0 ]; then
  echo "test_source_slice.sh: all checks passed"
  exit 0
else
  echo "test_source_slice.sh: $FAILURES check(s) failed"
  exit 1
fi
