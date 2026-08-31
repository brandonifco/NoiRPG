#!/usr/bin/env python3
"""route-gates — the routing state machine (phase 1: evidence verification).

On a PR (and whenever a check-run lands), derive the verification route via
tools/route.sh, label the PR with it, and upsert a single `gates-satisfied`
check-run that is green only when every required non-CI gate has a passing
check-run on the PR head commit. The individual gate results are posted by the
orchestrator through the App (#65); this job only aggregates them.

Runs in Actions with the default GITHUB_TOKEN (checks: write, pull-requests:
write). Pure-stdlib. The evaluate/route helpers are unit-tested offline.
"""
from __future__ import annotations

import json
import os
import subprocess
import sys
import urllib.error
import urllib.request

API = os.environ.get("GITHUB_API_URL", "https://api.github.com")
GATE_CHECK_NAME = "gates-satisfied"


def api(method: str, path: str, token: str, body: dict | None = None) -> dict:
    url = path if path.startswith("http") else f"{API}{path}"
    data = json.dumps(body).encode() if body is not None else None
    req = urllib.request.Request(url, data=data, method=method)
    req.add_header("Authorization", f"Bearer {token}")
    req.add_header("Accept", "application/vnd.github+json")
    req.add_header("X-GitHub-Api-Version", "2022-11-28")
    req.add_header("User-Agent", "noirpg-route-gates")
    try:
        with urllib.request.urlopen(req) as resp:
            raw = resp.read()
            return json.loads(raw) if raw else {}
    except urllib.error.HTTPError as e:
        print(f"::warning::{method} {url} -> {e.code}: {e.read().decode()[:200]}")
        raise


def latest_by_name(check_runs: list[dict]) -> dict[str, dict]:
    """Keep the most recent check-run per name."""
    out: dict[str, dict] = {}
    for r in check_runs:
        prev = out.get(r["name"])
        if prev is None or (r.get("started_at") or "") >= (prev.get("started_at") or ""):
            out[r["name"]] = r
    return out


def evaluate(required_gates: list[str], latest: dict[str, dict]) -> tuple[str | None, str, list[str]]:
    """Return (conclusion|None, title, outstanding/failed detail lines).

    conclusion None means still in progress. 'ci' is excluded — build-and-test is
    a separately-required status check.
    """
    gate_names = [g for g in required_gates if g != "ci"]
    outstanding, failed = [], []
    for g in gate_names:
        r = latest.get(g)
        if r is None or r.get("status") != "completed":
            outstanding.append(g)
        elif r.get("conclusion") not in ("success", "neutral"):
            failed.append(g)
    if failed:
        return "failure", f"{len(failed)} gate(s) failed", [f"Failed: {', '.join(failed)}"]
    if outstanding:
        return None, f"Waiting on {len(outstanding)} gate(s)", [f"Pending: {', '.join(outstanding)}"]
    return "success", "All required gates satisfied", []


def run_route(base: str) -> dict:
    out = subprocess.check_output(["bash", "tools/route.sh", "--base", base, "--json"], text=True)
    return json.loads(out.strip().splitlines()[-1])


def main() -> int:
    token = os.environ["GITHUB_TOKEN"]
    repo = os.environ["GITHUB_REPOSITORY"]
    event_name = os.environ.get("GITHUB_EVENT_NAME", "")
    ev = json.loads(open(os.environ["GITHUB_EVENT_PATH"]).read())

    # Never react to our own check-run (avoids an event loop).
    if event_name == "check_run" and ev.get("check_run", {}).get("name") == GATE_CHECK_NAME:
        print("own check_run event; skipping")
        return 0

    # Resolve the PR + head/base shas.
    if event_name == "pull_request":
        pr = ev["pull_request"]
        pr_number, head_sha, base_sha = pr["number"], pr["head"]["sha"], pr["base"]["sha"]
    elif event_name == "check_run":
        head_sha = ev["check_run"]["head_sha"]
        prs = ev["check_run"].get("pull_requests") or []
        if prs:
            pr_number = prs[0]["number"]
        else:
            assoc = api("GET", f"/repos/{repo}/commits/{head_sha}/pulls", token)
            if not assoc:
                print("no PR associated with commit; skipping")
                return 0
            pr_number = assoc[0]["number"]
        pr = api("GET", f"/repos/{repo}/pulls/{pr_number}", token)
        base_sha, head_sha = pr["base"]["sha"], pr["head"]["sha"]
    else:
        print(f"unsupported event {event_name}; skipping")
        return 0

    # Fetch both endpoints and classify.
    subprocess.run(["git", "fetch", "--no-tags", "origin", base_sha, head_sha], check=False)
    subprocess.run(["git", "checkout", "-q", head_sha], check=False)
    route = run_route(base_sha)
    gates = route.get("gates", [])

    # Apply the route:<x> label; remove any other route:* labels.
    want = f"route:{route.get('route')}"
    cur = api("GET", f"/repos/{repo}/issues/{pr_number}/labels", token)
    have = {l["name"] for l in cur}
    for name in have:
        if name.startswith("route:") and name != want:
            try:
                api("DELETE", f"/repos/{repo}/issues/{pr_number}/labels/{name}", token)
            except urllib.error.HTTPError:
                pass
    if route.get("route") and want not in have:
        try:
            api("POST", f"/repos/{repo}/issues/{pr_number}/labels", token, {"labels": [want]})
        except urllib.error.HTTPError:
            pass

    # Evaluate posted gate check-runs on the head commit.
    runs = api("GET", f"/repos/{repo}/commits/{head_sha}/check-runs?per_page=100", token)
    latest = latest_by_name(runs.get("check_runs", []))
    conclusion, title, detail = evaluate(gates, latest)

    summary = [
        f"Route: `{route.get('route')}`" + (" (content-escalated)" if route.get("escalated") else ""),
        f"Required gates: {', '.join('`' + g + '`' for g in gates) or '—'}",
        *detail,
        "",
        "_Individual gate results are posted by the orchestrator via the gate-poster App (#65)._",
    ]
    body = {
        "name": GATE_CHECK_NAME,
        "head_sha": head_sha,
        "output": {"title": title, "summary": "\n".join(summary)},
    }
    if conclusion:
        body["status"], body["conclusion"] = "completed", conclusion
    else:
        body["status"] = "in_progress"

    existing = latest.get(GATE_CHECK_NAME)
    if existing:
        api("PATCH", f"/repos/{repo}/check-runs/{existing['id']}", token, body)
    else:
        api("POST", f"/repos/{repo}/check-runs", token, body)

    print(f"{GATE_CHECK_NAME}: {conclusion or 'in_progress'} — {title} (route={route.get('route')})")
    return 0


if __name__ == "__main__":
    sys.exit(main())
