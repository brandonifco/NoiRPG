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

# Workflow state lives in GitHub's built-in Status field (Todo/In Progress/Done),
# which the default project workflows maintain automatically (item closed / PR
# merged -> Done). We do NOT add a custom "Stage" field — a second workflow field
# the automations would not touch. Verification Route mirrors tools/route.sh;
# Agent Role mirrors the roster.
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

# Bootstrap Status. The default "Item closed -> Done" workflow fires on the close
# EVENT and so does not retroactively touch items added while already closed; set
# those to Done here. Open items keep the "Item added -> Todo" default.
echo "setting Status for already-closed items:"
pid="$(gh project view "$num" --owner "$OWNER" --format json --jq .id)"
sid="$(gh project field-list "$num" --owner "$OWNER" --format json --jq '.fields[] | select(.name=="Status") | .id')"
done_opt="$(gh project field-list "$num" --owner "$OWNER" --format json \
  --jq '.fields[] | select(.name=="Status") | .options[] | select(.name=="Done") | .id')"
gh project item-list "$num" --owner "$OWNER" --format json \
  --jq '.items[] | [.id, (.content.number|tostring)] | @tsv' | while IFS=$'\t' read -r iid inum; do
  [ -z "$inum" ] && continue
  if [ "$(gh issue view "$inum" --json state --jq .state 2>/dev/null)" = "CLOSED" ]; then
    gh project item-edit --id "$iid" --project-id "$pid" --field-id "$sid" --single-select-option-id "$done_opt" >/dev/null 2>&1 \
      && echo "  #$inum -> Done"
  fi
done

echo
echo "Project: https://github.com/users/$OWNER/projects/$num"
echo
echo "Already-enabled default workflows maintain Status going forward (item closed /"
echo "PR merged -> Done; item added -> Todo). One optional UI-only step remains, since"
echo "the Projects v2 API cannot configure workflows:"
echo "  - Project -> ... -> Workflows -> 'Auto-add to project': repo items with label"
echo "    'orchestration' (sub-issues of the epic already auto-add)."
