#!/usr/bin/env bash
# Codex roles for NoiRPG.
#
# Codex is used where a *different model family* buys something Claude cannot buy
# from itself: independent verification. A second Claude agent re-deriving a table
# tends to repeat the same reasoning; a different vendor is far less likely to.
# Reserve it for the highest-risk surfaces — rules conformance and core-rules review.
#
# Sandbox defaults to read-only. Verification roles must not write.
#
# Usage:
#   tools/codex-agent.sh conformance "Verify the Skill Results Table implementation"
#   tools/codex-agent.sh review
#   tools/codex-agent.sh simcheck "Re-derive the advancement math independently"
#   DRY_RUN=1 tools/codex-agent.sh conformance "..."   # print the command, run nothing
#
# Env overrides: CODEX_BIN, CODEX_MODEL, OUT

set -euo pipefail

CODEX_BIN="${CODEX_BIN:-/usr/lib/chatgpt/resources/codex}"
REPO_ROOT="$(git rev-parse --show-toplevel)"
ROLE="${1:-}"
shift || true
PROMPT="${*:-}"

if [[ ! -x "$CODEX_BIN" ]]; then
  echo "codex not found at $CODEX_BIN (set CODEX_BIN)" >&2
  exit 127
fi

# Shared context every role gets. Keep this short — it is paid on every invocation.
read -r -d '' CONTEXT <<'CTX' || true
Repository context, read before answering:
- AGENTS.md is the operating contract. Read it first.
- The ONLY valid rules source is BasicRoleplaying-ORC-Content-Document.pdf.
- BRP SRD 1.0.2.pdf is a DIFFERENT, SUPERSEDED document with different success
  grades and different threshold rounding. Never use it. It is gitignored.
- orc-scope-filter.md defines what is in and out of scope. ~60% of the book is out.
- Where the book prints a table, verify every row. Never sample.
- Do not trust formulas in engine-implementation-plan.md; they are derivations.
  The printed table in the book is the only authority.
CTX

case "$ROLE" in
  conformance)
    EFFORT=high
    SANDBOX=read-only
    TASK="You are an independent rules-conformance verifier. Assume the implementation
is WRONG until proven otherwise. For every table-backed rule, check every printed row
and report how many you checked. For every derived formula, try to FALSIFY it: find an
input where the formula and the printed table disagree. Check rounding at boundaries,
grade precedence where ranges overlap, caps, floors, and behavior above 100%.
Report CONFIRMED or a specific defect with the input that breaks it.

Task: ${PROMPT}"
    ;;
  review)
    EFFORT=high
    SANDBOX=read-only
    TASK="Review the current branch diff with fresh context. Focus on core rules
correctness, determinism (no unseeded randomness, no ambient time), and scope
violations. Ignore style. Report only defects you can demonstrate with a concrete
failing input.

${PROMPT}"
    ;;
  simcheck)
    EFFORT=high
    SANDBOX=read-only
    TASK="Independently re-derive the math in tools/*.py. Do not read the existing
conclusions first — derive your own, then compare and report any disagreement.

Task: ${PROMPT}"
    ;;
  *)
    echo "usage: $0 {conformance|review|simcheck} [prompt]" >&2
    exit 2
    ;;
esac

OUT="${OUT:-$(mktemp -t codex-${ROLE}-XXXX.md)}"

CMD=("$CODEX_BIN" exec
  -C "$REPO_ROOT"
  -s "$SANDBOX"
  -c "model_reasoning_effort=\"${EFFORT}\""
  --output-last-message "$OUT"
  --skip-git-repo-check)

[[ -n "${CODEX_MODEL:-}" ]] && CMD+=(-m "$CODEX_MODEL")

if [[ "${DRY_RUN:-0}" == "1" ]]; then
  printf '%q ' "${CMD[@]}"; echo; echo "--- prompt ---"; echo "${CONTEXT}"; echo; echo "${TASK}"
  exit 0
fi

printf '%s\n\n%s\n' "$CONTEXT" "$TASK" | "${CMD[@]}" -
echo
echo "=== last message written to: $OUT ==="
