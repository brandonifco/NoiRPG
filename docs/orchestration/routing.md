# Verification routing

Every change acquires a **route** from what it touches, and the route determines
the **required gate set** — the reviewers that must pass before it can merge. This
is the machine-readable form of the pipeline in
[`docs/agent-team.md`](../agent-team.md): the orchestrator no longer remembers
which reviewers a change needs; it asks [`tools/route.sh`](../../tools/route.sh).

This is the source of truth consumed by the routing state machine (issue #62).

## How a route is derived

1. **Path baseline (simple, deterministic).** [`.github/route-map`](../../.github/route-map)
   maps each changed path to a coarse route, CODEOWNERS-style — the *last* matching
   rule wins for a given file. Across all changed files, the highest-precedence
   route is taken (`docs` < `tooling` < `rules` < `formulas`), and any file that
   maps to `architecture` adds an architecture review on top.
2. **Content escalation (precise, where it matters).** A `rules` change is promoted
   to `formulas` when the diff actually touches numeric tables or thresholds — so the
   expensive independent cross-check fires when a *value* could be wrong, not merely
   because a rules file was edited.

## Routes and required gates

| Route | What triggers it | Required gates |
|---|---|---|
| `docs` | `*.md`, `docs/**` | `ci`, `scope-warden` |
| `tooling` | `.github/**`, `tools/**`, `*.sln`, `global.json`, anything unmatched | `ci`, `scope-warden` |
| `rules` | `src/Brp.Core/**`, `src/Brp.Rules/**` (ordinary implementation) | `ci`, `scope-warden`, `rules-conformance` |
| `formulas` | `src/Brp.Data/**` ruleset JSON, **or** a `rules` change whose diff touches numeric tables/thresholds | `ci`, `scope-warden`, `rules-conformance`, `codex-conformance` |
| `architecture` | `**/*.csproj`, `Directory.Build.props` (project boundaries/refs) | *(above, per the other files)* **+** `architecture-review` |

`formulas` is a strict superset of `rules`. `architecture` composes with whatever
else applies — it adds `architecture-review` (dispatched to the `design-critic`
agent) without removing the normal gates. Escalation only ever *raises* the gate
set, never lowers it.

## Content-escalation patterns

A changed line (added or removed) in a `rules`/`formulas` file promotes the change
to `formulas` when it matches any of these table/threshold shapes. Keeping the set
explicit keeps the decision deterministic:

| Shape | Matches | Example |
|---|---|---|
| switch-arm / lambda to a number | `=> -?N` | `Grade.Special => 2` |
| JSON / property numeric value | `: -?N` | `"baseRange": 30` |
| numeric array / indexer | `[ -?N` | `[ 1, 3, 6 ]` |
| threshold comparison | `>= <= > <` followed by `-?N` | `roll <= skill / 5` |
| number in a list | `, -?N` | `Steps(1, 2, 4, 8)` |

The match is intentionally line-level and slightly over-inclusive: a false escalation
costs one extra (independent) verification pass; a false *de*-escalation would let a
wrong printed value merge unchecked. When in doubt, escalate.

## Usage

```bash
# Classify the current working-tree change
tools/route.sh

# Classify a change relative to a base ref (e.g. a PR)
tools/route.sh --base origin/main

# Classify specific paths
tools/route.sh src/Brp.Data/damage-ruleset.json

# Machine-readable, for the orchestrator / state machine
tools/route.sh --json --base origin/main
```

`--json` emits `{ "route", "architecture", "escalated", "gates": [...], "files": [...] }`.

## Labels

The derived route is surfaced on issues/PRs as a `route:*` label
(`route:docs`, `route:tooling`, `route:rules`, `route:formulas`,
`route:architecture`) so the route is visible without running the tool.
