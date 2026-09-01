#!/usr/bin/env bash
# tools/gate-evidence.sh — write ONE per-gate verdict-evidence file that binds a
# semantic reviewer's verdict to {head SHA + review-packet hash + gate identity}.
#
# This is a small INPUT builder for the repo's one verification-evidence
# authority (tools/agent-verify.sh) — it is deliberately NOT a second authority
# and mints nothing. The orchestrator runs a reviewer against a bounded review
# packet (tools/agent-brief.py review <pr>) plus, for codex conformance, a source
# packet (tools/source-slice.py); it reads the verdict; then it calls this to
# record that verdict as a machine-readable file. `tools/agent-verify.sh
# --gate-evidence FILE` re-derives the current PR head and the current review
# packet hash and REFUSES to mint `agent-verification` unless the recorded
# head/hash still match — so a stale or accidentally reused verdict can never
# ride onto a new head SHA (Issue #205, the review's remaining accidental-error
# hole).
#
# What this records vs. what agent-verify.sh enforces at verify time:
#   headSha            the PR head now (from gh). agent-verify refuses unless it
#                      still equals the PR head at verify time.
#   reviewPacketSha256 the `packet-sha256` footer of the review packet the
#                      reviewer actually saw. agent-verify regenerates the packet
#                      (agent-brief.py is deterministic) and refuses on mismatch.
#   sourcePacketSha256 sha256 of the source packet file (codex conformance only).
#                      RECORDED for provenance/audit; not re-validated (the PDF
#                      is pinned, but the page range is not recoverable at verify
#                      time). null when no source packet applies.
#   gate/verdict/reviewer/model/runId  identity + provenance.
#
# Usage:
#   tools/gate-evidence.sh --pr N --gate NAME --verdict pass|fail|skip \
#       --review-packet FILE [--source-packet FILE] \
#       [--reviewer R] [--model M] [--run-id ID] [--out FILE]
#
# Writes the JSON object to --out (default: stdout).
set -euo pipefail

die() { echo "gate-evidence: $*" >&2; exit 2; }

PR=""; GATE=""; VERDICT=""; REVIEW_PACKET=""; SOURCE_PACKET=""
REVIEWER=""; MODEL=""; RUN_ID=""; OUT=""
while [ $# -gt 0 ]; do
  case "$1" in
    --pr)            PR="${2:-}"; shift 2 ;;
    --gate)          GATE="${2:-}"; shift 2 ;;
    --verdict)       VERDICT="${2:-}"; shift 2 ;;
    --review-packet) REVIEW_PACKET="${2:-}"; shift 2 ;;
    --source-packet) SOURCE_PACKET="${2:-}"; shift 2 ;;
    --reviewer)      REVIEWER="${2:-}"; shift 2 ;;
    --model)         MODEL="${2:-}"; shift 2 ;;
    --run-id)        RUN_ID="${2:-}"; shift 2 ;;
    --out)           OUT="${2:-}"; shift 2 ;;
    -*)              die "unknown option: $1" ;;
    *)               die "unexpected argument: $1" ;;
  esac
done

[ -n "$PR" ]  || die "--pr is required"
[ -n "$GATE" ] || die "--gate is required"
[[ "$PR" =~ ^[0-9]+$ ]] || die "--pr must be numeric: $PR"
case "$VERDICT" in pass|fail|skip) ;; *) die "--verdict must be pass|fail|skip: ${VERDICT:-<empty>}" ;; esac
[ -n "$REVIEW_PACKET" ] || die "--review-packet is required"
[ -f "$REVIEW_PACKET" ] || die "review packet not found: $REVIEW_PACKET"
[ -z "$SOURCE_PACKET" ] || [ -f "$SOURCE_PACKET" ] || die "source packet not found: $SOURCE_PACKET"

# Reviewer defaults to the gate name (scope-warden's reviewer is scope-warden).
[ -n "$REVIEWER" ] || REVIEWER="$GATE"

# The current PR head — the SHA this verdict is being bound to.
HEAD_SHA="$(gh pr view "$PR" --json headRefOid --jq '.headRefOid' 2>/dev/null || true)"
[ -n "$HEAD_SHA" ] || die "cannot read head SHA for PR #$PR (is gh authenticated?)"

# The review packet's own content hash, from its deterministic footer.
packet_footer_sha() {
  # `|| true` so a footer-less packet yields "" (caught below) rather than
  # aborting under `set -o pipefail` when grep finds no match.
  { grep -E '^packet-sha256:[[:space:]]*[0-9a-f]+' "$1" | tail -1 | awk '{print $2}'; } || true
}
REVIEW_PACKET_SHA="$(packet_footer_sha "$REVIEW_PACKET")"
[ -n "$REVIEW_PACKET_SHA" ] || die "review packet has no 'packet-sha256:' footer: $REVIEW_PACKET (was it built by tools/agent-brief.py review?)"

SOURCE_PACKET_SHA=""
if [ -n "$SOURCE_PACKET" ]; then
  SOURCE_PACKET_SHA="$(sha256sum "$SOURCE_PACKET" | cut -d' ' -f1)"
fi

EVIDENCE_JSON="$(python3 - "$PR" "$GATE" "$VERDICT" "$HEAD_SHA" "$REVIEW_PACKET_SHA" "$SOURCE_PACKET_SHA" "$REVIEWER" "$MODEL" "$RUN_ID" <<'PYEOF'
import json
import sys

pr, gate, verdict, head_sha, rps, sps, reviewer, model, run_id = sys.argv[1:10]
obj = {
    "schemaVersion": 1,
    "gate": gate,
    "verdict": verdict,
    "pr": int(pr),
    "headSha": head_sha,
    "reviewPacketSha256": rps,
    "sourcePacketSha256": sps or None,
    "reviewer": reviewer,
    "model": model or None,
    "runId": run_id or None,
}
print(json.dumps(obj, indent=2))
PYEOF
)"

if [ -n "$OUT" ]; then
  printf '%s\n' "$EVIDENCE_JSON" > "$OUT"
  echo "gate-evidence: wrote $GATE=$VERDICT for PR #$PR @ ${HEAD_SHA:0:7} -> $OUT" >&2
else
  printf '%s\n' "$EVIDENCE_JSON"
fi
