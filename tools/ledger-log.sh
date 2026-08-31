#!/usr/bin/env bash
# tools/ledger-log.sh — append one row to an agent-team ledger CSV, so logging a
# job or a human-attention datum is a single command instead of hand-editing a
# 28-column CSV. This is the frictionless capture the metrics tool
# (tools/orchestration-metrics.py) has been waiting on: the ledger only measures
# what actually gets written, and a manual CSV edit per agent run does not happen.
#
#   tools/ledger-log.sh job   --layer 4 --issue 112 --pr 130 --seq 1 \
#                             --phase build --agent-role engine-dev-implement \
#                             --model sonnet --effort medium --tokens-total 210000 \
#                             --tests-after 2260 --outcome "Hit-location table + resolver"
#
#   tools/ledger-log.sh human --issue 112 --pr 130 --merge-sha abc1234 \
#                             --human-minutes 6 --interventions 0 \
#                             --note "one review pass, merged clean"
#
# Design rules (matching docs/orchestration/metrics.md and the ledger README):
#   * Unmeasured numeric fields default to `NI` ("not instrumented"), never 0 —
#     the metrics tool skips NI rather than treating it as a real value.
#   * `human` refuses an all-NI row: at least one of --human-minutes /
#     --interventions must be a real measurement. Do NOT commit fabricated rows.
#   * Values are CSV-escaped; the row is appended atomically after a header check.
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
LEDGER="$ROOT/docs/agent-team-ledger"
JOBS_CSV="$LEDGER/jobs.csv"
HUMAN_CSV="$LEDGER/human-minutes.csv"

die() { echo "ledger-log: $*" >&2; exit 2; }

# The column order is the CONTRACT with jobs.csv / metrics.py — do not reorder.
JOB_COLS=(layer issue pr seq date phase agent_role model effort risk_class
  review_layer tokens_total tokens_R tokens_A tokens_H tool_uses duration_ms
  human_minutes cost_usd commit merge_sha defects_found false_positives
  defects_fixed deterministic_controls_added repeated_error tests_after outcome)
HUMAN_COLS=(issue pr merge_sha human_minutes interventions note)

# --kebab-flag -> snake_case column name.
flag_to_col() { printf '%s' "${1#--}" | tr '-' '_'; }

# CSV-escape one field: quote it when it holds a comma, quote, CR, or LF.
csv_escape() {
  local v="$1"
  if [[ "$v" == *[,\"$'\n'$'\r']* ]]; then
    v="${v//\"/\"\"}"
    printf '"%s"' "$v"
  else
    printf '%s' "$v"
  fi
}

# Append CSV row "$@" to file $1, ensuring the file ends in a newline first.
append_row() {
  local file="$1"; shift
  local line="" first=1 f
  for f in "$@"; do
    [ "$first" = 1 ] && first=0 || line+=","
    line+="$(csv_escape "$f")"
  done
  # Guarantee a trailing newline on the existing file so we never join two rows.
  [ -s "$file" ] && [ -n "$(tail -c1 "$file")" ] && printf '\n' >> "$file"
  printf '%s\n' "$line" >> "$file"
}

# Verify the on-disk header matches our column contract, so a silent schema drift
# fails loudly instead of writing misaligned rows.
check_header() {
  local file="$1"; shift
  local expected; expected="$(IFS=,; echo "$*")"
  local actual; actual="$(head -1 "$file")"
  [ "$actual" = "$expected" ] || die "header mismatch in $(basename "$file")
  expected: $expected
  actual:   $actual"
}

subcmd="${1:-}"; shift || true
[ -n "$subcmd" ] || die "usage: ledger-log.sh {job|human} --field value ..."

declare -A VAL=()
while [ $# -gt 0 ]; do
  case "$1" in
    --*) [ $# -ge 2 ] || die "flag $1 needs a value"
         VAL["$(flag_to_col "$1")"]="$2"; shift 2 ;;
    *)   die "unexpected argument: $1 (use --field value)" ;;
  esac
done

case "$subcmd" in
  job)
    check_header "$JOBS_CSV" "${JOB_COLS[@]}"
    # date defaults to today; a logging timestamp, not engine state.
    [ -n "${VAL[date]:-}" ] || VAL[date]="$(date +%F)"
    row=()
    for c in "${JOB_COLS[@]}"; do
      if [ "$c" = outcome ]; then
        row+=("${VAL[$c]:-}")            # free-text summary; empty is fine
      else
        row+=("${VAL[$c]:-NI}")          # unmeasured -> NI, never 0
      fi
    done
    # Reject unknown flags — a typo'd column name is a silent data-loss bug.
    for k in "${!VAL[@]}"; do
      printf '%s\n' "${JOB_COLS[@]}" | grep -qx "$k" || die "unknown job field: --${k//_/-}"
    done
    append_row "$JOBS_CSV" "${row[@]}"
    echo "logged job: issue=${VAL[issue]:-NI} pr=${VAL[pr]:-NI} seq=${VAL[seq]:-NI} role=${VAL[agent_role]:-NI}"
    ;;

  human)
    # Never fabricate: require at least one real measurement.
    hm="${VAL[human_minutes]:-}"; iv="${VAL[interventions]:-}"
    if { [ -z "$hm" ] || [ "$hm" = NI ]; } && { [ -z "$iv" ] || [ "$iv" = NI ]; }; then
      die "refusing an all-NI row — measure --human-minutes and/or --interventions first"
    fi
    for k in "${!VAL[@]}"; do
      printf '%s\n' "${HUMAN_COLS[@]}" | grep -qx "$k" || die "unknown human field: --${k//_/-}"
    done
    if [ ! -f "$HUMAN_CSV" ]; then
      (IFS=,; echo "${HUMAN_COLS[*]}") > "$HUMAN_CSV"
    fi
    check_header "$HUMAN_CSV" "${HUMAN_COLS[@]}"
    row=()
    for c in "${HUMAN_COLS[@]}"; do
      if [ "$c" = note ]; then row+=("${VAL[$c]:-}"); else row+=("${VAL[$c]:-NI}"); fi
    done
    append_row "$HUMAN_CSV" "${row[@]}"
    echo "logged human: issue=${VAL[issue]:-NI} pr=${VAL[pr]:-NI} minutes=${hm:-NI} interventions=${iv:-NI}"
    ;;

  *) die "unknown subcommand: $subcmd (job|human)" ;;
esac
