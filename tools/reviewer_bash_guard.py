#!/usr/bin/env python3
"""tools/reviewer_bash_guard.py — the read-only Bash allowlist for verification
reviewer agents (Issue #170, docs/decisions/0025-reviewer-mechanical-read-only.md).

This module is imported directly by tests/tooling/test_reviewer_bash_guard.py and
invoked as a Claude Code PreToolUse "command" hook via tools/reviewer-bash-guard.sh,
which is registered ONLY in the frontmatter of the reviewer subagents
(scope-warden, rules-conformance, design-critic). Claude Code scopes a
subagent-frontmatter hook to that subagent's own run and removes it when the
subagent finishes, so nothing outside those three roles is affected.

Design posture (see the ADR for the full rationale): default-deny, allowlist of
known-safe LEAF commands with per-command argument restrictions. This module
does not attempt to parse general shell grammar. Any lexical evidence of
compounding — command separators, substitution, redirection, backgrounding — is
an unconditional deny, on the assumption that a hand-rolled parser for arbitrary
POSIX shell will eventually be wrong in the attacker's favor. Deny first,
recognize a specific safe shape second.
"""
from __future__ import annotations

import json
import shlex
import sys

# Any occurrence of these substrings anywhere in the raw command string is an
# unconditional deny: statement/pipe chaining, command substitution, process
# substitution, redirection, backgrounding. This is intentionally coarse — it
# will also reject some harmless commands (e.g. a literal ">" in a grep
# pattern), and that is the point: a false deny costs an agent one retry with a
# different command, a false allow costs mechanical isolation.
_DENYLIST_SUBSTRINGS = (
    ";",
    "&&",
    "||",
    "&",
    "|",
    "$(",
    "`",
    "<(",
    ">(",
    ">",
    "<",
    "\n",
    "\\\n",
)

# Leaf commands (matched by basename of argv[0]) that may run at all, each with
# its own arg-level allow/deny rules below. Nothing outside this set runs.
_ALLOWED_LEAVES = frozenset(
    {
        "git",
        "pdftotext",
        "grep",
        "rg",
        "ls",
        "find",
        "cat",
        "sed",
        "wc",
        "head",
        "tail",
        "sort",
        "uniq",
        "diff",
        "file",
        "stat",
        "tree",
        "dotnet",
        "pwd",
        "basename",
        "dirname",
        "echo",
    }
)

_GIT_ALLOWED_SUBCOMMANDS = frozenset({"show", "diff", "log", "cat-file", "rev-parse", "grep", "status"})
# git flags that can themselves cause a write, spawn an arbitrary program, or
# hand control to a pager/editor/hook — denied regardless of subcommand.
_GIT_DENIED_FLAG_PREFIXES = (
    "-c",
    "-C",
    "--exec",
    "--upload-pack",
    "--receive-pack",
    "--output",
    "-o",
    "--pager",
    "-p=",
)

_FIND_DENIED_FLAGS = frozenset(
    {
        "-exec",
        "-execdir",
        "-delete",
        "-fprintf",
        "-fls",
        "-ok",
        "-okdir",
    }
)

_DOTNET_ALLOWED_SUBCOMMANDS = frozenset({"build", "test"})

def _looks_like_env_assignment(token: str) -> bool:
    if "=" not in token:
        return False
    name = token.split("=", 1)[0]
    return name.isidentifier() or (name and (name[0].isalpha() or name[0] == "_"))


def evaluate(command: str) -> tuple[bool, str]:
    """Return (allowed, reason). reason explains a deny; empty string on allow."""
    if command is None:
        return False, "no command given"

    for needle in _DENYLIST_SUBSTRINGS:
        if needle in command:
            return False, (
                f"denied: command contains {needle!r}, which can chain, substitute, "
                "redirect, or background a command — read-only reviewer Bash does "
                "not evaluate compound shell grammar, it denies it outright"
            )

    try:
        tokens = shlex.split(command, posix=True)
    except ValueError as exc:
        return False, f"denied: command did not tokenize cleanly ({exc})"

    if not tokens:
        return False, "denied: empty command"

    # No leading VAR=value env-assignment prefix before the real program.
    if _looks_like_env_assignment(tokens[0]):
        return False, f"denied: leading environment-variable assignment {tokens[0]!r} is not allowed"

    program_token = tokens[0]
    program = program_token.rsplit("/", 1)[-1]

    if program not in _ALLOWED_LEAVES:
        return False, f"denied: {program!r} is not in the reviewer read-only allowlist"

    args = tokens[1:]

    if program == "git":
        return _check_git(args)
    if program == "pdftotext":
        return _check_pdftotext(args)
    if program == "sed":
        return _check_sed(args)
    if program == "find":
        return _check_find(args)
    if program == "dotnet":
        return _check_dotnet(args)

    # grep, rg, ls, cat, wc, head, tail, sort, uniq, diff, file, stat, tree,
    # pwd, basename, dirname, echo: no known write/exec form once the global
    # denylist above has already ruled out redirection and substitution.
    return True, ""


def _check_git(args: list[str]) -> tuple[bool, str]:
    if not args:
        return False, "denied: git with no subcommand"
    for a in args:
        for bad in _GIT_DENIED_FLAG_PREFIXES:
            if a == bad or a.startswith(bad):
                return False, f"denied: git flag {a!r} can write, reconfigure, or exec"
    subcommand = next((a for a in args if not a.startswith("-")), None)
    if subcommand not in _GIT_ALLOWED_SUBCOMMANDS:
        return False, f"denied: git subcommand {subcommand!r} is not in {sorted(_GIT_ALLOWED_SUBCOMMANDS)}"
    return True, ""


def _check_pdftotext(args: list[str]) -> tuple[bool, str]:
    # pdftotext <pdf> <output>; require the last argument to be "-" (stdout)
    # so it can never create or overwrite a file on disk.
    if not args or args[-1] != "-":
        return False, "denied: pdftotext must write to stdout ('-' as the last argument), never to a file"
    return True, ""


def _check_sed(args: list[str]) -> tuple[bool, str]:
    for a in args:
        if a == "-i" or a.startswith("-i") or a == "--in-place" or a.startswith("--in-place"):
            return False, "denied: sed -i / --in-place mutates files"
    # sed scripts can also write via a `w file` command or execute via `e` —
    # this guard does not parse sed script grammar, so as a conservative
    # substitute it denies any script argument that contains a 'w' or 'e'
    # command marker adjacent to a slash-delimited sed expression.
    for a in args:
        if a.startswith("-"):
            continue
        if "/w" in a or "/e" in a or a.strip().startswith("w ") or a.strip().startswith("e "):
            return False, "denied: sed script appears to use a write ('w') or exec ('e') command"
    return True, ""


def _check_find(args: list[str]) -> tuple[bool, str]:
    for a in args:
        if a in _FIND_DENIED_FLAGS:
            return False, f"denied: find flag {a!r} can execute or mutate"
    return True, ""


def _check_dotnet(args: list[str]) -> tuple[bool, str]:
    subcommand = next((a for a in args if not a.startswith("-")), None)
    if subcommand not in _DOTNET_ALLOWED_SUBCOMMANDS:
        return False, f"denied: dotnet subcommand {subcommand!r} is not in {sorted(_DOTNET_ALLOWED_SUBCOMMANDS)}"
    return True, ""


def main() -> int:
    raw = sys.stdin.read()
    try:
        payload = json.loads(raw) if raw.strip() else {}
    except json.JSONDecodeError:
        print("reviewer-bash-guard: denied: could not parse hook input as JSON", file=sys.stderr)
        return 2

    tool_name = payload.get("tool_name", "")
    if tool_name != "Bash":
        # Not our tool to gate; approve so other tools are unaffected. The
        # frontmatter matcher already restricts this hook to Bash, so this is
        # a belt-and-suspenders no-op in normal operation.
        return 0

    command = (payload.get("tool_input") or {}).get("command", "")
    allowed, reason = evaluate(command)
    if allowed:
        return 0
    print(f"reviewer-bash-guard: {reason}", file=sys.stderr)
    return 2


if __name__ == "__main__":
    sys.exit(main())
