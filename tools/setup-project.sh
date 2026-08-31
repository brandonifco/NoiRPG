#!/usr/bin/env bash
# tools/setup-project.sh — create the NoiRPG GitHub Project (v2) with the
# orchestration fields and add the epic's issues.
#
# Requires the 'project' WRITE scope, which the default token does not have:
#     gh auth refresh -s project
# (The orchestrator's token has read:project only, so it cannot run this — that is
# why this is a script you run once, not an API call the tooling makes.)
#
# Idempotent: re-running reuses the project and skips fields/items that exist.
set -euo pipefail
OWNER=brandonifco
TITLE="NoiRPG"

num="$(gh project list --owner "$OWNER" --format json \
  --jq ".projects[] | select(.title==\"$TITLE\") | .number" | head -1)"
if [ -z "$num" ]; then
  num="$(gh project create --owner "$OWNER" --title "$TITLE" --format json --jq .number)"
  echo "created project #$num"
else
  echo "reusing existing project #$num"
fi

mkfield() { # name  datatype  [comma,options]
  local name="$1" dt="$2" opts="${3:-}"
  if gh project field-list "$num" --owner "$OWNER" --format json --jq '.fields[].name' | grep -qx "$name"; then
    echo "  field exists: $name"; return
  fi
  if [ -n "$opts" ]; then
    gh project field-create "$num" --owner "$OWNER" --name "$name" --data-type "$dt" --single-select-options "$opts" >/dev/null
  else
    gh project field-create "$num" --owner "$OWNER" --name "$name" --data-type "$dt" >/dev/null
  fi
  echo "  + field: $name"
}

# Workflow states from #60; Status is GitHub's built-in, so we add "Stage" to avoid
# fighting it. Verification Route mirrors tools/route.sh; Agent Role mirrors the roster.
mkfield "Stage"                       SINGLE_SELECT "Backlog,Specified,Ready,Implementing,PR/Verifying,Merged"
mkfield "Layer"                       SINGLE_SELECT "L0,L1,L2,L3,L4,Orchestration"
mkfield "Subsystem"                   TEXT
mkfield "Risk"                        SINGLE_SELECT "low,medium,high"
mkfield "Verification Route"          SINGLE_SELECT "docs,tooling,rules,formulas,architecture"
mkfield "Agent Role"                  SINGLE_SELECT "engine-dev,case-author,scope-warden,rules-conformance,design-critic,rules-extractor,codex"
mkfield "Source-Conformance Required" SINGLE_SELECT "yes,no"

echo "adding items:"
for n in 53 54 55 56 57 58 59 60 61 62 63 65 73; do
  if gh project item-add "$num" --owner "$OWNER" --url "https://github.com/$OWNER/NoiRPG/issues/$n" >/dev/null 2>&1; then
    echo "  + #$n"
  else
    echo "  #$n (already present)"
  fi
done

echo
echo "Project: https://github.com/users/$OWNER/projects/$num"
echo
echo "Finish in the UI (Project -> ... -> Workflows), enabling the built-in automations:"
echo "  - Auto-add to project: repo items with label 'orchestration'."
echo "  - Item closed        -> set Stage = Merged."
echo "  - Item reopened      -> set Stage = Implementing."
echo "  - Pull request merged -> set Stage = Merged."
