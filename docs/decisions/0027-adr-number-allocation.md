# 0027. ADR-number allocation is serialized at merge time, not authoring time

## Status

Accepted — 2026-09-01. Resolves #185.

## Context

Burn-in finding **F8** (`docs/orchestration/agent-verification-burn-in.md`): #170 and
#171 ran in parallel, each read `0024` as the latest committed decision record on its
own base, and each authored `0025`. The collision only surfaced at merge time, and the
orchestrator had to renumber #170's record to `0026` by hand, updating every reference
(its own header, its `docs/decisions/README.md` row, and any cross-links) manually.

ADR numbers were being chosen at *authoring* time, by eyeballing the latest committed
record on the author's own branch. That races under exactly the condition the agent
team is built for: multiple implementers working in parallel off a common, possibly
stale, base. `tools/adr-index-check.sh` (Issue #189) detects the resulting drift — a
duplicate number, a gap — after the fact; it does not prevent it.

This is a house rule, not a sourced mechanic — orchestration process, out of scope for
`docs/source-handling.md`.

## Decision

**Merge-time assignment.** A design-decision author writes their ADR with a literal
placeholder number, `NNNN`, everywhere the number would normally appear: the filename
(`docs/decisions/NNNN-slug.md`), the `# NNNN. Title` header, any self-reference inside
the body, and the `docs/decisions/README.md` index row they add. Two authors on
parallel branches can both write `NNNN` — there is nothing to collide over, because no
number has actually been chosen yet.

The number is assigned exactly once, by `tools/assign-adr-number.sh`, run as a
pre-merge step against the tree that is about to land (i.e. by whoever/whatever is
about to merge the PR — the orchestrator, run by hand today; nothing here requires a
new automated merge gate). The tool:

1. Finds the `NNNN-*.md` placeholder(s) in `docs/decisions/`.
2. Computes the next free four-digit number from the union of existing
   `docs/decisions/NNNN-*.md` filenames and `README.md` index rows.
3. Renames the file and rewrites every `NNNN` occurrence inside it to the assigned
   number.
4. Rewrites the matching `README.md` row (matched by the placeholder's *old filename*,
   so an unrelated in-flight placeholder with a different slug is left alone).
5. Rewrites any other tracked Markdown file that links to the placeholder's exact old
   filename.

If two placeholders are present when the tool runs (the literal #170/#171 shape,
replayed against a single merged tree instead of two independent branches), it assigns
them sequential numbers in filename order — deterministic, not a second race, because
by the time the tool runs there is exactly one tree and one invocation.

### Considered and rejected

- **(a) A `tools/next-adr.sh` helper authors call themselves.** Simplest to build, but
  does not fix the bug: two authors can each call it before either has merged, and
  both still get told the same "next" number. Rejected — this is the scheme that
  already failed on #170/#171 in spirit (the number is still picked from the
  possibly-stale state each author happens to see).
- **(c) A reservation registry** — a committed file where a number is claimed before
  authoring begins. Explicit and auditable, but it is itself an append-only file that
  two parallel branches can both append to and then merge — the exact F9-class merge
  hazard (`docs/orchestration/agent-verification-burn-in.md`) this issue is trying to
  avoid, just relocated to a different file. Rejected.
- **(b) Merge-time assignment (chosen).** The only option under which the number is
  read from, and written to, a single tree at a single point in (logical) time. It
  costs a merge-time step and reference-rewriting, both of which
  `tools/assign-adr-number.sh` automates so the cost is one command, not manual
  renumbering.

### Bootstrapping this very record

`tools/assign-adr-number.sh` is introduced by this same PR, so it cannot yet govern the
PR that creates it — there was no prior committed tool to run against this file. This
record's number, `0027`, was therefore assigned by hand, the same way every prior ADR
was: `main` was at `0026` when this branch started, and no other decision record was
in flight concurrently. Every ADR authored after this one uses the `NNNN` placeholder
and this tool.

## Consequences

- `tools/assign-adr-number.sh` is new (`tests/tooling/test_assign_adr_number.sh`
  covers: placeholder to next-free-number, all references rewritten, refuses cleanly
  when no placeholder is present, a second run after a successful assignment is a
  safe no-op, and the #170/#171-shaped concurrent case resolves to distinct numbers).
- `docs/agent-team.md` documents the `NNNN` placeholder as the authoring convention for
  every future design-decision PR, and names this ADR as the reason.
- `tools/adr-index-check.sh` (Issue #189) is unchanged and still runs as the
  after-the-fact drift guard in `tools/orchestration-policy.sh`; this ADR's mechanism
  is what should now make that guard's failure mode — duplicate/gap — unreachable in
  the normal case, not a replacement for it.
- No new control-plane component. This is a Bash script invoked as a manual pre-merge
  step, the same trust and automation posture as every other `tools/*.sh` gate in this
  repo — consistent with the "no new orchestration machinery for now" locked decision
  in `AGENTS.md`.

## Known limitations

- The tool must actually be run before merge for the guarantee to hold — nothing
  currently blocks a merge that skips it (no CI gate invokes it; `tools/route.sh`'s
  `tooling` route is `ci`-only). If a PR is merged with an un-renamed `NNNN-slug.md`
  still in the tree, `tools/adr-index-check.sh` will catch the resulting drift (a
  non-four-digit filename fails its filename-shape check) at the next
  `orchestration-policy.sh` run, but only after the fact. Wiring this into an
  automated pre-merge check, if the manual step proves unreliable in practice, is
  future work and out of this issue's scope.
- Rewriting external cross-links (step 5) is a literal-filename search over tracked
  Markdown files under the given scan root; a reference written in prose without the
  exact old filename string (e.g. "see the new ADR about X") is not found or rewritten.
