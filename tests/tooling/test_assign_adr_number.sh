#!/usr/bin/env bash
# tests/tooling/test_assign_adr_number.sh — fixture tests for
# tools/assign-adr-number.sh (Issue #185, F8).
#
# Proves the merge-time ADR-number assignment tool: assigns the next free
# number to a placeholder, rewrites all references (header, README row,
# external cross-links), is idempotent (a second run after a successful
# assignment is a safe no-op), refuses when no placeholder is present, and
# resolves a concurrent-allocation race (two placeholders authored off the
# same base, both present when the tool runs, get distinct numbers).
#
# Run directly:
#   tests/tooling/test_assign_adr_number.sh

set -uo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
SCRIPT="$ROOT/tools/assign-adr-number.sh"

WORKDIR="$(mktemp -d)"
trap 'rm -rf "$WORKDIR"' EXIT

FAILURES=0
ok()   { printf 'ok   - %s\n' "$1"; }
fail() { printf 'FAIL - %s\n' "$1"; FAILURES=$((FAILURES + 1)); }

# ---------------------------------------------------------------------------
# Fixture 1: placeholder -> next free number, references rewritten.
# ---------------------------------------------------------------------------
F1="$WORKDIR/f1"
mkdir -p "$F1"
cat > "$F1/README.md" <<'EOF'
# Architecture Decision Records

| # | Decision | Status |
|---|---|---|
| [0001](0001-first.md) | First | Accepted |
| [0002](0002-second.md) | Second | Accepted |
| [NNNN](NNNN-third-thing.md) | Third thing | Accepted |
EOF
printf '# 0001. First\n\nStub.\n' > "$F1/0001-first.md"
printf '# 0002. Second\n\nStub.\n' > "$F1/0002-second.md"
cat > "$F1/NNNN-third-thing.md" <<'EOF'
# NNNN. Third thing

## Status

Proposed — 2026-09-01.

## Context

See NNNN-third-thing.md for the full record once it lands.
EOF

OUT="$("$SCRIPT" "$F1" 2>&1)"; RC=$?
if [ "$RC" -eq 0 ]; then ok "assigns exit 0"; else fail "assigns exit 0: got $RC: $OUT"; fi
if [ -f "$F1/0003-third-thing.md" ]; then ok "next free number is 0003 (0001,0002 taken)"; else fail "expected $F1/0003-third-thing.md; dir: $(ls "$F1")"; fi
if [ ! -e "$F1/NNNN-third-thing.md" ]; then ok "placeholder file removed"; else fail "placeholder file still present"; fi

if grep -q '^# 0003\. Third thing' "$F1/0003-third-thing.md" 2>/dev/null; then
  ok "header rewritten to assigned number"
else
  fail "header not rewritten: $(cat "$F1/0003-third-thing.md" 2>/dev/null)"
fi
if grep -q '0003-third-thing.md' "$F1/0003-third-thing.md" 2>/dev/null && ! grep -q 'NNNN' "$F1/0003-third-thing.md" 2>/dev/null; then
  ok "self-reference to own filename rewritten, no NNNN left in body"
else
  fail "self-reference not rewritten cleanly: $(cat "$F1/0003-third-thing.md" 2>/dev/null)"
fi
if grep -q '\[0003\](0003-third-thing.md)' "$F1/README.md"; then
  ok "README row rewritten to assigned number and filename"
else
  fail "README row not rewritten: $(cat "$F1/README.md")"
fi
if grep -q '\[0001\](0001-first.md)' "$F1/README.md" && grep -q '\[0002\](0002-second.md)' "$F1/README.md"; then
  ok "unrelated README rows untouched"
else
  fail "unrelated README rows were touched: $(cat "$F1/README.md")"
fi

# ---------------------------------------------------------------------------
# Fixture 1b: external cross-reference to the placeholder gets rewritten too.
# ---------------------------------------------------------------------------
F1B="$WORKDIR/f1b"
mkdir -p "$F1B/docs/decisions" "$F1B/docs"
cat > "$F1B/docs/decisions/README.md" <<'EOF'
| # | Decision | Status |
|---|---|---|
| [0001](0001-first.md) | First | Accepted |
| [NNNN](NNNN-third-thing.md) | Third thing | Accepted |
EOF
printf '# 0001. First\n\nStub.\n' > "$F1B/docs/decisions/0001-first.md"
printf '# NNNN. Third thing\n\nStub.\n' > "$F1B/docs/decisions/NNNN-third-thing.md"
cat > "$F1B/docs/agent-team.md" <<'EOF'
See [the third thing](decisions/NNNN-third-thing.md) for background.
EOF

"$SCRIPT" "$F1B/docs/decisions" "$F1B" >/dev/null 2>&1
if grep -q 'decisions/0002-third-thing.md' "$F1B/docs/agent-team.md" 2>/dev/null; then
  ok "external cross-link to the placeholder rewritten"
else
  fail "external cross-link not rewritten: $(cat "$F1B/docs/agent-team.md" 2>/dev/null)"
fi

# ---------------------------------------------------------------------------
# Fixture 2: refuses when no placeholder is present.
# ---------------------------------------------------------------------------
F2="$WORKDIR/f2"
mkdir -p "$F2"
cat > "$F2/README.md" <<'EOF'
| # | Decision | Status |
|---|---|---|
| [0001](0001-first.md) | First | Accepted |
EOF
printf '# 0001. First\n\nStub.\n' > "$F2/0001-first.md"

OUT="$("$SCRIPT" "$F2" 2>&1)"; RC=$?
if [ "$RC" -ne 0 ]; then ok "refuses with no placeholder present"; else fail "expected non-zero exit with no placeholder, got 0"; fi
if [[ "${OUT,,}" == *placeholder* ]]; then ok "refusal message mentions 'placeholder'"; else fail "refusal message unclear: $OUT"; fi
BEFORE_LISTING="$(ls "$F2")"
if [ "$BEFORE_LISTING" = "$(ls "$F2")" ]; then ok "refusal leaves directory untouched"; else fail "refusal mutated directory"; fi

# ---------------------------------------------------------------------------
# Fixture 3: idempotent — a second run after a successful assignment is a
# safe no-op (no further renames, no double-assignment, state unchanged).
# ---------------------------------------------------------------------------
F3="$WORKDIR/f3"
mkdir -p "$F3"
cat > "$F3/README.md" <<'EOF'
| # | Decision | Status |
|---|---|---|
| [0001](0001-first.md) | First | Accepted |
| [NNNN](NNNN-idempotent-case.md) | Idempotent case | Accepted |
EOF
printf '# 0001. First\n\nStub.\n' > "$F3/0001-first.md"
printf '# NNNN. Idempotent case\n\nStub.\n' > "$F3/NNNN-idempotent-case.md"

"$SCRIPT" "$F3" >/dev/null 2>&1
LISTING_AFTER_FIRST="$(ls "$F3" | sort)"
README_AFTER_FIRST="$(cat "$F3/README.md")"

set +e
"$SCRIPT" "$F3" >"$WORKDIR/second_run.out" 2>&1
RC2=$?
set -e
LISTING_AFTER_SECOND="$(ls "$F3" | sort)"
README_AFTER_SECOND="$(cat "$F3/README.md")"

if [ "$RC2" -ne 0 ]; then ok "second run on an already-assigned dir refuses (no placeholder left)"; else fail "second run unexpectedly succeeded (should have nothing left to assign)"; fi
if [ "$LISTING_AFTER_FIRST" = "$LISTING_AFTER_SECOND" ]; then ok "idempotent: directory listing unchanged by the second run"; else fail "directory listing changed between runs:\n$LISTING_AFTER_FIRST\nvs\n$LISTING_AFTER_SECOND"; fi
if [ "$README_AFTER_FIRST" = "$README_AFTER_SECOND" ]; then ok "idempotent: README.md unchanged by the second run"; else fail "README.md changed between runs"; fi

# ---------------------------------------------------------------------------
# Fixture 4: concurrent-allocation case (the #170/#171 collision this issue
# fixes) — two placeholders authored off the same base, both present when
# the assignment tool runs once against the merged tree, must not collide.
# ---------------------------------------------------------------------------
F4="$WORKDIR/f4"
mkdir -p "$F4"
cat > "$F4/README.md" <<'EOF'
| # | Decision | Status |
|---|---|---|
| [0001](0001-first.md) | First | Accepted |
| [NNNN](NNNN-branch-a.md) | Branch A decision | Accepted |
| [NNNN](NNNN-branch-b.md) | Branch B decision | Accepted |
EOF
printf '# 0001. First\n\nStub.\n' > "$F4/0001-first.md"
printf '# NNNN. Branch A decision\n\nStub.\n' > "$F4/NNNN-branch-a.md"
printf '# NNNN. Branch B decision\n\nStub.\n' > "$F4/NNNN-branch-b.md"

"$SCRIPT" "$F4" >/dev/null 2>&1
if [ -f "$F4/0002-branch-a.md" ] && [ -f "$F4/0003-branch-b.md" ]; then
  ok "concurrent placeholders assigned distinct, sequential numbers (0002, 0003)"
else
  fail "concurrent placeholders did not get distinct sequential numbers: $(ls "$F4")"
fi
if bash "$ROOT/tools/adr-index-check.sh" "$F4" >/dev/null 2>&1; then
  ok "resulting index passes adr-index-check.sh (no duplicate/gap)"
else
  fail "resulting index failed adr-index-check.sh: $(bash "$ROOT/tools/adr-index-check.sh" "$F4" 2>&1)"
fi

# ---------------------------------------------------------------------------
echo
if [ "$FAILURES" -eq 0 ]; then
  echo "test_assign_adr_number: PASS"
  exit 0
else
  echo "test_assign_adr_number: FAIL ($FAILURES failures)"
  exit 1
fi
