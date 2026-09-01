#!/usr/bin/env bash
# tests/tooling/test_orchestration_metrics.sh — fixture tests for
# tools/orchestration-metrics.py's read of the CURRENT `agent-verification`
# architecture (Issue #141), plus the jobs.csv schema migration it depends on.
#
# Proves:
#   1. docs/agent-team-ledger/jobs.csv is internally consistent post-migration —
#      every data row has exactly the same field count as the header (CSV-aware,
#      not a naive comma count, since some fields hold quoted commas).
#   2. tools/ledger-log.sh accepts --packet-type / --prompt-hash /
#      --discovery-calls and round-trips them into a fresh jobs.csv copy.
#   3. tools/orchestration-metrics.py parses the real (migrated) ledger CSVs
#      without error, in both --json and markdown modes.
#   4. The `agent-verification` commit-status + evidence-block reader
#      (parse_evidence_block / agent_verification_metrics) is exercised against
#      a stubbed `gh`, independent of network access.
#
# No network `gh` access is assumed — `gh` is stubbed, same pattern as
# tests/tooling/test_agent_verify.sh.
#
# Run directly:
#   tests/tooling/test_orchestration_metrics.sh
#
# Exit: 0 if every case passes, 1 on the first failure.
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
SCRIPT="$ROOT/tools/orchestration-metrics.py"
LEDGER_LOG="$ROOT/tools/ledger-log.sh"
JOBS_CSV="$ROOT/docs/agent-team-ledger/jobs.csv"

WORKDIR="$(mktemp -d)"
trap 'rm -rf "$WORKDIR"' EXIT

FAILURES=0
ok()   { printf 'ok   - %s\n' "$1"; }
fail() { printf 'FAIL - %s\n' "$1"; FAILURES=$((FAILURES + 1)); }

assert_eq() {
  local desc="$1" expected="$2" actual="$3"
  if [ "$expected" = "$actual" ]; then ok "$desc"; else
    fail "$desc (expected [$expected], got [$actual])"
  fi
}

# --- case 1: CSV field-count consistency (header vs every data row) -------- #
csv_check="$(python3 - "$JOBS_CSV" <<'PYEOF'
import csv, sys
path = sys.argv[1]
with open(path, newline="") as f:
    rows = list(csv.reader(f))
header_n = len(rows[0])
bad = [i for i, r in enumerate(rows[1:], start=2) if len(r) != header_n]
print("OK" if not bad else "BAD:" + ",".join(str(i) for i in bad))
PYEOF
)"
assert_eq "case1: every jobs.csv data row matches the header field count" "OK" "$csv_check"

ni_check="$(python3 - "$JOBS_CSV" <<'PYEOF'
import csv, re, sys
path = sys.argv[1]
with open(path, newline="") as f:
    rows = list(csv.DictReader(f))
HEX64 = re.compile(r"\A[0-9a-f]{64}\Z")
def ok(r):
    # Each new column is either NI (unmeasured) or a well-formed value — never
    # fabricated garbage. This catches migration/schema corruption while still
    # allowing real job telemetry to populate the columns (their whole purpose).
    pt, ph, dc = r.get("packet_type"), r.get("prompt_hash"), r.get("discovery_calls")
    if pt not in ("NI", "task", "review"):
        return False
    if ph != "NI" and not HEX64.match(ph or ""):
        return False
    if dc != "NI" and not (dc or "").isdigit():
        return False
    return True
bad = [r.get("issue") for r in rows if not ok(r)]
print("OK" if not bad else "MALFORMED:" + ",".join(str(b) for b in bad))
PYEOF
)"
assert_eq "case1b: every jobs.csv row has NI or a well-formed value in the 3 new columns (no fabrication)" "OK" "$ni_check"

# --- case 2: ledger-log.sh accepts and round-trips the 3 new job fields ---- #
WORK_LEDGER="$WORKDIR/ledger"
mkdir -p "$WORK_LEDGER"
head -1 "$JOBS_CSV" > "$WORK_LEDGER/jobs.csv"

# Run ledger-log.sh against a throwaway ledger by pointing ROOT at a scratch
# copy of the repo layout it expects (tools/ledger-log.sh resolves
# $ROOT/docs/agent-team-ledger relative to its own script location, so copy the
# script itself into a scratch tree with only jobs.csv rather than reimplement
# its path logic).
mkdir -p "$WORKDIR/tools" "$WORKDIR/docs/agent-team-ledger"
cp "$LEDGER_LOG" "$WORKDIR/tools/ledger-log.sh"
cp "$WORK_LEDGER/jobs.csv" "$WORKDIR/docs/agent-team-ledger/jobs.csv"

out="$("$WORKDIR/tools/ledger-log.sh" job --layer orch --issue 141 --pr 999 --seq 1 \
  --phase build --agent-role orchestration-dev --model sonnet --effort medium \
  --tokens-total 12345 --tool-uses 10 --tests-after 1728 \
  --packet-type task-packet/1 --prompt-hash deadbeef1234 --discovery-calls 2 \
  --outcome "test row" 2>&1)"
assert_eq "case2: ledger-log.sh job accepts the 3 new fields" \
  "logged job: issue=141 pr=999 seq=1 role=orchestration-dev" "$out"

row_check="$(python3 - "$WORKDIR/docs/agent-team-ledger/jobs.csv" <<'PYEOF'
import csv, sys
with open(sys.argv[1], newline="") as f:
    rows = list(csv.DictReader(f))
r = rows[-1]
print(r["packet_type"], r["prompt_hash"], r["discovery_calls"], r["outcome"])
PYEOF
)"
assert_eq "case2b: round-tripped values land in the right columns" \
  "task-packet/1 deadbeef1234 2 test row" "$row_check"

# Unmeasured job still defaults the 3 new fields to NI (never fabricated).
"$WORKDIR/tools/ledger-log.sh" job --layer orch --issue 141 --pr 999 --seq 2 \
  --phase verify --agent-role scope-warden --model haiku --effort low \
  --outcome "no discovery data" >/dev/null
default_check="$(python3 - "$WORKDIR/docs/agent-team-ledger/jobs.csv" <<'PYEOF'
import csv, sys
with open(sys.argv[1], newline="") as f:
    rows = list(csv.DictReader(f))
r = rows[-1]
print(r["packet_type"], r["prompt_hash"], r["discovery_calls"])
PYEOF
)"
assert_eq "case2c: omitted new fields default to NI, never 0/blank" "NI NI NI" "$default_check"

# --- case 3: orchestration-metrics.py parses the real migrated ledger ------ #
mkdir -p "$WORKDIR/emptybin"
cat > "$WORKDIR/emptybin/gh" <<'EOF'
#!/bin/sh
exit 127
EOF
chmod +x "$WORKDIR/emptybin/gh"

json_out="$(PATH="$WORKDIR/emptybin:$PATH" python3 "$SCRIPT" --limit 5 --json 2>&1)"
if printf '%s' "$json_out" | python3 -m json.tool >/dev/null 2>&1; then
  ok "case3: --json parses the migrated jobs.csv without crashing (gh unavailable)"
else
  fail "case3: --json parses the migrated jobs.csv without crashing (gh unavailable)"
fi

md_out="$(PATH="$WORKDIR/emptybin:$PATH" python3 "$SCRIPT" --limit 5 2>&1)"
if printf '%s' "$md_out" | grep -q "Job telemetry"; then
  ok "case3b: markdown report renders the Job telemetry section"
else
  fail "case3b: markdown report renders the Job telemetry section"
fi
if printf '%s' "$md_out" | grep -q "HISTORICAL (pre-#90/#91 check-runs)"; then
  ok "case3c: markdown report labels the old check-run metric as historical-compat"
else
  fail "case3c: markdown report labels the old check-run metric as historical-compat"
fi

# --- case 4: agent-verification status + evidence-block reader, stubbed gh - #
MOCKBIN="$WORKDIR/bin"
mkdir -p "$MOCKBIN"
HEAD_SHA="cafebabe1234567890cafebabe1234567890cafe"
cat > "$MOCKBIN/gh" <<MOCKGH
#!/usr/bin/env bash
set -euo pipefail
case "\$1 \$2" in
  "repo view")
    echo '{"nameWithOwner":"brandonifco/NoiRPG"}' ;;
  "pr list")
    cat <<'JSON'
[{"number": 999, "title": "test (#141)", "body": "Closes #141\n\n<!-- agent-verification:start -->\n### agent-verification - \`success\` for \`cafebabe\`\n\n| gate | result |\n|---|---|\n| \`ci\` | pass |\n| \`scope-warden\` | pass |\n\n_2/2 gates passed [route: tooling]. Regenerated per head SHA by \`tools/agent-verify.sh\`._\n<!-- agent-verification:end -->", "createdAt": "2026-08-31T00:00:00Z", "mergedAt": "2026-08-31T01:00:00Z", "headRefName": "x", "labels": [], "reviews": []}]
JSON
    ;;
  "pr view")
    echo '{"headRefOid":"$HEAD_SHA"}' ;;
  *)
    case "\$1" in
      run)
        echo '[]' ;;
      api)
        path="\$2"
        case "\$path" in
          repos/*/commits/*/status)
            echo '{"statuses":[{"context":"agent-verification","state":"success"}]}' ;;
          *) echo "mock gh: unhandled api path: \$path" >&2; exit 1 ;;
        esac ;;
      auth) exit 0 ;;
      --version) echo "gh version 0.0.0" ;;
      *) echo "mock gh: unhandled args: \$*" >&2; exit 1 ;;
    esac ;;
esac
MOCKGH
chmod +x "$MOCKBIN/gh"

driver_out="$(PATH="$MOCKBIN:$PATH" python3 - "$SCRIPT" <<'PYEOF'
import importlib.util
import sys

spec = importlib.util.spec_from_file_location("orchestration_metrics", sys.argv[1])
m = importlib.util.module_from_spec(spec)
spec.loader.exec_module(m)

# parse_evidence_block, unit-level.
body = ("Closes #141\n\n<!-- agent-verification:start -->\n"
        "### agent-verification -- `success` for `cafebabe`\n\n"
        "| gate | result |\n|---|---|\n| `ci` | pass |\n| `scope-warden` | pass |\n\n"
        "_2/2 gates passed [route: tooling]. Regenerated per head SHA._\n"
        "<!-- agent-verification:end -->")
ev = m.parse_evidence_block(body)
assert ev is not None, "evidence block must parse"
assert ev["gates"] == {"ci": "pass", "scope-warden": "pass"}, ev["gates"]
assert ev["route"] == "tooling", ev["route"]

# agent_verification_metrics against the stubbed gh commit-status endpoint.
prs = [{"number": 999, "body": body, "headRefName": "x"}]
av = m.agent_verification_metrics(prs, "brandonifco/NoiRPG")
assert av["available"] is True
assert av["prs_with_status"] == 1, av
assert av["status_states"] == {"success": 1}, av
assert av["prs_with_evidence"] == 1, av
assert av["route_distribution"] == {"tooling": 1}, av
assert av["gate_pass"] == {"ci": 1, "scope-warden": 1}, av

rs = m.review_scope_metrics(av)
assert rs["available"] is True
assert rs["pct_needing_semantic_review"] == 100.0, rs

print("PYOK")
PYEOF
)"
assert_eq "case4: parse_evidence_block + agent_verification_metrics against stubbed gh" "PYOK" "$driver_out"

echo
if [ "$FAILURES" -eq 0 ]; then
  echo "test_orchestration_metrics.sh: all checks passed"
  exit 0
else
  echo "test_orchestration_metrics.sh: $FAILURES check(s) failed"
  exit 1
fi
