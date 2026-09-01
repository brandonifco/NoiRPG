#!/usr/bin/env bash
# tests/tooling/test_codex_agent.sh — fixture tests for tools/codex-agent.sh's
# packet-first invocation (Issue #142).
#
# Proves, entirely via DRY_RUN=1 (never invoking the real Codex binary):
#   - each role requires its packet flag(s) and fails loudly without them
#   - the composed command always uses a read-only sandbox
#   - `conformance` includes the SOURCE and REVIEW packet content in the prompt,
#     but never injects a Claude/rules-conformance verdict or reasoning
#   - `review` includes the REVIEW packet content
#   - `simcheck` takes --packet and includes its content
#   - a prompt-sha256 is always printed
#   - `--check` / `preflight` report Codex availability via CODEX_BIN (never
#     `which codex`): exit 0 when present, non-zero with a clear message when
#     absent, honoring a CODEX_BIN override
#
# Run directly:
#   tests/tooling/test_codex_agent.sh
#
# Exit: 0 if every case passes, 1 on the first failure.
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
SCRIPT="$ROOT/tools/codex-agent.sh"

WORKDIR="$(mktemp -d)"
trap 'rm -rf "$WORKDIR"' EXIT

FAILURES=0
ok()   { printf 'ok   - %s\n' "$1"; }
fail() { printf 'FAIL - %s\n' "$1"; FAILURES=$((FAILURES + 1)); }

assert_contains() {
  local desc="$1" haystack="$2" needle="$3"
  if [[ "$haystack" == *"$needle"* ]]; then ok "$desc"; else
    fail "$desc (expected to find [$needle])"
  fi
}

assert_not_contains() {
  local desc="$1" haystack="$2" needle="$3"
  if [[ "$haystack" != *"$needle"* ]]; then ok "$desc"; else
    fail "$desc (expected NOT to find [$needle])"
  fi
}

SOURCE_PACKET="$WORKDIR/source.txt"
REVIEW_PACKET="$WORKDIR/review.txt"
cat > "$SOURCE_PACKET" <<'EOF'
authoritative-file: BasicRoleplaying-ORC-Content-Document.pdf
pages: 130
UNIQUE-SOURCE-MARKER: the printed Resistance Table text goes here
EOF
cat > "$REVIEW_PACKET" <<'EOF'
# REVIEW BRIEF — Add resistance table
UNIQUE-REVIEW-MARKER: diff, route, and gate checklist go here
EOF

# ---------------------------------------------------------------------------
# 1. Each role fails loudly without its required packet flag(s).
# ---------------------------------------------------------------------------
set +e
OUT="$(DRY_RUN=1 "$SCRIPT" conformance 2>&1)"; RC=$?
set -e
[ "$RC" -ne 0 ] && ok "conformance: missing packets -> nonzero exit" || fail "conformance: missing packets should fail"
assert_contains "conformance: missing-packet message names --source-packet" "$OUT" "source-packet"

set +e
OUT="$(DRY_RUN=1 "$SCRIPT" conformance --source-packet "$SOURCE_PACKET" 2>&1)"; RC=$?
set -e
[ "$RC" -ne 0 ] && ok "conformance: review packet still required -> nonzero exit" || fail "conformance: missing review-packet should fail"
assert_contains "conformance: missing-packet message names --review-packet" "$OUT" "review-packet"

set +e
OUT="$(DRY_RUN=1 "$SCRIPT" review 2>&1)"; RC=$?
set -e
[ "$RC" -ne 0 ] && ok "review: missing --review-packet -> nonzero exit" || fail "review: missing packet should fail"
assert_contains "review: missing-packet message names --review-packet" "$OUT" "review-packet"

set +e
OUT="$(DRY_RUN=1 "$SCRIPT" simcheck 2>&1)"; RC=$?
set -e
[ "$RC" -ne 0 ] && ok "simcheck: missing --packet -> nonzero exit" || fail "simcheck: missing packet should fail"
assert_contains "simcheck: missing-packet message names --packet" "$OUT" "--packet"

set +e
OUT="$(DRY_RUN=1 "$SCRIPT" conformance --review-packet "$REVIEW_PACKET" --source-packet "$WORKDIR/nope.txt" 2>&1)"; RC=$?
set -e
[ "$RC" -ne 0 ] && ok "conformance: nonexistent packet file -> nonzero exit" || fail "conformance: nonexistent packet file should fail"

# ---------------------------------------------------------------------------
# 2. Unknown role fails with usage.
# ---------------------------------------------------------------------------
set +e
OUT="$(DRY_RUN=1 "$SCRIPT" bogus-role 2>&1)"; RC=$?
set -e
[ "$RC" -ne 0 ] && ok "unknown role -> nonzero exit" || fail "unknown role should fail"
assert_contains "unknown role: usage printed" "$OUT" "usage:"

# ---------------------------------------------------------------------------
# 3. conformance: sandbox is read-only; prompt carries both packets, a
#    prompt-sha256, and NO injected verifier reasoning.
# ---------------------------------------------------------------------------
CONF_OUT="$(DRY_RUN=1 "$SCRIPT" conformance --review-packet "$REVIEW_PACKET" --source-packet "$SOURCE_PACKET")"
assert_contains "conformance: sandbox is read-only" "$CONF_OUT" "-s read-only"
assert_contains "conformance: includes source packet content" "$CONF_OUT" "UNIQUE-SOURCE-MARKER"
assert_contains "conformance: includes review packet content" "$CONF_OUT" "UNIQUE-REVIEW-MARKER"
assert_contains "conformance: prints a prompt-sha256" "$CONF_OUT" "prompt-sha256:"
assert_not_contains "conformance: no rules-conformance verdict/reasoning injected" "$CONF_OUT" "rules-conformance's"
assert_not_contains "conformance: no 'Claude concluded' style injection" "$CONF_OUT" "Claude concluded"
assert_not_contains "conformance: does not tell Codex to read AGENTS.md itself" "$CONF_OUT" "Read \`AGENTS.md\` first"
assert_contains "conformance: instructs independent re-derivation" "$CONF_OUT" "must not seek out"

# ---------------------------------------------------------------------------
# 4. review: sandbox is read-only; prompt carries the review packet content.
# ---------------------------------------------------------------------------
REVIEW_OUT="$(DRY_RUN=1 "$SCRIPT" review --review-packet "$REVIEW_PACKET")"
assert_contains "review: sandbox is read-only" "$REVIEW_OUT" "-s read-only"
assert_contains "review: includes review packet content" "$REVIEW_OUT" "UNIQUE-REVIEW-MARKER"
assert_contains "review: prints a prompt-sha256" "$REVIEW_OUT" "prompt-sha256:"

# ---------------------------------------------------------------------------
# 5. simcheck: sandbox is read-only; --packet content included.
# ---------------------------------------------------------------------------
SIM_OUT="$(DRY_RUN=1 "$SCRIPT" simcheck --packet "$SOURCE_PACKET")"
assert_contains "simcheck: sandbox is read-only" "$SIM_OUT" "-s read-only"
assert_contains "simcheck: includes packet content" "$SIM_OUT" "UNIQUE-SOURCE-MARKER"
assert_contains "simcheck: prints a prompt-sha256" "$SIM_OUT" "prompt-sha256:"

# ---------------------------------------------------------------------------
# 6. DRY_RUN never invokes the real Codex binary — prove by pointing CODEX_BIN
#    at a nonexistent path and confirming DRY_RUN still succeeds.
# ---------------------------------------------------------------------------
set +e
OUT="$(DRY_RUN=1 CODEX_BIN="$WORKDIR/no-such-codex-binary" "$SCRIPT" simcheck --packet "$SOURCE_PACKET" 2>&1)"; RC=$?
set -e
[ "$RC" -eq 0 ] && ok "DRY_RUN succeeds even with a nonexistent CODEX_BIN (never invoked)" || \
  fail "DRY_RUN should succeed without a real codex binary (rc=$RC): $OUT"

# ---------------------------------------------------------------------------
# 7. --check / preflight: reports availability via CODEX_BIN, never `which`.
# ---------------------------------------------------------------------------
STUB_CODEX="$WORKDIR/stub-codex"
cat > "$STUB_CODEX" <<'EOF'
#!/usr/bin/env bash
echo "stub codex"
EOF
chmod +x "$STUB_CODEX"

set +e
OUT="$(CODEX_BIN="$STUB_CODEX" "$SCRIPT" --check 2>&1)"; RC=$?
set -e
[ "$RC" -eq 0 ] && ok "--check: present CODEX_BIN -> exit 0" || fail "--check: present CODEX_BIN should exit 0 (rc=$RC): $OUT"
assert_contains "--check: present CODEX_BIN reports the path" "$OUT" "$STUB_CODEX"

set +e
OUT="$(CODEX_BIN="$STUB_CODEX" "$SCRIPT" preflight 2>&1)"; RC=$?
set -e
[ "$RC" -eq 0 ] && ok "preflight: present CODEX_BIN -> exit 0" || fail "preflight: present CODEX_BIN should exit 0 (rc=$RC): $OUT"

set +e
OUT="$(CODEX_BIN=/nonexistent "$SCRIPT" --check 2>&1)"; RC=$?
set -e
[ "$RC" -ne 0 ] && ok "--check: absent CODEX_BIN -> nonzero exit" || fail "--check: absent CODEX_BIN should fail (rc=$RC): $OUT"
assert_contains "--check: absent CODEX_BIN reports a clear message" "$OUT" "/nonexistent"

set +e
OUT="$(CODEX_BIN=/nonexistent "$SCRIPT" preflight 2>&1)"; RC=$?
set -e
[ "$RC" -ne 0 ] && ok "preflight: absent CODEX_BIN -> nonzero exit" || fail "preflight: absent CODEX_BIN should fail (rc=$RC): $OUT"

echo
if [ "$FAILURES" -eq 0 ]; then
  echo "test_codex_agent.sh: all checks passed"
  exit 0
else
  echo "test_codex_agent.sh: $FAILURES check(s) failed"
  exit 1
fi
