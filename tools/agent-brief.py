#!/usr/bin/env python3
"""agent-brief — assemble the minimal context packet for an Issue or a PR review.

The orchestrator's job is to hand an agent the problem, not make it rediscover
the problem. Measured cost of not doing this: on #9, scope-warden spent ~40k of
~52k tokens locating the change. This tool assembles what the repo already knows
so the agent spends its budget reasoning instead of searching.

    tools/agent-brief.py task   <issue-number>
    tools/agent-brief.py review <pr-number>
    tools/agent-brief.py review --base <ref> --head <ref> [--issue <n>]

Uses `gh` and `git`; run it where the orchestrator runs. Output is markdown, meant
to be pasted directly into an agent prompt.
"""
from __future__ import annotations

import argparse
import hashlib
import json
import re
import subprocess
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
DECISIONS = ROOT / "docs" / "decisions"

# Packet schema/hash version. Bump this whenever a section is added, removed, or
# reordered in a way that changes what a consumer can rely on being present —
# NOT on every cosmetic wording change. The hash footer this stamps is
# reproducible: it covers only the packet's semantic content, never a
# timestamp or other wall-clock value (there is deliberately none in this
# file), so two runs against identical repo/issue/PR state produce the same
# packet-sha256.
PACKET_VERSION = 1

# Foundational authority that applies to essentially all engine work.
ALWAYS_AUTHORITY = ["AGENTS.md", "orc-scope-filter.md", "docs/source-handling.md"]
ALWAYS_ADRS = ["0001", "0002", "0003"]  # source text, scope filter, determinism

GATE_REVIEW = {
    "ci": "Build and full test suite must be green.",
    "scope-warden": "Check the diff against orc-scope-filter.md and docs/source-handling.md: "
                    "no out-of-scope content, correct source, modern-era baselines, seeded randomness.",
    "rules-conformance": "Verify every implemented value against the printed table in the cited "
                         "ORC section. Assume the implementation is wrong until each row proves out.",
    "codex-conformance": "Independently RE-DERIVE each formula/threshold from the source text — do "
                         "not reuse the implementer's reasoning. Different-vendor cross-check.",
    "architecture-review": "Review the new subsystem boundary and project references: layering holds, "
                           "Brp.Core/Brp.Rules take no game-engine dependency.",
}

# The architecture-review gate lands in the gate set for two structurally
# different reasons, per tools/route.sh (the one route authority):
#   1. a path-derived project/boundary change actually present in the diff (a
#      changed .csproj, project reference, or similar) — `architecture: true`.
#   2. an issue-level `route:architecture` intent floor with NO such change in
#      the diff. route.sh only reports `issueRaised: true` off the
#      `route:architecture` floor when it had to add the gate itself (the
#      path-derived `arch` flag was 0 before applying the floor) — see
#      route.sh's "Issue-intent floor" comment. So `architecture && issueRaised
#      && issueRoute == "architecture"` means the gate exists ONLY because of
#      the issue floor, not because of anything in the diff.
# These need different reasons/checklists (F11 burn-in finding): the layering
# boilerplate is nonsensical for, e.g., a docs-only ADR that merely records a
# decision.
ARCH_REVIEW_LAYERING = (
    "Review the new subsystem boundary and project references: layering holds, "
    "Brp.Core/Brp.Rules take no game-engine dependency."
)
ARCH_REVIEW_DECISION_SCOPED = (
    "No project/boundary change is actually in this diff — this gate is present only "
    "because the Issue carries a `route:architecture` intent floor. Review the "
    "SOUNDNESS of the recorded decision or boundary the change makes (is it correct, "
    "consistent with prior decisions, and adequately justified) — do NOT check "
    "`Brp.*` layering or project references, which this diff does not touch."
)


def architecture_review_reason_and_checklist(route: dict) -> tuple[str, str] | None:
    """Return (reason, checklist text) for the architecture-review gate, or
    None if the gate is not present in `route`'s gate set. Distinguishes WHY
    the gate is present — see the comment above ARCH_REVIEW_LAYERING."""
    if not route.get("architecture"):
        return None
    issue_floor_only = bool(route.get("issueRaised")) and route.get("issueRoute") == "architecture"
    if issue_floor_only:
        return (
            "architecture review added: Issue intent floor `route:architecture` — "
            "no project/boundary change in the diff",
            ARCH_REVIEW_DECISION_SCOPED,
        )
    return (
        "architecture review added: the diff touches project references/layering",
        ARCH_REVIEW_LAYERING,
    )


def sh(cmd: list[str], check: bool = True) -> str:
    r = subprocess.run(cmd, cwd=ROOT, text=True, capture_output=True)
    if check and r.returncode != 0:
        print(f"command failed: {' '.join(cmd)}\n{r.stderr}", file=sys.stderr)
        sys.exit(1)
    return r.stdout


def gh_json(args: list[str]) -> dict | list:
    return json.loads(sh(["gh", *args]))


def split_sections(body: str) -> dict[str, str]:
    out, cur, buf = {}, None, []
    for line in (body or "").splitlines():
        m = re.match(r"^\s{0,3}#{1,6}\s+(.*?)\s*#*\s*$", line)
        if m:
            if cur is not None:
                out[cur] = "\n".join(buf).strip()
            cur, buf = m.group(1).strip().lower(), []
        else:
            buf.append(line)
    if cur is not None:
        out[cur] = "\n".join(buf).strip()
    return out


def first_of(sec: dict[str, str], *names: str) -> str:
    for n in names:
        for k, v in sec.items():
            if k == n or k.startswith(n):
                if v.strip():
                    return v.strip()
    return ""


def adr_titles() -> dict[str, tuple[str, Path]]:
    out = {}
    for f in sorted(DECISIONS.glob("0*.md")):
        num = f.name[:4]
        title = f.read_text().splitlines()[0].lstrip("# ").strip()
        out[num] = (title, f)
    return out


STOPWORDS = {
    "layer", "rules", "this", "that", "with", "from", "into", "issue", "work",
    "content", "document", "system", "keystone", "shape", "applied", "reconciling",
    "three", "four", "data", "driven", "case", "deferred", "does", "stack",
}


def relevant_adrs(text: str, cap: int = 4) -> list[str]:
    """Foundational ADRs (always) plus the top-N whose title best overlaps `text`.

    `text` should be focused (title + labels + rules source), not the whole body —
    the body shares common words with almost every ADR and defeats the point.
    """
    adrs = adr_titles()
    words = {w for w in re.findall(r"[a-z]{4,}", text.lower())} - STOPWORDS
    scored = []
    for num, (title, _) in adrs.items():
        if num in ALWAYS_ADRS:
            continue
        tw = set(re.findall(r"[a-z]{4,}", title.lower())) - STOPWORDS
        score = len(tw & words)
        if score:
            scored.append((score, num))
    scored.sort(reverse=True)
    picked = list(ALWAYS_ADRS) + [num for _, num in scored[:cap]]
    return [n for n in sorted(picked) if n in adrs]


def referenced_issues(text: str, self_num: int | None) -> list[int]:
    nums = {int(n) for n in re.findall(r"#(\d+)", text or "")}
    nums.discard(self_num or -1)
    return sorted(nums)


def dep_state(nums: list[int]) -> list[str]:
    lines = []
    for n in nums:
        r = subprocess.run(["gh", "issue", "view", str(n), "--json", "state,title"],
                           cwd=ROOT, text=True, capture_output=True)
        if r.returncode != 0:
            lines.append(f"- #{n} — (could not resolve; may be a PR)")
            continue
        d = json.loads(r.stdout)
        mark = "✅" if d["state"].lower() == "closed" else "⏳"
        lines.append(f"- #{n} {mark} {d['state'].lower()} — {d['title']}")
    return lines


def _test_dir_for(path: str) -> str | None:
    m = re.match(r"src/(Brp\.\w+)/(.+?)(?:/[^/]+)?$", path)
    if not m:
        return None
    tdir = ROOT / "tests" / f"{m.group(1)}.Tests" / m.group(2)
    return f"tests/{m.group(1)}.Tests/{m.group(2)}/" if tdir.is_dir() else None


def workspace(files_text: str, body: str) -> tuple[list[str], list[str]]:
    """Return (markdown lines, derived src paths for route prediction)."""
    lines, seen, derived = [], set(), []
    # 1. Explicit file paths named anywhere in the files section or body.
    for path in re.findall(r"[`\s(]((?:src|tests|tools|docs)/[\w./-]+\.\w+)", f"{files_text}\n{body}"):
        if path in seen:
            continue
        seen.add(path)
        mark = "exists" if (ROOT / path).exists() else "new"
        lines.append(f"- `{path}` ({mark})")
        derived.append(path)
        td = _test_dir_for(path)
        if td:
            lines.append(f"    neighbouring tests: `{td}`")
    # 2. Module tokens like `Brp.Rules.Combat` -> src/Brp.Rules/Combat/ (+ tests).
    for tok in dict.fromkeys(re.findall(r"\bBrp\.[A-Z]\w+(?:\.[A-Z]\w+)*\b", body)):
        parts = tok.split(".")
        if len(parts) < 3:
            continue
        d = "src/" + "/".join([".".join(parts[:2]), *parts[2:]])
        if d in seen or not (ROOT / d).is_dir():
            continue
        seen.add(d)
        lines.append(f"- `{d}/` (module)")
        derived.append(d)
        td = _test_dir_for(d)
        if td:
            lines.append(f"    neighbouring tests: `{td}`")
    if not lines:
        lines = ["- (none named; derive from the acceptance criteria)"]
    return lines, derived


def route_for(files: list[str], base: str | None = None, issue: int | None = None) -> dict:
    # route.sh is the one route authority: derives changed paths, content
    # escalation, and (via --issue) the issue-intent floor the same way for
    # every consumer — the review packet must never approximate this itself.
    cmd = ["bash", str(ROOT / "tools" / "route.sh"), "--json"]
    empty_diff = None
    if base:
        cmd += ["--base", base]
    elif files:
        cmd += files
    elif issue:
        # No known files and no base ref: route.sh's "neither" fallback reads
        # the local working tree's `git diff HEAD`, which has nothing to do
        # with a not-yet-started Issue and would make the prediction depend on
        # whatever happens to be dirty in this checkout. Feed it an explicit
        # empty diff instead, so the path baseline stays "tooling" and only
        # the Issue's `route:*` floor (still applied independently of the
        # file list) can raise it.
        import tempfile
        empty_diff = tempfile.NamedTemporaryFile(mode="w", suffix=".diff", delete=False)
        empty_diff.close()
        cmd += ["--diff-file", empty_diff.name]
    if issue:
        cmd += ["--issue", str(issue)]
    try:
        return json.loads(sh(cmd).strip().splitlines()[-1])
    except Exception:
        return {"route": "unknown", "gates": []}
    finally:
        if empty_diff:
            Path(empty_diff.name).unlink(missing_ok=True)


def render(schema: str, lines: list[str]) -> str:
    """Join packet lines and append a deterministic version/hash footer.

    The hash covers exactly the rendered body (schema line included) — no
    timestamp, hostname, or other nondeterministic value is ever part of it,
    so re-running the same command against the same repo/issue/PR state
    reproduces the identical `packet-sha256`.
    """
    body = "\n".join(lines).rstrip() + "\n"
    digest = hashlib.sha256(body.encode("utf-8")).hexdigest()
    footer = f"\n---\npacket-schema: {schema}/{PACKET_VERSION}\npacket-version: {PACKET_VERSION}\npacket-sha256: {digest}\n"
    return body + footer


def cmd_task(num: int) -> None:
    d = gh_json(["issue", "view", str(num), "--json", "title,body,labels,number,url"])
    body = d.get("body") or ""
    sec = split_sections(body)
    labels = " ".join(l["name"] for l in d.get("labels", []))
    adrs = adr_titles()

    files_text = first_of(sec, "likely files or subsystem", "workspace", "likely files")
    ws_lines, derived = workspace(files_text, body)
    route = route_for(derived, issue=num)

    src = first_of(sec, "rules source")
    focus = f"{d['title']} {labels} {src}"
    adr_pick = relevant_adrs(focus)

    deps = referenced_issues(first_of(sec, "dependencies", "relationship to other work", "blockers") or body, num)

    p = []
    p.append(f"# TASK BRIEF — Issue #{d['number']}: {d['title']}\n")
    p.append(f"<{d['url']}>\n")

    p.append("## TASK")
    p.append(f"**Outcome.** {first_of(sec, 'outcome') or '(see issue)'}")
    acc = first_of(sec, "acceptance criteria", "acceptance")
    if acc:
        p.append(f"\n**Acceptance criteria.**\n{acc}")
    excl = first_of(sec, "out of scope", "exclusions")
    p.append(f"\n**Explicitly out of scope.**\n{excl or '(none stated in the issue body)'}")

    p.append("\n## AUTHORITY (read these, in this order)")
    for f in ALWAYS_AUTHORITY:
        p.append(f"- `{f}`")
    for a in adr_pick:
        p.append(f"- `docs/decisions/{adrs[a][1].name}` — {adrs[a][0]}")
    if src:
        p.append(f"- **Rules source:** {src}")

    p.append("\n## DEPENDENCIES")
    p.extend(dep_state(deps) if deps else ["- (none referenced)"])

    p.append("\n## WORKSPACE")
    p.extend(ws_lines)

    p.append("\n## REQUIRED GATES (predicted from the likely files; final route is set at PR time)")
    p.append(f"- route: **{route.get('route')}** → gates: {', '.join(route.get('gates', [])) or '—'}")

    p.append("\n## DO NOT REVISIT (locked decisions)")
    for a in adr_pick:
        p.append(f"- {adrs[a][0]} (`{adrs[a][1].name}`)")

    p.append("\n## KNOWN DEAD ENDS")
    dead = first_of(sec, "known dead ends", "dead ends")
    p.append(dead if dead else "(none recorded in the issue; if you hit one, note it in the PR for the next agent.)")

    sys.stdout.write(render("task-packet", p))


def cmd_review(args: argparse.Namespace) -> None:
    if args.pr is not None:
        pr = gh_json(["pr", "view", str(args.pr), "--json",
                      "title,body,number,baseRefName,headRefName,headRefOid,url"])
        head = pr["headRefOid"]
        sh(["git", "fetch", "--no-tags", "-q", "origin", f"refs/pull/{pr['number']}/head", pr["baseRefName"]], check=False)
        base = sh(["git", "merge-base", f"origin/{pr['baseRefName']}", head], check=False).strip() or f"origin/{pr['baseRefName']}"
        title, body, url = pr["title"], pr.get("body") or "", pr["url"]
        issue_ref = referenced_issues(body, pr["number"])
        issue = issue_ref[0] if issue_ref else args.issue
    else:
        base, head = args.base, args.head
        sh(["git", "fetch", "--no-tags", "-q", "origin", base, head], check=False)
        title, body, url, issue = "(explicit range)", "", "", args.issue

    names = [f for f in sh(["git", "diff", "--name-only", f"{base}...{head}"]).splitlines() if f]
    diff = sh(["git", "diff", "-U1", f"{base}...{head}"])
    route = route_for(names, base=base, issue=issue)

    sec = split_sections(body)
    claim = first_of(sec, "exact behavioral claim", "behavioral claim") or "(none stated)"

    focus = f"{title} {sec.get('rules source', '')}"
    adrs = adr_titles()
    adr_pick = relevant_adrs(focus) if title != "(explicit range)" else list(ALWAYS_ADRS)

    arch_review = architecture_review_reason_and_checklist(route)

    reasons = []
    if route.get("escalated"):
        reasons.append("content escalation: the diff touches a numeric table/threshold, "
                        "so `rules` was promoted to `formulas`")
    if route.get("issueRaised") and route.get("issueRoute") != "architecture":
        reasons.append(f"issue-intent floor: Issue #{issue} carries `route:{route.get('issueRoute')}`, "
                        "which raises (never lowers) the route")
    if arch_review:
        reasons.append(arch_review[0])
    escalation_reason = "; ".join(reasons) if reasons else "none — plain path-based route, no escalation"

    p = []
    p.append(f"# REVIEW BRIEF — {title}")
    p.append(f"\n## PR")
    if args.pr is not None:
        p.append(f"- PR: #{args.pr}")
    else:
        p.append("- PR: (explicit range, no PR)")
    if url:
        p.append(f"- <{url}>")

    p.append("\n## ISSUE")
    if issue:
        p.append(f"- Closes/related: #{issue}")
        p.append(f"- Source task packet: `tools/agent-brief.py task {issue}`")
    else:
        p.append("- (none referenced — no `Closes #<n>` found; not applicable)")

    p.append(f"\n## RANGE\n- base `{base}`\n- head `{head}`")

    p.append("\n## CHANGED FILES")
    p.extend([f"- `{f}`" for f in names] or ["- (none)"])

    p.append("\n## AUTHORITY (read these, in this order)")
    for f in ALWAYS_AUTHORITY:
        p.append(f"- `{f}`")
    for a in adr_pick:
        if a in adrs:
            p.append(f"- `docs/decisions/{adrs[a][1].name}` — {adrs[a][0]}")

    p.append("\n## IMPLEMENTER CLAIM (verify — do not assume true)")
    p.append(claim)

    p.append(f"\n## REQUIRED REVIEW — route **{route.get('route')}**"
             + (" (content-escalated)" if route.get("escalated") else ""))
    p.append("Checklist — every listed gate must return a verdict before this PR can merge:")
    for g in route.get("gates", []):
        if g == "architecture-review" and arch_review:
            p.append(f"- [ ] **{g}** — {arch_review[1]}")
        elif g in GATE_REVIEW:
            p.append(f"- [ ] **{g}** — {GATE_REVIEW[g]}")
        else:
            p.append(f"- [ ] **{g}**")

    p.append("\n## ESCALATION REASON")
    p.append(escalation_reason)

    p.append("\n## DIFF (`git diff -U1`)")
    p.append("```diff")
    p.append(diff.rstrip())
    p.append("```")

    sys.stdout.write(render("review-packet", p))


def main() -> int:
    ap = argparse.ArgumentParser(description="Assemble an agent context/review packet.")
    sub = ap.add_subparsers(dest="mode", required=True)

    t = sub.add_parser("task", help="context packet for an Issue")
    t.add_argument("issue", type=int)

    r = sub.add_parser("review", help="review packet for a PR or an explicit range")
    r.add_argument("pr", type=int, nargs="?")
    r.add_argument("--base")
    r.add_argument("--head")
    r.add_argument("--issue", type=int)

    args = ap.parse_args()
    if args.mode == "task":
        cmd_task(args.issue)
    else:
        if args.pr is None and not (args.base and args.head):
            ap.error("review needs a PR number, or --base and --head")
        cmd_review(args)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
