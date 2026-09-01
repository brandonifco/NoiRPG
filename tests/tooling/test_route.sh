#!/usr/bin/env bash
# tests/tooling/test_route.sh — fixture tests for tools/route.sh, the one route
# authority (Issue #137).
#
# Proves route.sh classifies a change identically whether it derives the diff
# itself (--base / working tree) or is handed an externally captured patch
# (--diff-file) — the shape agent-verify.sh, pr_policy.py, and agent-brief.py
# now all call through rather than approximating classification themselves.
#
# No network access is available in this environment (or in CI without a PAT),
# so `gh` is stubbed for the two cases that need an issue's route:* label.
#
# Run directly:
#   tests/tooling/test_route.sh
#
# Exit: 0 if every case passes, 1 on the first failure.
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
SCRIPT="$ROOT/tools/route.sh"

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

route_of()     { printf '%s' "$1" | python3 -c 'import json,sys; print(json.load(sys.stdin)["route"])'; }
escalated_of() { printf '%s' "$1" | python3 -c 'import json,sys; print(json.load(sys.stdin)["escalated"])'; }
arch_of()      { printf '%s' "$1" | python3 -c 'import json,sys; print(json.load(sys.stdin)["architecture"])'; }
gates_of()     { printf '%s' "$1" | python3 -c 'import json,sys; print(sorted(json.load(sys.stdin)["gates"]))'; }

# --- case 1: ordinary docs file -> docs ------------------------------------ #
j1="$("$SCRIPT" --json -- README.md)"
assert_eq "case1: docs file -> docs" "docs" "$(route_of "$j1")"
assert_eq "case1: docs gate set is ci-only (no semantic AI reviewer)" \
  "['ci']" "$(gates_of "$j1")"

# --- case 2: ordinary tooling file -> tooling ------------------------------ #
j2="$("$SCRIPT" --json -- tools/some-script.sh)"
assert_eq "case2: tooling file -> tooling" "tooling" "$(route_of "$j2")"
assert_eq "case2: tooling gate set is ci-only (no semantic AI reviewer)" \
  "['ci']" "$(gates_of "$j2")"

# --- case 3: ordinary BRP C# implementation -> rules ----------------------- #
j3="$("$SCRIPT" --json -- src/Brp.Rules/Combat/RangeBands.cs)"
assert_eq "case3: BRP C# impl -> rules" "rules" "$(route_of "$j3")"

# --- case 4: ruleset JSON -> formulas --------------------------------------- #
j4="$("$SCRIPT" --json -- src/Brp.Data/damage-ruleset.json)"
assert_eq "case4: ruleset JSON -> formulas" "formulas" "$(route_of "$j4")"

# --- Layer 5 route taxonomy (Issue #143) ------------------------------------- #
# gameplay — original Noir mechanics, design-led, NOT auto BRP conformance.
j10="$("$SCRIPT" --json -- src/Noir.Rules/Foo.cs)"
assert_eq "case10: original Noir mechanics -> gameplay" "gameplay" "$(route_of "$j10")"
assert_eq "case10: gameplay gate set is ci-only (no design-critic on routine PRs)" \
  "['ci']" "$(gates_of "$j10")"

# scenario — authored case content, machine-enforced by case_validator.py.
j11="$("$SCRIPT" --json -- cases/case01.yaml)"
assert_eq "case11: authored case YAML -> scenario" "scenario" "$(route_of "$j11")"
assert_eq "case11: scenario gate set is ci-only (case-author is not a second reviewer)" \
  "['ci']" "$(gates_of "$j11")"

j12="$("$SCRIPT" --json -- src/Noir.Scenario/Model.cs)"
assert_eq "case12: case-schema engine -> scenario" "scenario" "$(route_of "$j12")"
assert_eq "case12: scenario engine gate set is ci-only" "['ci']" "$(gates_of "$j12")"

# presentation — game/client/presentation code, an explicit route instead of
# falling through to the tooling catch-all.
j13="$("$SCRIPT" --json -- src/Noir.Game/Ui.cs)"
assert_eq "case13: game/client code -> presentation" "presentation" "$(route_of "$j13")"
assert_eq "case13: presentation gate set is ci-only" "['ci']" "$(gates_of "$j13")"

# architecture still composes on top of a gameplay-routed .csproj change.
j14="$("$SCRIPT" --json -- src/Noir.Rules/Foo.cs src/Noir.Rules/Noir.Rules.csproj)"
assert_eq "case14: base route unaffected by the architecture file" "gameplay" "$(route_of "$j14")"
assert_eq "case14: architecture flag set for a .csproj under src/Noir.Rules" "True" "$(arch_of "$j14")"
assert_eq "case14: gate set composes (architecture-review added, gameplay gates kept)" \
  "['architecture-review', 'ci']" "$(gates_of "$j14")"

# --- fixture diffs ----------------------------------------------------------- #
NUMERIC_DIFF="$WORKDIR/numeric.diff"
cat > "$NUMERIC_DIFF" <<'EOF'
diff --git a/src/Brp.Rules/Combat/RangeBands.cs b/src/Brp.Rules/Combat/RangeBands.cs
index 1111111..2222222 100644
--- a/src/Brp.Rules/Combat/RangeBands.cs
+++ b/src/Brp.Rules/Combat/RangeBands.cs
@@ -1,4 +1,4 @@
 switch (r) {
-    Range.Short => 15,
+    Range.Short => 20,
 }
EOF

BORING_DIFF="$WORKDIR/boring.diff"
cat > "$BORING_DIFF" <<'EOF'
diff --git a/tools/some-script.sh b/tools/some-script.sh
index 1111111..2222222 100644
--- a/tools/some-script.sh
+++ b/tools/some-script.sh
@@ -1,2 +1,2 @@
-echo hi
+echo hello
EOF

# --- case 5: C# numeric-threshold change -> formulas via content escalation  #
j5="$("$SCRIPT" --json --diff-file "$NUMERIC_DIFF")"
assert_eq "case5: numeric threshold change -> formulas" "formulas" "$(route_of "$j5")"
assert_eq "case5: content-escalated flag set" "True" "$(escalated_of "$j5")"

# --- fixture `gh` stub for --issue label lookups ---------------------------- #
MOCKBIN="$WORKDIR/bin"
mkdir -p "$MOCKBIN"
cat > "$MOCKBIN/gh" <<'MOCKGH'
#!/usr/bin/env bash
set -euo pipefail
# route.sh calls: gh issue view <N> --json labels --jq '...route:* labels...'
if [ "$1 $2" = "issue view" ]; then
  printf '%s\n' "${MOCK_ROUTE_LABELS:-}"
  exit 0
fi
echo "mock gh: unhandled args: $*" >&2
exit 1
MOCKGH
chmod +x "$MOCKBIN/gh"

# --- case 6: issue label route:formulas raises a boring diff -> formulas --- #
j6_base="$("$SCRIPT" --json --diff-file "$BORING_DIFF")"
assert_eq "case6 sanity: the boring diff alone is tooling" "tooling" "$(route_of "$j6_base")"
j6="$(PATH="$MOCKBIN:$PATH" MOCK_ROUTE_LABELS="formulas" "$SCRIPT" --json --diff-file "$BORING_DIFF" --issue 999)"
assert_eq "case6: issue label route:formulas raises a boring diff -> formulas" "formulas" "$(route_of "$j6")"

# --- case 7: issue label route:docs CANNOT lower an actual formulas diff --- #
j7="$(PATH="$MOCKBIN:$PATH" MOCK_ROUTE_LABELS="docs" "$SCRIPT" --json --diff-file "$NUMERIC_DIFF" --issue 999)"
assert_eq "case7: route:docs cannot lower a real formulas diff" "formulas" "$(route_of "$j7")"

# --- case 7b: issue label route:gameplay CANNOT lower a real formulas diff -- #
j7b="$(PATH="$MOCKBIN:$PATH" MOCK_ROUTE_LABELS="gameplay" "$SCRIPT" --json --diff-file "$NUMERIC_DIFF" --issue 999)"
assert_eq "case7b: route:gameplay cannot lower a real formulas diff" "formulas" "$(route_of "$j7b")"

# --- case 8: architecture composes with another route ----------------------- #
j8="$("$SCRIPT" --json -- src/Brp.Rules/Combat/RangeBands.cs Directory.Build.props)"
assert_eq "case8: base route unaffected by the architecture file" "rules" "$(route_of "$j8")"
assert_eq "case8: architecture flag set" "True" "$(arch_of "$j8")"
assert_eq "case8: gate set composes (architecture-review added, rules gates kept)" \
  "['architecture-review', 'ci', 'rules-conformance', 'scope-warden']" "$(gates_of "$j8")"

# --- case 9: route derivation away from the PR branch == on the PR branch -- #
# Build a throwaway git repo that stands in for "the PR branch", carrying its
# own copy of route.sh and .github/route-map so `--base` classification is
# self-contained. Classify it there (on-branch), then capture the identical
# patch to a file and classify THAT from this real checkout instead — which is
# a different git repository, on a different branch entirely (away from the
# PR branch) — and confirm the two agree exactly.
PRREPO="$WORKDIR/pr-repo"
mkdir -p "$PRREPO/tools" "$PRREPO/.github" "$PRREPO/src/Brp.Rules/Combat"
cp "$SCRIPT" "$PRREPO/tools/route.sh"
cp "$ROOT/.github/route-map" "$PRREPO/.github/route-map"
git -C "$PRREPO" init -q -b main
git -C "$PRREPO" config user.email "test@example.invalid"
git -C "$PRREPO" config user.name "test"
cat > "$PRREPO/src/Brp.Rules/Combat/Foo.cs" <<'EOF'
public static int Threshold(int x) => x switch
{
    1 => 5,
    _ => 0,
};
EOF
git -C "$PRREPO" add -A
git -C "$PRREPO" commit -q -m base
sed -i 's/1 => 5,/1 => 9,/' "$PRREPO/src/Brp.Rules/Combat/Foo.cs"
git -C "$PRREPO" commit -aq -m "pr change"

ON_BRANCH_JSON="$(cd "$PRREPO" && ./tools/route.sh --json --base HEAD~1)"
git -C "$PRREPO" diff HEAD~1...HEAD > "$WORKDIR/captured.diff"
OFF_BRANCH_JSON="$("$SCRIPT" --json --diff-file "$WORKDIR/captured.diff")"

assert_eq "case9: off-branch route == on-branch route" \
  "$(route_of "$ON_BRANCH_JSON")" "$(route_of "$OFF_BRANCH_JSON")"
assert_eq "case9: off-branch route is formulas (content-escalated)" \
  "formulas" "$(route_of "$OFF_BRANCH_JSON")"
assert_eq "case9: off-branch escalated flag == on-branch" \
  "$(escalated_of "$ON_BRANCH_JSON")" "$(escalated_of "$OFF_BRANCH_JSON")"
assert_eq "case9: off-branch gate set == on-branch gate set" \
  "$(gates_of "$ON_BRANCH_JSON")" "$(gates_of "$OFF_BRANCH_JSON")"

echo
if [ "$FAILURES" -eq 0 ]; then
  echo "test_route.sh: all checks passed"
  exit 0
else
  echo "test_route.sh: $FAILURES check(s) failed"
  exit 1
fi
