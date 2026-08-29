# Agent Team

Token cost is dominated by running expensive reasoning on cheap problems. This file
routes work to the smallest model that can do it correctly, and reserves expensive
reasoning for the two places where being wrong is costly and hard to detect.

## Roster

| Agent | Model | Effort | Use for | Cost posture |
|---|---|---|---|---|
| `rules-extractor` | Haiku | medium | Transcribing tables and stat blocks from the source PDF into ruleset JSON | Cheap, high volume |
| `scope-warden` | Haiku | low | One checklist against a diff — out-of-scope content, wrong source, era baselines, determinism | Cheapest gate; run first |
| `engine-dev` | Sonnet | medium | Implementing one settled Issue in C# | Workhorse |
| `case-author` | Sonnet | medium | Case YAML, Three Doors compliance, build coverage | Workhorse |
| `rules-conformance` | Opus | high | Adversarially verifying implemented mechanics against printed tables | Expensive, narrow |
| `design-critic` | Opus | xhigh | Phase-gate design review | Expensive, rare |
| Codex `conformance` | GPT (configured default) | high | **Independent** second verification of rules tables | Expensive, reserved |
| Codex `review` | GPT | high | Fresh-context review of a core-rules diff | Expensive, reserved |
| Codex `simcheck` | GPT | high | Independently re-deriving simulation math | Expensive, rare |

## Why Codex is in the team at all

A second Claude agent re-deriving a table tends to reproduce the first one's
reasoning, including its mistakes. A different model family is far less likely to.
That independence is the only thing Codex is here for, so it is used only where a
silent error would be expensive and hard to catch:

- **Rules conformance.** This project has already demonstrated the failure mode. Two
  Chaosium books were present in the repo with different success grades and different
  threshold rounding, and code derived from the wrong one looks entirely correct.
- **Core-rules review.** The guide's fresh-context review advice, applied to the one
  subsystem where independence is worth the second context load.

Do not use Codex for bulk implementation, transcription, or anything a Haiku agent
handles. It is a verification instrument, not a second workhorse.

Invoke via `tools/codex-agent.sh`. Sandbox is `read-only` for every role — the
verification agents must not be able to "fix" what they are checking. `DRY_RUN=1`
prints the command without running it.

## Routing rules

**Run cheap gates before expensive ones.** `scope-warden` costs almost nothing and
catches the errors most likely to appear. It runs before `rules-conformance`, never
after.

**Escalate on risk, not on size.** A three-line change to a threshold formula deserves
`rules-conformance` and a Codex cross-check. A three-hundred-line change to CLI
formatting deserves neither.

**Never route an open design question to an implementation agent.** `engine-dev`
stops and reports when it hits one. That is correct behavior, not a failure —
deciding it inside an implementation PR is how decisions get buried where no later
agent will find them.

**Verification agents get read-only tools.** An agent that can edit what it is checking
will eventually make the check pass instead of making the code right.

## The pipeline for a rules-engine change

```
Issue (ready)
  -> engine-dev            implement, one concern
  -> scope-warden          cheap checklist gate
  -> rules-conformance     verify every printed row
  -> codex conformance     independent cross-check   [core rules only]
  -> PR review + merge
```

For non-rules work — tooling, CLI, docs, case data — stop after `scope-warden`. The
expensive verification layers exist for one specific risk and buy nothing elsewhere.

## What this is expected to save

The guide this workflow follows estimates 35-60% token reduction per completed task
at full adoption. The model-routing layer is additive to that: the bulk of engine
work is transcription and bounded implementation, which does not need frontier
reasoning, while the small fraction that does gets more of it than a uniform setting
would allow.

Measure it rather than assuming it. Track, over ten tasks: agent turns from task
selection to merged PR, clarification questions asked, and tasks reopened because
acceptance criteria were incomplete.
