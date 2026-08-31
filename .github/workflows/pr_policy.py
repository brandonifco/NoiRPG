#!/usr/bin/env python3
"""pr-policy — make a PR machine-readable, and reject one that is not.

Validates that the PR body actually carries the evidence the template asks for
(not just the template's prompts), derives the verification route via
tools/route.sh, and emits a small evidence artifact so agents can query PR state
instead of scraping prose.

Runs in CI from the pull_request event, or locally:

    .github/workflows/pr_policy.py \
        --body-file body.md --base <sha> --head <sha> --number 58

Exit status is non-zero when the body fails policy, so the check fails.
"""
from __future__ import annotations

import argparse
import json
import os
import re
import subprocess
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]

# Prompt text from .github/pull_request_template.md. A section whose only content
# is (a paraphrase of) its prompt counts as empty.
PROMPTS = {
    "exact behavioral claim": "what is now true, and what failure does this prevent",
    "scope": "what changed? what nearby behavior deliberately did not change",
    "rules conformance": "which section of the orc content document",
    "tests and evidence": "exact commands and results",
    "decisions and tradeoffs": "any material judgment made during implementation",
    "known limitations": "state what remains unresolved",
    "agent provenance": "which agent or model implemented",
}


def sections(body: str) -> dict[str, str]:
    """Split a markdown body into {lowercased header: content}."""
    out: dict[str, str] = {}
    cur = None
    buf: list[str] = []
    for line in body.splitlines():
        m = re.match(r"^\s{0,3}#{1,6}\s+(.*?)\s*#*\s*$", line)
        if m:
            if cur is not None:
                out[cur] = "\n".join(buf).strip()
            cur = m.group(1).strip().lower()
            buf = []
        else:
            buf.append(line)
    if cur is not None:
        out[cur] = "\n".join(buf).strip()
    return out


def is_empty(header: str, content: str) -> bool:
    c = content.strip()
    if not c:
        return True
    # Strip HTML comments and checklist scaffolding, then compare to the prompt.
    stripped = re.sub(r"<!--.*?-->", "", c, flags=re.S).strip().lower()
    if not stripped:
        return True
    prompt = PROMPTS.get(header)
    if prompt and stripped.startswith(prompt[:24]):
        return True
    return False


def run_route(base: str | None) -> dict:
    cmd = [str(ROOT / "tools" / "route.sh"), "--json"]
    if base:
        cmd += ["--base", base]
    try:
        out = subprocess.check_output(cmd, cwd=ROOT, text=True, stderr=subprocess.DEVNULL)
        return json.loads(out.strip().splitlines()[-1])
    except Exception as e:  # noqa: BLE001 — route helper is advisory here
        return {"route": "unknown", "gates": [], "architecture": False, "escalated": False, "files": [], "error": str(e)}


def changed_files(base: str | None, head: str | None) -> list[str]:
    if not base:
        return []
    rng = f"{base}...{head}" if head else base
    try:
        out = subprocess.check_output(["git", "diff", "--name-only", rng], cwd=ROOT, text=True)
        return [f for f in out.splitlines() if f]
    except Exception:  # noqa: BLE001
        return []


def validate(body: str, route: dict, files: list[str]) -> tuple[list[str], dict]:
    sec = sections(body)
    violations: list[str] = []

    # Linked issue.
    m = re.search(r"\b(clos|fix|resolv)(e|es|ed)?\s+#(\d+)", body, re.I)
    issue = int(m.group(3)) if m else None
    if issue is None:
        violations.append("Linked Issue missing — body has no `Closes #<n>` / `Fixes #<n>`.")

    def require(header: str, label: str) -> None:
        if is_empty(header, sec.get(header, "")):
            violations.append(f"{label} is empty.")

    require("exact behavioral claim", "Exact behavioral claim")
    require("scope", "Scope")
    require("agent provenance", "Agent provenance")
    require("known limitations", "Known limitations")

    # Tests and evidence must name a command, not just assert "tests pass".
    tests = sec.get("tests and evidence", "")
    if is_empty("tests and evidence", tests):
        violations.append("Tests and evidence is empty.")
    elif not (re.search(r"`[^`]+`", tests) or re.search(r"\bdotnet\b|\bpython\b|\btools/|\bgit\b", tests)):
        violations.append("Tests and evidence names no command (write the exact command, not 'tests pass').")

    # Rules changes must carry a conformance section.
    touches_rules = route.get("route") in ("rules", "formulas") or any(
        f.startswith("src/Brp.Rules/") or f.startswith("src/Brp.Core/") or f.startswith("src/Brp.Data/")
        for f in files
    )
    if touches_rules:
        rc = re.sub(r"<!--.*?-->", "", sec.get("rules conformance", ""), flags=re.S).strip()
        if is_empty("rules conformance", rc) or re.match(r"(n/?a|none)\b", rc, re.I):
            violations.append(
                "Rules files changed but Rules conformance is empty or 'N/A' — name the "
                "ORC section and the printed table verified against.")

    # Parse a test count from evidence if present (best-effort, for the artifact).
    tc = re.search(r"\b(\d{2,6})\s+tests?\b", body, re.I) or re.search(r"Passed!?\s*[-:]?\s*.*?(\d{2,6})", body)
    return violations, {"issue": issue, "tests": int(tc.group(1)) if tc else None}


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--body-file")
    ap.add_argument("--base")
    ap.add_argument("--head")
    ap.add_argument("--number", type=int)
    ap.add_argument("--out", default="evidence.json")
    args = ap.parse_args()

    body, base, head, number = args.body_file and Path(args.body_file).read_text(), args.base, args.head, args.number
    ev_path = os.environ.get("GITHUB_EVENT_PATH")
    if body is None and ev_path and Path(ev_path).exists():
        ev = json.loads(Path(ev_path).read_text())
        pr = ev.get("pull_request", {})
        body = pr.get("body") or ""
        base = base or pr.get("base", {}).get("sha")
        head = head or pr.get("head", {}).get("sha")
        number = number or pr.get("number")
    if body is None:
        print("no PR body available (pass --body-file or run in a pull_request event)", file=sys.stderr)
        return 2

    route = run_route(base)
    files = changed_files(base, head)
    violations, meta = validate(body, route, files)

    gates = route.get("gates", [])
    evidence = {
        "issue": meta["issue"] or number,
        "base": base,
        "head": head,
        "route": route.get("route"),
        "escalated": route.get("escalated", False),
        "requiredGates": gates,
        # Gate states start pending; the App (#65) / state machine (#62) fill these in.
        "gateStates": {g: ("pass" if g == "ci" else "pending") for g in gates},
        "tests": meta["tests"],
        "prPolicy": "pass" if not violations else "fail",
    }
    Path(args.out).write_text(json.dumps(evidence, indent=2) + "\n")

    # Human summary.
    summary = Path(os.environ.get("GITHUB_STEP_SUMMARY", os.devnull))
    with summary.open("a") as fh:
        fh.write(f"### pr-policy: {'PASS ✅' if not violations else 'FAIL ❌'}\n\n")
        fh.write(f"- route: `{route.get('route')}`"
                 f"{' (content-escalated)' if route.get('escalated') else ''}\n")
        fh.write(f"- required gates: {', '.join(f'`{g}`' for g in gates) or '—'}\n")
        if violations:
            fh.write("\n**Violations:**\n\n")
            for v in violations:
                fh.write(f"- {v}\n")
        fh.write("\n```json\n" + json.dumps(evidence, indent=2) + "\n```\n")

    for v in violations:
        print(f"::error title=pr-policy::{v}")
    print(json.dumps(evidence, indent=2))
    return 1 if violations else 0


if __name__ == "__main__":
    raise SystemExit(main())
