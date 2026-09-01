#!/usr/bin/env bash
# tests/tooling/test_gate_evidence.sh — fixture tests for tools/gate-evidence.sh,
# the per-gate verdict builder that binds a reviewer's verdict to
# {head SHA + review-packet hash + gate identity} (Issue #205).
#
# `gh` is stubbed on PATH to answer `gh pr view N --json headRefOid --jq
# .headRefOid` with a fixed SHA. Packets are plain fixture files; the review
# packet carries a `packet-sha256:` footer exactly as tools/agent-brief.py emits.
#
# Run directly:
#   tests/tooling/test_gate_evidence.sh
# Exit: 0 if every case passes, 1 on the first failure.
set -uo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
SCRIPT="$ROOT/tools/gate-evidence.sh"

FAILURES=0
ok()   { printf 'ok   - %s\n' "$1"; }
fail() { printf 'FAIL - %s\n' "$1"; FAILURES=$((FAILURES + 1)); }
assert_eq() { local d="$1" e="$2" a="$3"; [ "$e" = "$a" ] && ok "$d" || fail "$d (expected [$e], got [$a])"; }
assert_contains() { local d="$1" h="$2" n="$3"; case "$h" in *"$n"*) ok "$d" ;; *) fail "$d (missing [$n] in [$h])" ;; esac }

WORKDIR="$(mktemp -d)"; BINDIR="$WORKDIR/bin"; mkdir -p "$BINDIR"
trap 'rm -rf "$WORKDIR"' EXIT

HEAD="1234567890abcdef1234567890abcdef12345678"
cat > "$BINDIR/gh" <<STUB
#!/usr/bin/env bash
# gh pr view N --json headRefOid --jq .headRefOid
if [ "\${1:-}" = "pr" ] && [ "\${2:-}" = "view" ]; then printf '%s' "${HEAD}"; exit 0; fi
exit 1
STUB
chmod +x "$BINDIR/gh"

# Build a packet with a TRUTHFUL footer (body hash matches), mirroring
# tools/agent-brief.py render(): body = "\n".join(lines).rstrip() + "\n", and the
# footer's packet-sha256 is sha256(body). Prints the digest.
make_packet() { # $1=path  $2=body-text
  python3 - "$1" "$2" <<'PY'
import hashlib, sys
path, raw = sys.argv[1], sys.argv[2]
body = raw.rstrip() + "\n"
digest = hashlib.sha256(body.encode("utf-8")).hexdigest()
footer = "\n---\npacket-schema: review-packet/1\npacket-version: 1\npacket-sha256: %s\n" % digest
open(path, "w", encoding="utf-8").write(body + footer)
print(digest)
PY
}

REVIEW="$WORKDIR/review.md"
RPS="$(make_packet "$REVIEW" "# REVIEW BRIEF — fixture")"
SOURCE="$WORKDIR/source.txt"; printf 'BRP source packet fixture\n' > "$SOURCE"
SRC_SHA="$(sha256sum "$SOURCE" | cut -d' ' -f1)"

run() { PATH="$BINDIR:$PATH" "$SCRIPT" "$@"; }

# === 1. happy path (no source packet) ======================================= #
j="$(run --pr 42 --gate scope-warden --verdict pass --review-packet "$REVIEW" --model haiku)"
printf '%s' "$j" | python3 -m json.tool >/dev/null && ok "case1: valid JSON" || fail "case1: valid JSON"
read -r g v pr hs rps sps rev mod <<< "$(printf '%s' "$j" | python3 -c '
import json,sys
d=json.load(sys.stdin)
print(d["gate"],d["verdict"],d["pr"],d["headSha"],d["reviewPacketSha256"],d["sourcePacketSha256"],d["reviewer"],d["model"])')"
assert_eq "case1: gate"    "scope-warden" "$g"
assert_eq "case1: verdict" "pass"         "$v"
assert_eq "case1: pr"      "42"           "$pr"
assert_eq "case1: headSha" "$HEAD"        "$hs"
assert_eq "case1: reviewPacketSha256 from footer" "$RPS" "$rps"
assert_eq "case1: sourcePacketSha256 null when none" "None" "$sps"
assert_eq "case1: reviewer defaults to gate" "scope-warden" "$rev"
assert_eq "case1: model recorded" "haiku" "$mod"

# === 2. source packet hashed for provenance ================================= #
j2="$(run --pr 42 --gate codex-conformance --verdict pass --review-packet "$REVIEW" --source-packet "$SOURCE")"
sps2="$(printf '%s' "$j2" | python3 -c 'import json,sys; print(json.load(sys.stdin)["sourcePacketSha256"])')"
assert_eq "case2: sourcePacketSha256 == sha256(file)" "$SRC_SHA" "$sps2"

# === 3. --out writes a file ================================================= #
run --pr 42 --gate scope-warden --verdict pass --review-packet "$REVIEW" --out "$WORKDIR/ev.json" 2>/dev/null
[ -s "$WORKDIR/ev.json" ] && python3 -m json.tool < "$WORKDIR/ev.json" >/dev/null \
  && ok "case3: --out wrote valid JSON" || fail "case3: --out wrote valid JSON"

# === 4. bad verdict rejected ================================================ #
rc=0; run --pr 42 --gate scope-warden --verdict maybe --review-packet "$REVIEW" >/dev/null 2>&1 || rc=$?
assert_eq "case4: bad verdict rejected (exit 2)" "2" "$rc"

# === 5. review packet without a footer rejected ============================= #
NOFOOT="$WORKDIR/nofoot.md"; printf 'no footer here\n' > "$NOFOOT"
rc=0; err="$(run --pr 42 --gate scope-warden --verdict pass --review-packet "$NOFOOT" 2>&1 1>/dev/null)" || rc=$?
assert_eq "case5: missing footer rejected (exit 2)" "2" "$rc"
assert_contains "case5: names the footer" "$err" "packet-sha256"

# === 6. missing review packet file rejected ================================= #
rc=0; run --pr 42 --gate scope-warden --verdict pass --review-packet "$WORKDIR/nope.md" >/dev/null 2>&1 || rc=$?
assert_eq "case6: missing review packet rejected (exit 2)" "2" "$rc"

# === 7. non-numeric pr rejected ============================================= #
rc=0; run --pr abc --gate scope-warden --verdict pass --review-packet "$REVIEW" >/dev/null 2>&1 || rc=$?
assert_eq "case7: non-numeric --pr rejected (exit 2)" "2" "$rc"

# === 8. tampered packet (body edited, footer kept) is rejected ============== #
# Build a truthful packet, then corrupt the body without touching the footer:
# the recomputed body hash must no longer match the declared footer hash.
TAMPER="$WORKDIR/tamper.md"; make_packet "$TAMPER" "# REVIEW BRIEF — original body" >/dev/null
python3 - "$TAMPER" <<'PY'
p = __import__("sys").argv[1]
t = open(p, encoding="utf-8").read()
idx = t.rfind("\n---\npacket-schema:")
# Insert text INTO the body (before the footer), leaving the footer hash stale.
open(p, "w", encoding="utf-8").write(t[:idx] + "TAMPERED\n" + t[idx:])
PY
rc=0; err="$(run --pr 42 --gate scope-warden --verdict pass --review-packet "$TAMPER" 2>&1 1>/dev/null)" || rc=$?
assert_eq "case8: tampered packet rejected (exit 2)" "2" "$rc"
assert_contains "case8: names the body/footer mismatch" "$err" "does not match its footer hash"

# === 9. a truthful packet still passes (regression) ========================= #
GOOD="$WORKDIR/good.md"; g="$(make_packet "$GOOD" "# REVIEW BRIEF — clean")"
sha9="$(run --pr 42 --gate scope-warden --verdict pass --review-packet "$GOOD" | python3 -c 'import json,sys; print(json.load(sys.stdin)["reviewPacketSha256"])')"
assert_eq "case9: truthful packet accepted, records its real hash" "$g" "$sha9"

echo
if [ "$FAILURES" -eq 0 ]; then echo "test_gate_evidence.sh: all checks passed"; exit 0
else echo "test_gate_evidence.sh: $FAILURES check(s) failed"; exit 1; fi
