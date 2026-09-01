# Verification routing

Every change acquires a **route** from what it touches, and the route determines
the **required gate set** — the reviewers that must pass before it can merge. This
is the machine-readable form of the pipeline in
[`docs/agent-team.md`](../agent-team.md): the orchestrator no longer remembers
which reviewers a change needs; it asks [`tools/route.sh`](../../tools/route.sh).

Route derivation and labelling are live and consumed by `pr-policy` (which records
the route in its evidence artifact). The gate **enforcement** that once sat on top of
this — the `gates-satisfied` aggregator and the gate-poster App — was removed in
#90/#91; the "required gates" below are the reviewers a change *conceptually* needs,
no longer an automated check.

`tools/route.sh` is the **one route authority** (#137): it is the only place that
parses a diff for content escalation, composes architecture, and applies the
issue-intent floor. Every consumer that needs to classify a change it did not
generate locally — `agent-verify.sh` when the working tree isn't the PR head,
`pr_policy.py` once it knows the linked Issue, `agent-brief.py`'s review packet —
calls this script (via `--diff-file` for an externally captured patch) rather than
approximating any of this itself.

## How a route is derived

1. **Path baseline (simple, deterministic).** [`.github/route-map`](../../.github/route-map)
   maps each changed path to a coarse route, CODEOWNERS-style — the *last* matching
   rule wins for a given file. Across all changed files, the highest-precedence
   route is taken (`docs` < `tooling` < `presentation` < `scenario` < `gameplay` <
   `rules` < `formulas`), and any file that maps to `architecture` adds an
   architecture review on top.
2. **Content escalation (precise, where it matters).** A `rules` change is promoted
   to `formulas` when the diff actually touches numeric tables or thresholds — so the
   expensive independent cross-check fires when a *value* could be wrong, not merely
   because a rules file was edited.
3. **Issue-intent floor (asymmetric).** A change may declare, via its issue's
   `route:*` label, that it is riskier than its filenames suggest. The label can only
   *raise* the route — `max(diff-route, issue-route)` — never lower it. Pass it with
   `tools/route.sh --issue <n>` (reads the label) or `--issue-route <route>` (supplies
   it directly). A `route:architecture` intent *adds* the architecture review rather
   than replacing the base route.

## Routes and required gates

| Route | What triggers it | Required gates |
|---|---|---|
| `docs` | `*.md`, `docs/**` | `ci` |
| `tooling` | `.github/**`, `tools/**`, `*.sln`, `global.json`, anything unmatched | `ci` |
| `presentation` | `src/Noir.Game/**`, `src/Noir.Client/**` (game engine / client / presentation code — Layer 5) | `ci` |
| `scenario` | `cases/**`, `src/Noir.Scenario/**` (authored case content and the case-schema engine — Layer 5) | `ci` |
| `gameplay` | `src/Noir.Rules/**` (original Noir mechanics — Layer 5) | `ci` |
| `rules` | `src/Brp.Core/**`, `src/Brp.Rules/**`, `src/Brp.Data/**/*.cs` (loaders/models — ordinary implementation) | `ci`, `scope-warden`, `rules-conformance` |
| `formulas` | `src/Brp.Data/**/*.json` (printed numeric tables), **or** a `rules` change whose diff touches numeric tables/thresholds | `ci`, `scope-warden`, `rules-conformance`, `codex-conformance` |
| `architecture` | `**/*.csproj`, `Directory.Build.props` (project boundaries/refs) | *(above, per the other files)* **+** `architecture-review` |

`formulas` is a strict superset of `rules`. `architecture` composes with whatever
else applies — it adds `architecture-review` (dispatched to the `design-critic`
agent) without removing the normal gates. Escalation only ever *raises* the gate
set, never lowers it.

### `gameplay`, `scenario`, `presentation` — design-led, not BRP source-conformance

These three routes exist so Layer 5 — the noir game layer on top of BRP — gets an
explicit classification instead of falling through to the `tooling` catch-all once it
starts landing. Their gate set is `ci` only, deliberately: none of them is checked
against a printed source table, so none of them needs `rules-conformance` or
`codex-conformance`, and none of them gets a semantic AI reviewer on every routine PR.

- **`gameplay`** is original Noir mechanics — not BRP, so there is no source table to
  conform to. A settled `gameplay` change relies on CI, deterministic tests, and
  layer/scope enforcement, plus the prior accepted design decision that authorized the
  mechanic in the first place. `design-critic` (Opus, phase-gate design review) belongs
  at design/phase gates — the point where the mechanic is *decided* — not on every
  routine implementation PR that merely builds what was already decided.
- **`scenario`** is authored case/scenario content. Its correctness is machine-checked
  by [`tools/case_validator.py`](../../tools/case_validator.py) — schema, the Three
  Doors rule, the junction budget, canonical skill names, and load/parse validity — all
  deterministic. That validator is not merely *pointed at* this route: the required
  `build-and-test` job runs it over every `cases/*.yaml` on every PR (see
  [`.github/workflows/ci.yml`](../../.github/workflows/ci.yml), "Validate case data"),
  so a malformed case fails a required check and cannot merge behind a green build —
  which is what makes `ci`-only a sufficient gate for this route. `case-author` (Sonnet,
  see [`agent-team.md`](../agent-team.md)) is a content-producing role, not a second
  reviewer; it does not get paired with an Opus review on ordinary case YAML.
- **`presentation`** is game engine / client / presentation code. It gets its own route
  rather than the generic `tooling` catch-all so its gate set is legible on its own
  terms, even though none of `src/Noir.Game/**` or `src/Noir.Client/**` exists yet —
  this route is added ahead of the code, not in response to it.

A `**/*.csproj` or `Directory.Build.props` change under any of these paths still
composes `architecture-review` on top, exactly as it does for `rules`/`formulas` —
the architecture rules are last in `.github/route-map`, so they still win for those
files.

None of this authorizes Layer 5 implementation. `needs-design` remains a scheduler
stop (`tools/ready-issues.sh` treats it as a human gate alongside `blocked`): an
unresolved original-design Issue must not be picked up for implementation merely
because its dependencies have closed. The route taxonomy tells verification which
gates a Layer 5 change will need *once it is settled and ready* — it does not settle
the design itself.

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
| plain / const assignment (also `==`, `!=`) | `= -?N` | `public const int MaximumRoll = 99;` |
| numeric return | `return -?N` | `return baseDamage * 3;` |
| arithmetic on a number | `* / %` followed by `-?N` | `baseDamage * 3` |

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

# Raise the route to a linked issue's declared intent (never lowers it)
tools/route.sh --base origin/main --issue 112
tools/route.sh --issue-route formulas src/SomeLoader.cs

# Classify a patch that wasn't generated in this checkout — e.g. a PR's actual
# diff, fetched with `gh pr diff` while HEAD is somewhere else entirely. Content
# escalation, architecture composition, and --issue all work exactly as above.
gh pr diff 137 > /tmp/pr137.diff
tools/route.sh --diff-file /tmp/pr137.diff --issue 137

# Machine-readable, for the orchestrator / state machine
tools/route.sh --json --base origin/main
```

`--json` emits
`{ "route", "architecture", "escalated", "issueRoute", "issueRaised", "gates": [...], "files": [...] }`.

`src/Brp.Data` is split by kind: the `*.json` files are the printed numeric tables
(route `formulas` — verify every value), while the `*.cs` loaders/models route to
`rules` and are promoted to `formulas` only when their diff actually touches numeric
content. So editing a loader no longer triggers the expensive Codex route.

## Labels

The derived route can be surfaced on issues/PRs as a `route:*` label
(`route:docs`, `route:tooling`, `route:presentation`, `route:scenario`,
`route:gameplay`, `route:rules`, `route:formulas`, `route:architecture`) so the route
is visible without running the tool. These were applied automatically by the
now-removed `route-gates` workflow; apply them by hand (or from a new workflow) if
you want them.

A `route:*` label on an **issue** is also an *input*, not just a readout: it declares
the change's intended risk, and `tools/route.sh --issue <n>` reads it as the
asymmetric floor described above.

## Enforcement

Merges into `main` are gated by **four** required status checks: `build-and-test`,
`pr-policy`, `orchestration-policy` (strict policy: the branch must be current
with `main`), and `agent-verification`. The model-driven per-route gates
(`scope-warden`, `rules-conformance`, `codex-conformance`, `architecture-review`)
and the `gates-satisfied` aggregate that once combined them were removed in #90/#91
— they never posted a result on any PR directly. The route table above is retained
as the map of which review a change *should* get; that map is now enforced through
`agent-verification`, not bypassed.

The enforcer is [`tools/agent-verify.sh`](../../tools/agent-verify.sh): the local
orchestrator runs the route's gates for a PR head SHA and posts a single
`agent-verification` commit status — success only if `ci` passed *and* every other
gate the route requires was supplied as `pass`. It is a **required** check
alongside, not instead of, `build-and-test` / `pr-policy` / `orchestration-policy`:
a PR needs all four green on its current head SHA to merge. See
[`agent-verification.md`](agent-verification.md) for how the status is produced and
[`agent-verification-burn-in.md`](agent-verification-burn-in.md) for the evidence
behind requiring it.

### Merge contract

A PR into `main` is mergeable only when all of the following hold on its current
head SHA at once:

1. `pr-policy` is green (a real `Closes #<n>` / `Fixes #<n>`, and the PR body meets
   policy).
2. `orchestration-policy` is green (branch is up to date with `main` under the
   ruleset's strict-status policy).
3. `build-and-test` is green (build, test, format CI).
4. Review conversations are resolved.
5. `agent-verification` is a success for the exact current head SHA — i.e. every
   gate `tools/route.sh` derives for this change's route was supplied as `pass`
   (see the route table above), not just CI.

There is no separate, invented human-approval step in the contract — `pr-policy`,
`orchestration-policy`, and `build-and-test` are unchanged and independently
required; `agent-verification` adds the route's model-driven gate set as a fourth,
SHA-bound requirement rather than replacing any of the first three.

