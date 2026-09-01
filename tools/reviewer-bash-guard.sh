#!/usr/bin/env bash
# tools/reviewer-bash-guard.sh — a PreToolUse "command" hook that makes the
# verification reviewer roles (scope-warden, rules-conformance, design-critic)
# mechanically read-only for Bash, not just prompt-read-only (Issue #170,
# docs/decisions/0026-reviewer-mechanical-read-only.md).
#
# Registered in each reviewer's own frontmatter (`.claude/agents/*.md`), matched
# on the Bash tool, so it runs ONLY while that subagent is executing — Claude
# Code removes a subagent-frontmatter hook when the subagent finishes, so the
# main orchestrator thread and every implementer agent keep full Bash.
#
# Contract: read JSON on stdin (the PreToolUse payload), inspect
# .tool_input.command, and:
#   exit 0  -> approve (stdout/stderr ignored on approve)
#   exit 2  -> block; stderr is fed back to the agent as the reason
# Never exit non-{0,2}; a crash must not silently allow.
#
# Default-deny, allowlist-of-leaves posture (deliberately conservative — see the
# ADR's "evasion surface" section for why this does NOT attempt to parse
# compound shell grammar):
#   - Any chaining/substitution/redirection metacharacter anywhere in the
#     command string is an unconditional deny: ; & | $( ` < > \n
#   - No leading environment-variable assignment prefix (`FOO=bar cmd`).
#   - The command must tokenize cleanly (shlex); anything that doesn't is denied.
#   - The first token, taken by basename, must be one of a fixed set of
#     read-only leaf commands, each with its own narrow argument allowlist
#     below. Everything else is denied.
set -uo pipefail

exec python3 "$(dirname "${BASH_SOURCE[0]}")/reviewer_bash_guard.py"
