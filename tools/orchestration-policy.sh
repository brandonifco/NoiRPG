#!/usr/bin/env bash
# tools/orchestration-policy.sh — the mechanical guardrails from AGENTS.md as an
# instant CI gate. Fast (no build, no restore). It enforces the invariants that
# are cheaply and deterministically checkable, and it *guards the guardrails* the
# build already has, so nobody can silently remove them.
#
# Deliberately NOT duplicated here (already enforced elsewhere):
#   - randomness/ambient-clock bans   -> build -warnaserror + BannedApiAnalyzers
#     (this script instead checks that wiring is still present).
#   - determinism / table completeness -> the dotnet test suite (build-and-test).
#   - scope filter, rules-as-data judgment -> scope-warden / rules-conformance.
#     (AGENTS.md #7 is a judgment call; a naive magic-number grep would be noise.)
set -uo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT"

fail=0
ok()      { printf '  ok   %s\n' "$1"; }
bad()     { printf 'FAIL   %s\n' "$1"; fail=1; }
section() { printf '\n== %s ==\n' "$1"; }

# 1. Authoritative source pinned (AGENTS.md: the ORC document is the rules).
section "authoritative source hash"
PIN=.github/authoritative-source.sha256
if [ ! -f "$PIN" ]; then
  bad "missing $PIN"
elif sha256sum -c --status "$PIN" 2>/dev/null; then
  ok "ORC Content Document matches pinned SHA-256"
else
  bad "AUTHORITATIVE SOURCE CHANGED — the pinned ORC document hash does not match"
  echo "    expected: $(cut -d' ' -f1 "$PIN")"
  echo "    actual:   $(sha256sum BasicRoleplaying-ORC-Content-Document.pdf 2>/dev/null | cut -d' ' -f1 || echo '<file missing>')"
fi

# 2. Superseded source never committed (AGENTS.md #1: BRP SRD 1.0.2 is not our source).
section "forbidden source"
srd="$(git ls-files | grep -iE 'brp.?srd|srd.?1[._]?0[._]?2' || true)"
if [ -n "$srd" ]; then bad "superseded SRD is tracked in git: $srd"; else ok "no superseded SRD file tracked"; fi
if grep -qi 'SRD 1.0.2' .gitignore 2>/dev/null; then ok ".gitignore still excludes the SRD"; else bad ".gitignore no longer excludes 'BRP SRD 1.0.2.pdf'"; fi

# 3. Seeded-randomness / no-ambient-clock guardrail intact (AGENTS.md #5).
section "seeded-randomness guardrail (BannedApiAnalyzers)"
BS=src/Brp.Core/BannedSymbols.txt
if [ ! -f "$BS" ]; then
  bad "missing $BS"
else
  miss=""
  for sym in 'System.Random' 'System.DateTime.Now' 'System.DateTime.UtcNow' 'System.DateTimeOffset.Now' 'System.DateTimeOffset.UtcNow'; do
    grep -q "$sym" "$BS" || miss="$miss $sym"
  done
  if [ -z "$miss" ]; then ok "BannedSymbols.txt bans Random + ambient clock"; else bad "BannedSymbols.txt no longer bans:$miss"; fi
fi
for proj in src/Brp.Core/Brp.Core.csproj src/Brp.Rules/Brp.Rules.csproj; do
  if grep -q 'BannedApiAnalyzers' "$proj" && grep -q 'BannedSymbols.txt' "$proj"; then
    ok "$(basename "$proj") wires the analyzer + banned list"
  else
    bad "$(basename "$proj") no longer references BannedApiAnalyzers + BannedSymbols.txt"
  fi
done

# 4. No game-engine dependency in Brp.Core / Brp.Rules (AGENTS.md #6).
section "no game-engine dependency in Brp.Core / Brp.Rules"
engine='Godot|UnityEngine|Unity\.|MonoGame|Microsoft\.Xna'
refs="$(grep -rInE "Include=\"[^\"]*($engine)" src/Brp.Core src/Brp.Rules --include='*.csproj' 2>/dev/null || true)"
usings="$(grep -rInE "^using +(Godot|UnityEngine|MonoGame|Microsoft\.Xna)" src/Brp.Core src/Brp.Rules --include='*.cs' 2>/dev/null || true)"
if [ -z "$refs$usings" ]; then
  ok "no Unity/Godot/MonoGame references"
else
  bad "game-engine reference found in Core/Rules:"
  printf '%s\n' "$refs" "$usings" | sed '/^$/d;s/^/       /'
fi

section "result"
if [ "$fail" = 0 ]; then echo "orchestration-policy: PASS"; else echo "orchestration-policy: FAIL"; fi
exit "$fail"
