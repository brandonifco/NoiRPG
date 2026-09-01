#!/usr/bin/env bash
# Codex roles for NoiRPG — packet-first (Issue #142).
#
# Codex buys one thing Claude cannot buy from itself: reasoning from a different
# model family. It is reserved for the highest-risk surfaces — rules conformance
# and core-rules review — and is otherwise unused. See docs/agent-team.md.
#
# Codex never assembles its own context. The orchestrator hands it an exact,
# bounded packet built by tools/agent-brief.py and/or tools/source-slice.py, and
# Codex reasons over that packet alone: no whole-repo survey, no independent
# page-hunting when a source packet is supplied, and no exposure to any other
# verifier's notes or conclusions — `conformance` re-derives its own verdict from
# the source text and the diff, nothing else. Codex is verification-only: it does
# not implement.
#
# Sandbox is read-only for every role. This script never writes.
#
# Usage:
#   tools/codex-agent.sh conformance --review-packet FILE --source-packet FILE
#   tools/codex-agent.sh review      --review-packet FILE
#   tools/codex-agent.sh simcheck    --packet FILE
#   tools/codex-agent.sh --check
#   tools/codex-agent.sh preflight
#     # report whether Codex is available at CODEX_BIN (never `which codex`);
#     # exit 0 if present, non-zero with a clear message if absent
#   DRY_RUN=1 tools/codex-agent.sh conformance --review-packet R --source-packet S
#     # print the composed command + prompt, invoke nothing
#
# Packets (build these first, then hand the file to this script):
#   --review-packet   tools/agent-brief.py review <pr>     (diff, route, gate checklist)
#   --source-packet   tools/source-slice.py --pages A-B    (authoritative source text)
#   --packet          any single bounded file, for simcheck (e.g. a source-slice packet)
#
# Env overrides: CODEX_BIN, CODEX_MODEL, OUT.
# LEDGER_LOG=1 additionally appends one job row via tools/ledger-log.sh (never
# fabricated — set ISSUE/PR/SEQ/LAYER/PHASE to whatever is actually known; unset
# fields land as NI). Off by default.

set -euo pipefail

CODEX_BIN="${CODEX_BIN:-/usr/lib/chatgpt/resources/codex}"
REPO_ROOT="$(git rev-parse --show-toplevel)"
ROLE="${1:-}"
shift || true

# --check / preflight: report Codex availability by testing the CODEX_BIN
# path directly. Do NOT use `which codex` or `command -v codex` — the binary
# is not installed under that name on PATH; only CODEX_BIN is authoritative.
case "$ROLE" in
  --check|preflight)
    if [[ -x "$CODEX_BIN" ]]; then
      echo "codex-agent: codex available at $CODEX_BIN"
      exit 0
    else
      echo "codex-agent: codex not found at $CODEX_BIN (set CODEX_BIN to override)" >&2
      exit 1
    fi
    ;;
esac

REVIEW_PACKET=""
SOURCE_PACKET=""
PACKET=""
while [ $# -gt 0 ]; do
  case "$1" in
    --review-packet) REVIEW_PACKET="${2:-}"; shift 2 ;;
    --source-packet) SOURCE_PACKET="${2:-}"; shift 2 ;;
    --packet)        PACKET="${2:-}"; shift 2 ;;
    *) echo "codex-agent: unknown argument: $1" >&2; exit 2 ;;
  esac
done

usage() {
  cat >&2 <<'USAGE'
usage:
  tools/codex-agent.sh conformance --review-packet FILE --source-packet FILE
  tools/codex-agent.sh review      --review-packet FILE
  tools/codex-agent.sh simcheck    --packet FILE
USAGE
  exit 2
}

read_packet() {
  # $1 = path, $2 = label (for error messages)
  local path="$1" label="$2"
  [ -n "$path" ] || { echo "codex-agent: --${label}-packet is required for this role" >&2; usage; }
  [ -f "$path" ] || { echo "codex-agent: ${label} packet not found: $path" >&2; exit 2; }
  cat "$path"
}

# Short, role-independent framing. Deliberately does NOT tell Codex to read
# AGENTS.md or go looking for source pages — the packet(s) below are the bounded
# context; anything Codex needs to verify against is already in them.
CONTEXT="You are a read-only verification agent. Work only from the packet(s) given
below. Do not attempt to write files, and do not propose or perform an
implementation — report findings only."

case "$ROLE" in
  conformance)
    EFFORT=high
    SANDBOX=read-only
    SOURCE_CONTENT="$(read_packet "$SOURCE_PACKET" source)"
    REVIEW_CONTENT="$(read_packet "$REVIEW_PACKET" review)"
    PACKET_TYPE="conformance"
    TASK="You are an independent rules-conformance verifier. Assume the
implementation is WRONG until proven otherwise. Re-derive your verdict yourself
from the SOURCE packet and the diff in the REVIEW packet below — you have not
been given, and must not seek out, any other verifier's notes or conclusions.
For every table-backed rule, check every printed row and report how many you
checked. For every derived formula, try to FALSIFY it: find an input where the
formula and the printed table disagree. Check rounding at boundaries, grade
precedence where ranges overlap, caps, floors, and behavior above 100%. Report
CONFIRMED or a specific defect with the input that breaks it.

--- SOURCE packet (tools/source-slice.py) ---
${SOURCE_CONTENT}

--- REVIEW packet (tools/agent-brief.py review) ---
${REVIEW_CONTENT}"
    HASH_INPUT="${SOURCE_CONTENT}
${REVIEW_CONTENT}"
    ;;
  review)
    EFFORT=high
    SANDBOX=read-only
    REVIEW_CONTENT="$(read_packet "$REVIEW_PACKET" review)"
    PACKET_TYPE="review"
    TASK="Review the packet below with fresh context. It already carries the
diff, route, and required-gate checklist — do not go looking for more. Focus on
core rules correctness, determinism (no unseeded randomness, no ambient time),
and scope violations. Ignore style. Report only defects you can demonstrate
with a concrete failing input.

--- REVIEW packet (tools/agent-brief.py review) ---
${REVIEW_CONTENT}"
    HASH_INPUT="${REVIEW_CONTENT}"
    ;;
  simcheck)
    EFFORT=high
    SANDBOX=read-only
    if [ -z "$PACKET" ]; then
      echo "codex-agent: --packet is required for this role" >&2
      usage
    fi
    [ -f "$PACKET" ] || { echo "codex-agent: packet not found: $PACKET" >&2; exit 2; }
    PACKET_CONTENT="$(cat "$PACKET")"
    PACKET_TYPE="simcheck"
    TASK="Independently re-derive the math in the packet below. Do not read any
existing conclusions first — derive your own, then compare and report any
disagreement.

--- packet ---
${PACKET_CONTENT}"
    HASH_INPUT="${PACKET_CONTENT}"
    ;;
  *)
    usage
    ;;
esac

PROMPT_HASH="$(printf '%s' "$HASH_INPUT" | sha256sum | cut -d' ' -f1)"

OUT="${OUT:-$(mktemp -t codex-${ROLE}-XXXX.md)}"

CMD=("$CODEX_BIN" exec
  -C "$REPO_ROOT"
  -s "$SANDBOX"
  -c "model_reasoning_effort=\"${EFFORT}\""
  --output-last-message "$OUT"
  --skip-git-repo-check)

[[ -n "${CODEX_MODEL:-}" ]] && CMD+=(-m "$CODEX_MODEL")

if [[ "${DRY_RUN:-0}" == "1" ]]; then
  printf '%q ' "${CMD[@]}"; echo
  echo "--- packet-type: ${PACKET_TYPE} ---"
  echo "--- prompt-sha256: ${PROMPT_HASH} ---"
  echo "--- prompt ---"
  echo "${CONTEXT}"
  echo
  echo "${TASK}"
  exit 0
fi

if [[ ! -x "$CODEX_BIN" ]]; then
  echo "codex not found at $CODEX_BIN (set CODEX_BIN)" >&2
  exit 127
fi

echo "codex-agent: role=${ROLE} packet-type=${PACKET_TYPE} prompt-sha256=${PROMPT_HASH}"

if [[ "${LEDGER_LOG:-0}" == "1" ]]; then
  LARGS=(job --packet-type "$PACKET_TYPE" --prompt-hash "$PROMPT_HASH"
    --agent-role "codex-${ROLE}" --model "${CODEX_MODEL:-codex}" --effort "$EFFORT")
  [ -n "${ISSUE:-}" ] && LARGS+=(--issue "$ISSUE")
  [ -n "${PR:-}" ]    && LARGS+=(--pr "$PR")
  [ -n "${SEQ:-}" ]   && LARGS+=(--seq "$SEQ")
  [ -n "${LAYER:-}" ] && LARGS+=(--layer "$LAYER")
  [ -n "${PHASE:-}" ] && LARGS+=(--phase "$PHASE")
  "$REPO_ROOT/tools/ledger-log.sh" "${LARGS[@]}" || echo "codex-agent: ledger-log failed (non-fatal)" >&2
fi

printf '%s\n\n%s\n' "$CONTEXT" "$TASK" | "${CMD[@]}" -
echo
echo "=== last message written to: $OUT ==="
