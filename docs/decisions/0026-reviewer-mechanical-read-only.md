# 0026. Verification reviewers get a constrained, mechanically-enforced read-only Bash layer

## Status

Accepted — 2026-09-01. Resolves #170.

## Context

ADR 0004 states the intent: "Verification agents get read-only tools." In practice
`scope-warden`, `rules-conformance`, and `design-critic` (`.claude/agents/*.md`) list
`Bash` in their `tools:` grant — a general-purpose shell that can mutate the tree
(`sed -i`, `rm`, `git commit`, `gh api`, `curl`), install packages, or exfiltrate data,
none of which their prompt-level "you are read-only" instructions can actually stop
against a misbehaving or subverted agent. That is *procedural* rail, not *capability*
isolation: it works against a forgetful agent and does nothing against an adversarial
one.

The Codex verification path already runs under a real `-s read-only` OS sandbox
(`tools/codex-agent.sh`), so before this change it had strictly stronger mechanical
isolation than the Claude reviewers sitting beside it in the same verification tier.

The reviewers do have a legitimate, narrow need to execute commands: `git
show`/`git diff`/`git log` to read history the packet's diff doesn't carry (e.g. a
cited prior revision), `pdftotext` to pull ORC source pages for `rules-conformance`
(the pattern already used throughout `docs/decisions/*.md` and `tools/source-slice.py`),
and `grep`/`rg` to search within files they've already read. Dropping `Bash` entirely
(Issue #170 option 1) would cost `rules-conformance` its own primary verification
method — "open the book yourself" (its own AGENTS.md-derived method) requires running
`pdftotext`, not just reading a pre-cut packet slice.

## Decision

Keep `Bash` in the `tools:` grant for `scope-warden`, `rules-conformance`, and
`design-critic`, but gate every Bash call **while those specific subagents are
running** through a default-deny, read-only allowlist: `tools/reviewer-bash-guard.sh`
(a thin wrapper) → `tools/reviewer_bash_guard.py` (the actual policy).

### Enforcement mechanism — subagent-frontmatter `PreToolUse` hook

Claude Code hooks support exactly the scoping this needs: hooks declared in a
subagent's own YAML frontmatter run "only while that subagent is running" and are
removed when it finishes (Claude Code hooks reference, "Hooks in skills and agents").
Each reviewer's frontmatter now carries:

```yaml
hooks:
  PreToolUse:
    - matcher: "Bash"
      hooks:
        - type: command
          command: "tools/reviewer-bash-guard.sh"
```

This is strictly narrower than a project-wide `.claude/settings.json` hook matched on
`agent_type` (the common `PreToolUse` input also carries `agent_id`/`agent_type` for
subagent calls, which would have worked too): the frontmatter form cannot fire outside
the three files it is declared in, so the main orchestrator thread and every
implementer agent (`engine-dev`, `orchestration-dev`, `case-author`,
`rules-extractor`) keep unrestricted `Bash`, with zero risk of the guard leaking onto
them by a matcher typo. No `.claude/settings.json` changes were needed or made.

The hook receives the `PreToolUse` JSON payload on stdin (`tool_name`, `tool_input.command`,
plus session metadata), and communicates its verdict through the process exit code:
`0` approves, `2` blocks and returns `stderr` to the agent as the reason. A hook that
crashes or exits any other code is a bug, not an allow — `reviewer_bash_guard.py`
always exits `0` or `2` explicitly, including on malformed JSON input.

### Allowlist design — default-deny, allowlist of leaf commands

`tools/reviewer_bash_guard.py` (`evaluate(command)`) applies, in order:

1. **Reject on sight** if the raw command string contains any of
   `; & && || | $( `` <( >( > < <newline>` — anywhere, regardless of quoting context.
   This is the answer to the evasion surface named in the Issue: compound commands
   (`;`, `&&`), pipes, command/process substitution (`$(...)`, `` `...` ``, `<(...)`),
   redirection (`>`, `>>`, `<`), and backgrounding (`&`). No attempt is made to parse
   these safely in some contexts and unsafely in others — every occurrence denies,
   full stop.
2. **Reject** a leading `VAR=value` environment-variable-assignment prefix (a classic
   way to smuggle configuration into an otherwise-innocent-looking command, e.g.
   `GIT_PAGER=... git show` or a `LD_PRELOAD=...`).
3. **Reject** anything that does not tokenize cleanly under `shlex.split` (an
   unbalanced quote is treated as hostile input, not a parsing inconvenience).
4. **Reject** any program whose basename is not in the fixed leaf allowlist:
   `git, pdftotext, grep, rg, ls, find, cat, sed, wc, head, tail, sort, uniq, diff,
   file, stat, tree, dotnet, pwd, basename, dirname, echo`.
5. **Per-command narrowing** for the leaves that have a write-capable form even
   without shell metacharacters:
   - `git` — only `show`, `diff`, `log`, `cat-file`, `rev-parse`, `grep`, `status`
     subcommands; `-c`, `-C`, `--exec`, `--upload-pack`, `--receive-pack`, `--output`,
     `-o`, `--pager` are denied outright (each can write a file, reconfigure the repo,
     or hand control to an arbitrary program via the pager/exec hooks).
   - `pdftotext` — the last argument must be exactly `-` (stdout). `pdftotext file.pdf
     out.txt` writes a file with no shell redirection needed at all; requiring
     stdout-only closes that.
   - `sed` — `-i`/`--in-place` denied. As a conservative, non-exhaustive substitute for
     fully parsing sed script grammar, any non-flag argument containing a `w` or `e`
     command marker (sed's own write-to-file and execute-as-shell script commands) is
     also denied — see Known limitations.
   - `find` — `-exec`, `-execdir`, `-delete`, `-fprintf`, `-fls`, `-ok`, `-okdir` denied
     (each executes or mutates without any shell metacharacter in sight).
   - `dotnet` — only `build`, `test` (present for a reviewer that genuinely needs to
     confirm the tree builds; neither subcommand can write outside `obj`/`bin`).
   - `grep`, `rg`, `ls`, `cat`, `wc`, `head`, `tail`, `sort`, `uniq`, `diff`, `file`,
     `stat`, `tree`, `pwd`, `basename`, `dirname`, `echo` — no further narrowing; none
     of these has a write/exec form once redirection and substitution are already
     denied by step 1.

Package installs (`pip`, `npm`, `apt`, `dotnet add package`, …) and network egress
(`curl`, `wget`, `ssh`, `nc`, `dotnet nuget push`, …) are denied by omission — they are
simply not in `_ALLOWED_LEAVES`.

## Known limitations (read before trusting this against a genuinely adversarial agent)

- **Pipes are denied entirely**, including harmless ones (`git show HEAD:x | grep y`).
  A reviewer that wants to search within a git object must `git show` it and then
  `grep`/read it as two separate calls. This is the direct cost of the conservative
  posture in the Issue's brief: parsing "this compound command happens to be safe" is
  exactly the kind of shell-grammar special-casing this design refuses to attempt.
- **The `sed` write/exec-marker check is a heuristic, not a parser.** A sufficiently
  obscure sed script could still smuggle a `w` or `e` command past a naive substring
  check (or trigger a false deny on an innocent pattern that happens to contain `/w`).
  Reviewers are expected to use `sed` only for the display forms already established in
  this repo (`sed -n '10,20p'`) — nothing here should be read as a general-purpose sed
  sandbox.
- **PATH is not restricted.** A basename match against `git`/`grep`/etc. resolves
  through the normal system `PATH`. If an agent could plant a same-named executable
  earlier on `PATH` than the real one, the guard would approve running it. Nothing in
  this Issue's scope changes `PATH` or the execution environment to close that; it is a
  pre-existing property of running Bash at all, not something this allowlist introduces
  or claims to fix.
- **This is a single command-hook process, not an OS sandbox.** Unlike
  `tools/codex-agent.sh`'s real `-s read-only` sandbox, `reviewer-bash-guard.sh` is a
  policy check that runs *before* the shell executes the command string — it has no
  enforcement power once a command it approved is actually running (e.g. it cannot stop
  `dotnet build` from doing something unexpected via a malicious `.csproj` target). It
  narrows what can be *attempted*, not what a successfully-launched allowed program can
  do.
- **Scope is exactly the three reviewer subagents, by design — not a repo-wide Bash
  policy.** `engine-dev`, `orchestration-dev`, `case-author`, `rules-extractor`, and the
  main orchestrator thread are unaffected; this ADR does not claim otherwise and no
  other agent's `tools:` grant changed.

None of these limitations were judged severe enough to withhold the change: each
narrows what an adversarial reviewer agent could do today (unrestricted `Bash`) to a
short, auditable list of read-only leaf commands, closing off `rm`, `git commit`,
`git push`, `curl`, `pip install`, and arbitrary shell chaining outright. They are
recorded here so a future change to this allowlist starts from an accurate picture of
what it does and does not guarantee, rather than assuming it is a full sandbox.

## Alternatives considered

**Drop `Bash` entirely (Issue #170 option 1).** Simplest, and genuinely stronger for
`scope-warden` (which only ever needs the packet's own diff). Rejected as the
uniform answer because `rules-conformance`'s stated method is "open the book
yourself" via `pdftotext` — its highest-value catches (the two documented near-misses
in ADR 0004: the wrong source book, the wrong rounding rule) came from checking the
primary source directly, not from trusting a pre-cut packet slice. Removing `Bash`
there would remove the one thing that makes it worth running at frontier cost.

**Isolated read-only checkout/worktree (Issue #170 option 3).** Strongest isolation in
principle. Rejected for now as more infrastructure than the actual risk justifies: it
would need a maintained parallel checkout or mount per review, wired into whatever
spawns these subagents, for a marginal gain over an allowlisted command layer once
`sed -i`/`rm`/`git commit`/redirection are already denied. Revisit if the allowlist
proves insufficient in practice.

**A project-wide `.claude/settings.json` `PreToolUse` hook matched on `agent_type`.**
Would also work — the common hook input carries `agent_id`/`agent_type` for subagent
tool calls — but is strictly worse-scoped than the frontmatter form: a settings-file
hook lives for the whole session and depends on getting the `agent_type` matcher
exactly right, where a matcher bug fails open (the hook simply never matches and
`Bash` stays unrestricted) rather than failing closed. The subagent-frontmatter hook
instead cannot exist outside the three files it is declared in. Not adopted; no
`.claude/settings.json` was created or modified by this change.

## Consequences

- `tools/reviewer-bash-guard.sh` (wrapper) and `tools/reviewer_bash_guard.py` (policy,
  reusable as a library for tests) are new. `tests/tooling/test_reviewer_bash_guard.sh`
  exercises: an allowed read command passes, a mutating command is denied, and a
  chained/evasion attempt (`;`, `|`, `$(...)`, redirection, `bash -c`, `xargs`) is
  denied — matching the Issue's required test shape.
- `.claude/agents/scope-warden.md`, `.claude/agents/rules-conformance.md`, and
  `.claude/agents/design-critic.md` each gained a `hooks.PreToolUse` frontmatter block
  and a short pointer to this ADR in their body text. No other agent file changed.
- No existing CI gate weakened. `tools/orchestration-policy.sh`,
  `tools/agent-verify.sh`, and the route/gate machinery are untouched — this closes a
  capability-isolation gap in the reviewer roles, it does not touch what evidence a PR
  must produce to merge.
