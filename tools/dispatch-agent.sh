#!/usr/bin/env bash
# tools/dispatch-agent.sh — make worktree isolation a RAIL, not a convention.
#
# Burn-in F12 (docs/orchestration/agent-verification-burn-in.md): a dispatched
# implementer/writeup agent created its feature branch in the PRIMARY checkout
# and left it stranded there. The fix documented at the time was a sentence in
# docs/agent-team.md — "give the agent its own worktree" — which a forgotten
# option or a future orchestrator regression can silently violate.
#
# This tool turns that convention into two mechanical rails:
#
#   1. `<issue#>`         — the ONE way to stand up an implementer workspace:
#                           derives a branch off origin/main, creates (or
#                           re-verifies) a dedicated git worktree under
#                           .worktrees/, and prints its absolute path + branch
#                           for the orchestrator to pass as the agent's cwd. It
#                           never checks the feature branch out into the primary
#                           tree, so the F12 failure cannot happen by using it.
#
#   2. `--assert-isolated` — the self-check an implementer runs as its FIRST
#                           action: exit non-zero if the cwd is the primary
#                           worktree, zero if it is a linked one. Detection is
#                           mechanical (git's own worktree metadata), never a
#                           guess based on the path string. A dispatched agent
#                           that finds itself un-isolated stops instead of
#                           writing into the primary tree.
#
#   3. `--cleanup <issue#>` — remove the worktree after merge, only if clean.
#
# What this deliberately does NOT do: it cannot intercept the harness's Agent
# tool (a shell script has no way to), so it is a preflight + self-check, not an
# execution sandbox. That is the whole ask from the review — "a small dispatch
# wrapper/preflight is enough" — no daemon, scheduler, or service.
#
# Usage:
#   tools/dispatch-agent.sh <issue#>            # create/verify + print path,branch
#   tools/dispatch-agent.sh --assert-isolated   # exit 1 in the primary worktree
#   tools/dispatch-agent.sh --cleanup <issue#>  # remove the worktree if clean
#   tools/dispatch-agent.sh --print-path <issue#>  # just the worktree path (no create)
#
# Env:
#   BASE_REF      base to branch from (default: origin/main)
#   WORKTREE_DIR  container dir for worktrees (default: <mainroot>/.worktrees)
set -euo pipefail

die() { echo "dispatch-agent: $*" >&2; exit 2; }

command -v git >/dev/null 2>&1 || die "git not found"
git rev-parse --is-inside-work-tree >/dev/null 2>&1 || die "not inside a git repository"

# --- primary vs linked worktree (mechanical, not path-string heuristics) ----- #
# In the PRIMARY worktree, the absolute git dir IS the common git dir. In a
# LINKED worktree, the git dir is <common>/worktrees/<name>, so the two differ.
abs() { (cd "$1" 2>/dev/null && pwd) || return 1; }
git_dir_abs="$(git rev-parse --absolute-git-dir)"
common_dir_abs="$(abs "$(git rev-parse --git-common-dir)")" \
  || die "cannot resolve --git-common-dir"

is_primary_worktree() { [ "$git_dir_abs" = "$common_dir_abs" ]; }

# --- --assert-isolated: the implementer's first-action self-check ------------ #
if [ "${1:-}" = "--assert-isolated" ]; then
  [ $# -eq 1 ] || die "--assert-isolated takes no other arguments"
  if is_primary_worktree; then
    cat >&2 <<EOF
dispatch-agent: NOT ISOLATED — this is the PRIMARY checkout ($(git rev-parse --show-toplevel)).
An implementer/writeup agent must run in a dedicated worktree so it can never
strand the primary tree on a feature branch (burn-in F12). Stop and report a
dispatch error; the orchestrator should create your workspace with:
    tools/dispatch-agent.sh <issue#>
and dispatch you with that path as your working directory.
EOF
    exit 1
  fi
  echo "dispatch-agent: isolated worktree OK ($(git rev-parse --show-toplevel))"
  exit 0
fi

# Everything below operates on the MAIN worktree regardless of where we are run
# from, so the tool works when invoked from the primary checkout (the normal
# case) or from another worktree.
main_root="$(git worktree list --porcelain | awk '/^worktree /{print $2; exit}')"
[ -n "$main_root" ] || die "cannot determine the main worktree root"

BASE_REF="${BASE_REF:-origin/main}"
WORKTREE_DIR="${WORKTREE_DIR:-$main_root/.worktrees}"

slugify() {
  printf '%s' "$1" \
    | tr '[:upper:]' '[:lower:]' \
    | sed -E 's/[^a-z0-9]+/-/g; s/^-+//; s/-+$//' \
    | cut -c1-40 | sed -E 's/-+$//'
}

# Branch name for an issue: issue-<n>-<slug-of-title> (slug best-effort via gh).
branch_for_issue() {
  local n="$1" title="" slug=""
  if command -v gh >/dev/null 2>&1; then
    title="$(gh issue view "$n" --json title --jq '.title' 2>/dev/null || true)"
  fi
  slug="$(slugify "$title")"
  if [ -n "$slug" ]; then echo "issue-${n}-${slug}"; else echo "issue-${n}"; fi
}

worktree_path_for_issue() { echo "$WORKTREE_DIR/issue-$1"; }

# --- --cleanup <issue#> ------------------------------------------------------ #
if [ "${1:-}" = "--cleanup" ]; then
  [ $# -eq 2 ] || die "usage: --cleanup <issue#>"
  n="$2"; [[ "$n" =~ ^[0-9]+$ ]] || die "issue number must be numeric: $n"
  wt="$(worktree_path_for_issue "$n")"
  if ! git -C "$main_root" worktree list --porcelain | grep -qx "worktree $wt"; then
    echo "dispatch-agent: no worktree registered at $wt (nothing to clean up)"
    exit 0
  fi
  # `git worktree remove` refuses a dirty worktree unless --force; we never
  # force, so uncommitted work is preserved and the caller is told.
  if ! git -C "$main_root" worktree remove "$wt" 2>/dev/null; then
    die "worktree at $wt is not clean (uncommitted changes) — commit/push or discard first; refusing to --force"
  fi
  echo "dispatch-agent: removed worktree $wt"
  exit 0
fi

# --- --print-path <issue#> (no side effects) --------------------------------- #
PRINT_ONLY=0
if [ "${1:-}" = "--print-path" ]; then PRINT_ONLY=1; shift; fi

# --- <issue#>: create or verify the dedicated worktree ----------------------- #
[ $# -eq 1 ] || die "usage: tools/dispatch-agent.sh <issue#> | --assert-isolated | --cleanup <issue#> | --print-path <issue#>"
case "$1" in -*) die "unknown option: $1" ;; esac
n="$1"; [[ "$n" =~ ^[0-9]+$ ]] || die "issue number must be numeric: $n"

wt="$(worktree_path_for_issue "$n")"

if [ "$PRINT_ONLY" = 1 ]; then
  echo "$wt"
  exit 0
fi

branch="$(branch_for_issue "$n")"

# Refuse to operate in-place: the target worktree must never be the primary
# checkout (this is what would recreate F12).
if [ "$(abs "$wt" 2>/dev/null || echo "$wt")" = "$(abs "$main_root")" ]; then
  die "refusing to use the primary checkout ($main_root) as an implementer worktree"
fi

git -C "$main_root" fetch --no-tags -q origin "${BASE_REF#origin/}" 2>/dev/null || true

# Already registered? Verify it points at our branch, then reuse it.
if git -C "$main_root" worktree list --porcelain | grep -qx "worktree $(abs "$wt" 2>/dev/null || echo "$wt")" \
   || git -C "$main_root" worktree list --porcelain | grep -qx "worktree $wt"; then
  cur="$(git -C "$wt" rev-parse --abbrev-ref HEAD 2>/dev/null || echo '?')"
  if [ "$cur" != "$branch" ]; then
    die "worktree $wt already exists on branch '$cur', not '$branch' — resolve manually (or --cleanup $n first)"
  fi
  echo "dispatch-agent: reusing worktree"
else
  mkdir -p "$WORKTREE_DIR"
  if git -C "$main_root" show-ref --verify --quiet "refs/heads/$branch"; then
    git -C "$main_root" worktree add "$wt" "$branch" >&2
  else
    git -C "$main_root" worktree add -b "$branch" "$wt" "$BASE_REF" >&2
  fi
fi

# Machine-parseable last two lines: absolute path, then branch.
echo "path=$(abs "$wt")"
echo "branch=$branch"
