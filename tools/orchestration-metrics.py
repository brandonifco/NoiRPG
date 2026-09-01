#!/usr/bin/env python3
"""orchestration-metrics — measure the orchestration system, not the code.

The optimization target this repository's "GitHub as orchestration engine" epic
(#53) is chasing is **minimum human attention per correctly-merged unit of
capability** — not lines of code, not raw PR count. This tool computes what is
observable toward that target from three sources:

  1. GitHub, via `gh` (merged PRs, CI runs, gate check-runs, issue timelines);
  2. the agent-team ledger (`docs/agent-team-ledger/*.csv`) — token cost per job;
  3. an optional human-minutes log (`docs/agent-team-ledger/human-minutes.csv`).

It prints a markdown report by default, or the same data as JSON with `--json`.

Design rule: never fabricate precision. Every metric that is a heuristic says so
in its own line; every metric whose data is not reliably available prints with an
explicit "(not tracked / needs manual input)" note instead of a made-up number.
The tool degrades gracefully — a missing CSV or an empty `gh` response produces a
note, never a crash.

Dependency-light: Python stdlib + the `gh` and `git` binaries. No pip packages.

Usage:
    tools/orchestration-metrics.py [--limit N] [--since YYYY-MM-DD] [--json]

    --limit N        how many recent merged PRs to inspect (default 20)
    --since DATE     only PRs merged on/after DATE (YYYY-MM-DD)
    --json           emit the computed data as JSON instead of markdown

See docs/orchestration/metrics.md for what each metric means and its data source.
"""
from __future__ import annotations

import argparse
import csv
import json
import re
import statistics
import subprocess
import sys
from datetime import datetime, timezone
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
LEDGER = ROOT / "docs" / "agent-team-ledger"
JOBS_CSV = LEDGER / "jobs.csv"
FINDINGS_CSV = LEDGER / "findings.csv"
HUMAN_MINUTES_CSV = LEDGER / "human-minutes.csv"

# Gate names posted as check-runs by the orchestrator (docs/orchestration/routing.md).
GATE_NAMES = ("scope-warden", "rules-conformance", "codex-conformance", "architecture-review")
CI_CHECK_NAME = "build-and-test"  # the check-run name CI publishes; `ci` gate maps to this
CI_WORKFLOW = "ci.yml"
NOT_TRACKED = "(not tracked / needs manual input)"


# --------------------------------------------------------------------------- #
# Shelling out — always graceful, never fatal.
# --------------------------------------------------------------------------- #
def _run(cmd: list[str]) -> str | None:
    """Run a command, return stdout, or None on any failure."""
    try:
        out = subprocess.run(cmd, cwd=ROOT, text=True, capture_output=True, timeout=90)
    except Exception:  # noqa: BLE001 — a missing/blocked binary must not crash the report
        return None
    if out.returncode != 0:
        return None
    return out.stdout


def gh_json(args: list[str]):
    """Run `gh <args>` expecting JSON on stdout; return parsed value or None."""
    raw = _run(["gh", *args])
    if raw is None:
        return None
    raw = raw.strip()
    if not raw:
        return None
    try:
        return json.loads(raw)
    except json.JSONDecodeError:
        return None


def gh_available() -> bool:
    return _run(["gh", "auth", "status"]) is not None or _run(["gh", "--version"]) is not None


def repo_slug() -> str | None:
    data = gh_json(["repo", "view", "--json", "nameWithOwner"])
    if isinstance(data, dict):
        return data.get("nameWithOwner")
    return None


# --------------------------------------------------------------------------- #
# Small helpers.
# --------------------------------------------------------------------------- #
def parse_iso(s: str | None) -> datetime | None:
    if not s:
        return None
    try:
        return datetime.fromisoformat(s.replace("Z", "+00:00"))
    except ValueError:
        return None


def fmt_hours(hours: float) -> str:
    if hours < 1:
        return f"{hours * 60:.0f} min"
    if hours < 48:
        return f"{hours:.1f} h"
    return f"{hours / 24:.1f} d"


def summarize(values: list[float]) -> dict | None:
    """Descriptive stats for a list of numbers, or None if empty."""
    if not values:
        return None
    return {
        "n": len(values),
        "min": min(values),
        "max": max(values),
        "mean": statistics.fmean(values),
        "median": statistics.median(values),
    }


def closing_issue(pr: dict) -> int | None:
    """Best-effort: the issue a PR closes, from body keywords then the title tail."""
    body = pr.get("body") or ""
    m = re.search(r"\b(?:clos(?:e|es|ed)?|fix(?:es|ed)?|resolv(?:e|es|ed)?)\s+#(\d+)", body, re.I)
    if m:
        return int(m.group(1))
    # Convention in this repo: PR titles end with "(#<issue>)".
    m = re.search(r"\(#(\d+)\)\s*$", pr.get("title") or "")
    return int(m.group(1)) if m else None


# --------------------------------------------------------------------------- #
# GitHub-sourced metric groups.
# --------------------------------------------------------------------------- #
def fetch_merged_prs(limit: int, since: datetime | None) -> list[dict] | None:
    prs = gh_json([
        "pr", "list", "--state", "merged", "--limit", str(limit),
        "--json", "number,title,body,createdAt,mergedAt,headRefName,labels,reviews",
    ])
    if prs is None:
        return None
    if since is not None:
        out = []
        for p in prs:
            merged = parse_iso(p.get("mergedAt"))
            if merged is not None and merged >= since:
                out.append(p)
        return out
    return prs


def cycle_time_metrics(prs: list[dict]) -> dict:
    """PR opened -> merged, in hours. Directly observable and exact."""
    hours: list[float] = []
    rows = []
    for p in prs:
        created = parse_iso(p.get("createdAt"))
        merged = parse_iso(p.get("mergedAt"))
        if created and merged:
            h = (merged - created).total_seconds() / 3600.0
            hours.append(h)
            rows.append({"number": p["number"], "hours": h})
    return {"stats": summarize(hours), "per_pr": rows}


def ready_to_pr_metrics(prs: list[dict], slug: str | None) -> dict:
    """Issue READY -> PR-opened, in hours. APPROXIMATE: depends on the `ready`
    label's timeline event on the closing issue, which is only present if the
    issue was labeled `ready` before the PR was opened."""
    if not slug:
        return {"available": False, "note": "no repo slug; skipped", "per_pr": [], "stats": None}
    hours: list[float] = []
    rows = []
    skipped = 0
    for p in prs:
        issue = closing_issue(p)
        pr_created = parse_iso(p.get("createdAt"))
        if issue is None or pr_created is None:
            skipped += 1
            continue
        timeline = gh_json(["api", f"repos/{slug}/issues/{issue}/timeline", "--paginate"])
        if not isinstance(timeline, list):
            skipped += 1
            continue
        ready_at = None
        for ev in timeline:
            if ev.get("event") == "labeled" and (ev.get("label") or {}).get("name") == "ready":
                t = parse_iso(ev.get("created_at"))
                if t and t <= pr_created and (ready_at is None or t > ready_at):
                    ready_at = t  # last `ready` labeling at or before PR open
        if ready_at is None:
            skipped += 1
            continue
        h = (pr_created - ready_at).total_seconds() / 3600.0
        hours.append(h)
        rows.append({"number": p["number"], "issue": issue, "hours": h})
    return {
        "available": bool(hours),
        "approximate": True,
        "skipped": skipped,
        "note": "approximate: needs a `ready` label event on the closing issue before PR open",
        "stats": summarize(hours),
        "per_pr": rows,
    }


def ci_run_metrics(limit: int, since: datetime | None) -> dict:
    """CI failure rate over recent runs, from `gh run list`. Exact for the window."""
    runs = gh_json([
        "run", "list", "--workflow", CI_WORKFLOW, "--limit", str(max(limit * 5, 50)),
        "--json", "conclusion,status,headBranch,headSha,event,createdAt",
    ])
    if runs is None:
        return {"available": False, "note": f"`gh run list --workflow {CI_WORKFLOW}` returned nothing"}
    if since is not None:
        runs = [r for r in runs if (parse_iso(r.get("createdAt")) or datetime.max.replace(tzinfo=timezone.utc)) >= since]
    completed = [r for r in runs if r.get("status") == "completed"]
    failed = [r for r in completed if r.get("conclusion") == "failure"]
    return {
        "available": True,
        "total_runs": len(completed),
        "failed_runs": len(failed),
        "failure_rate": (len(failed) / len(completed)) if completed else None,
        "runs": runs,
    }


def first_try_metrics(prs: list[dict], ci: dict) -> dict:
    """First-try merge vs needed-correction, HEURISTIC.

    Heuristic: a PR "needed correction" if its head branch had at least one
    FAILED CI (build-and-test) run before merge, OR it carried >=1 review. This
    is a proxy for rework, not a measurement of it; a green-first PR that was
    corrected via force-push before its first CI run will read as first-try."""
    runs = ci.get("runs") if ci.get("available") else None
    fails_by_branch: dict[str, int] = {}
    if runs:
        for r in runs:
            if r.get("conclusion") == "failure":
                fails_by_branch[r.get("headBranch") or ""] = fails_by_branch.get(r.get("headBranch") or "", 0) + 1
    first_try = 0
    needed_correction = 0
    undetermined = 0
    rows = []
    ci_known = runs is not None
    for p in prs:
        branch = p.get("headRefName") or ""
        reviews = p.get("reviews") or []
        ci_fail = fails_by_branch.get(branch, 0)
        corrected = ci_fail > 0 or len(reviews) > 0
        # If we have no CI runs at all for this branch and no reviews, we can't
        # distinguish first-try from "runs aged out of the window".
        seen_ci = any((r.get("headBranch") == branch) for r in (runs or []))
        if not ci_known:
            undetermined += 1
            state = "undetermined (no CI data)"
        elif corrected:
            needed_correction += 1
            state = "needed-correction"
        elif not seen_ci:
            undetermined += 1
            state = "undetermined (no CI run in window)"
        else:
            first_try += 1
            state = "first-try"
        rows.append({"number": p["number"], "ci_failures_on_branch": ci_fail,
                     "reviews": len(reviews), "state": state})
    return {
        "heuristic": True,
        "note": "HEURISTIC — failed build-and-test run on the branch OR >=1 review = needed-correction",
        "first_try": first_try,
        "needed_correction": needed_correction,
        "undetermined": undetermined,
        "per_pr": rows,
    }


def gate_catch_metrics(prs: list[dict], slug: str | None) -> dict:
    """HISTORICAL-COMPAT ONLY — do not read this as describing current enforcement.

    Gate catches — count `failure` conclusions for gate check-runs on PR heads.
    This reads PRE-#90/#91 per-gate check-runs (scope-warden / rules-conformance /
    codex-conformance / architecture-review posted individually by the removed
    gate-poster App). That system is gone; nothing posts these check-runs anymore,
    so on any current PR this reads zero — and that zero is an artifact of the
    system's removal, NOT a measurement that verification caught nothing. Retained
    only so a PR merged before #90/#91 can still be inspected, and clearly labeled
    so a reader never mistakes an absent old check-run for "0 gate catches" under
    the CURRENT architecture. For the current architecture, see
    `agent_verification_metrics()` below, which reads the single
    `agent-verification` commit status `tools/agent-verify.sh` posts (#131) plus
    the canonical per-gate evidence block it writes into the PR body (#136)."""
    if not slug:
        return {"available": False, "note": "no repo slug; skipped"}
    catches = {g: 0 for g in GATE_NAMES}
    seen = {g: 0 for g in GATE_NAMES}
    any_gate_runs = False
    inspected = 0
    for p in prs:
        head = gh_json(["pr", "view", str(p["number"]), "--json", "headRefOid"])
        sha = head.get("headRefOid") if isinstance(head, dict) else None
        if not sha:
            continue
        cr = gh_json(["api", f"repos/{slug}/commits/{sha}/check-runs", "--paginate",
                      "--jq", ".check_runs[] | {name, conclusion}"])
        # --jq with --paginate yields NDJSON, not a JSON array; parse defensively.
        runs = _parse_ndjson_or_list(cr)
        if runs is None:
            raw = _run(["gh", "api", f"repos/{slug}/commits/{sha}/check-runs"])
            data = json.loads(raw) if raw else {}
            runs = data.get("check_runs", []) if isinstance(data, dict) else []
        inspected += 1
        for run in runs:
            name = run.get("name")
            if name in catches:
                any_gate_runs = True
                seen[name] += 1
                if run.get("conclusion") == "failure":
                    catches[name] += 1
    return {
        "available": True,
        "prs_inspected": inspected,
        "gate_check_runs_present": any_gate_runs,
        "catches": catches,
        "seen": seen,
        "note": (None if any_gate_runs else
                 "no gate check-runs found on inspected PR heads — the verification-gate "
                 "system was removed in #90/#91, so none are produced; catches read 0"),
    }


def _parse_ndjson_or_list(raw):
    if raw is None:
        return None
    if isinstance(raw, list):
        return raw
    if isinstance(raw, dict):
        return [raw]
    return None


# --------------------------------------------------------------------------- #
# CURRENT architecture: the single `agent-verification` commit status
# (tools/agent-verify.sh, #131) plus the canonical per-gate evidence block it
# writes into the PR body (#136). This is what actually gates merges today.
# --------------------------------------------------------------------------- #
EVIDENCE_BLOCK_RE = re.compile(
    r"<!--\s*agent-verification:start\s*-->(.*?)<!--\s*agent-verification:end\s*-->", re.S)
GATE_ROW_RE = re.compile(r"^\|\s*`([^`]+)`\s*\|\s*([a-z]+)\s*\|\s*$", re.M)
ROUTE_IN_EVIDENCE_RE = re.compile(r"\[route:\s*([^\]]+)\]")


def parse_evidence_block(body: str | None) -> dict | None:
    """Parse the canonical per-gate evidence block agent-verify.sh --evidence
    writes into the PR body (schema: docs/orchestration/agent-verification.md).
    Returns {"gates": {name: state}, "route": str|None} or None if absent."""
    m = EVIDENCE_BLOCK_RE.search(body or "")
    if not m:
        return None
    block = m.group(1)
    gates = {name: state for name, state in GATE_ROW_RE.findall(block)}
    if not gates:
        return None
    rm = ROUTE_IN_EVIDENCE_RE.search(block)
    return {"gates": gates, "route": rm.group(1) if rm else None}


def agent_verification_metrics(prs: list[dict], slug: str | None) -> dict:
    """The CURRENT enforcement architecture: read the `agent-verification` commit
    status on each PR's head SHA (posted by tools/agent-verify.sh --post, #131),
    and the canonical per-gate evidence block from the PR body (#136) where the
    PR carries one. A PR with neither predates #131/#136, or never had
    agent-verify.sh run on it — that is reported as "no evidence", never folded
    into a "0 gate catches" claim the way the removed check-run system was."""
    if not slug:
        return {"available": False, "note": "no repo slug; skipped"}
    inspected = 0
    with_status = 0
    with_evidence = 0
    status_states: dict[str, int] = {}
    route_counts: dict[str, int] = {}
    gate_seen: dict[str, int] = {}
    gate_pass: dict[str, int] = {}
    rows = []
    for p in prs:
        inspected += 1
        head = gh_json(["pr", "view", str(p["number"]), "--json", "headRefOid"])
        sha = head.get("headRefOid") if isinstance(head, dict) else None
        state = None
        if sha:
            status = gh_json(["api", f"repos/{slug}/commits/{sha}/status"])
            if isinstance(status, dict):
                for s in status.get("statuses", []):
                    if s.get("context") == "agent-verification":
                        state = s.get("state")
                        break
        if state:
            with_status += 1
            status_states[state] = status_states.get(state, 0) + 1
        evidence = parse_evidence_block(p.get("body"))
        route = None
        if evidence:
            with_evidence += 1
            route = evidence.get("route")
            if route:
                route_counts[route] = route_counts.get(route, 0) + 1
            for g, st in evidence["gates"].items():
                gate_seen[g] = gate_seen.get(g, 0) + 1
                if st == "pass":
                    gate_pass[g] = gate_pass.get(g, 0) + 1
        rows.append({
            "number": p["number"], "head_sha": sha, "status_state": state,
            "evidence_gates": evidence["gates"] if evidence else None,
            "route": route,
        })
    return {
        "available": True,
        "prs_inspected": inspected,
        "prs_with_status": with_status,
        "prs_with_evidence": with_evidence,
        "status_states": status_states,
        "route_distribution": route_counts,
        "gate_seen": gate_seen,
        "gate_pass": gate_pass,
        "per_pr": rows,
        "note": ("reads the CURRENT architecture: the single `agent-verification` "
                 "commit status (#131) and the canonical per-gate evidence block in "
                 "the PR body (#136); a PR with neither predates that machinery — "
                 "it is not evidence of a verification failure"),
    }


def review_scope_metrics(av: dict) -> dict:
    """% of PRs needing semantic AI review (evidence's gate set includes at least
    one gate beyond `ci`) vs % with zero AI verification beyond implementation
    (evidence present but only `ci` was required), and the Codex invocation rate
    (fraction of evidenced PRs whose required gates include codex-conformance).
    Computed only over PRs that actually carry an evidence block — never guessed
    for PRs that predate it."""
    rows = [r for r in (av.get("per_pr") or []) if r.get("evidence_gates")]
    if not rows:
        return {"available": False, "note": "no inspected PR carries an agent-verification evidence block"}
    needing_semantic = 0
    zero_ai = 0
    codex_required = 0
    for r in rows:
        gates = r["evidence_gates"]
        non_ci = [g for g in gates if g != "ci"]
        if non_ci:
            needing_semantic += 1
        else:
            zero_ai += 1
        if "codex-conformance" in gates:
            codex_required += 1
    n = len(rows)
    return {
        "available": True,
        "n": n,
        "pct_needing_semantic_review": needing_semantic / n * 100,
        "pct_zero_ai_verification": zero_ai / n * 100,
        "codex_invocation_rate": codex_required / n,
        "note": ("gate set beyond `ci` in the evidence block = semantic AI review "
                 "required (scope-warden/rules-conformance/codex-conformance/"
                 "architecture-review); denominator is PRs with an evidence block, "
                 "not all inspected PRs"),
    }


# --------------------------------------------------------------------------- #
# Ledger-sourced metric groups (token cost).
# --------------------------------------------------------------------------- #
def _read_csv(path: Path) -> list[dict] | None:
    if not path.exists():
        return None
    try:
        with path.open(newline="") as fh:
            return list(csv.DictReader(fh))
    except Exception:  # noqa: BLE001
        return None


def _to_int(v: str | None) -> int | None:
    if v is None:
        return None
    v = v.strip()
    if not v or v.upper() == "NI":
        return None
    try:
        return int(v)
    except ValueError:
        return None


def cost_metrics() -> dict:
    """Agent token cost from jobs.csv, grouped by layer and by phase."""
    jobs = _read_csv(JOBS_CSV)
    if jobs is None:
        return {"available": False, "note": f"{JOBS_CSV.relative_to(ROOT)} missing"}
    by_layer: dict[str, dict] = {}
    by_phase: dict[str, dict] = {}
    total = 0
    counted = 0
    for row in jobs:
        tok = _to_int(row.get("tokens_total"))
        layer = row.get("layer") or "?"
        phase = row.get("phase") or "?"
        bl = by_layer.setdefault(layer, {"jobs": 0, "tokens": 0})
        bp = by_phase.setdefault(phase, {"jobs": 0, "tokens": 0})
        bl["jobs"] += 1
        bp["jobs"] += 1
        if tok is not None:
            bl["tokens"] += tok
            bp["tokens"] += tok
            total += tok
            counted += 1
    for grp in (*by_layer.values(), *by_phase.values()):
        grp["avg_tokens"] = (grp["tokens"] / grp["jobs"]) if grp["jobs"] else 0
    return {
        "available": True,
        "total_jobs": len(jobs),
        "jobs_with_tokens": counted,
        "total_tokens": total,
        "by_layer": by_layer,
        "by_phase": by_phase,
        "note": "output-token totals only; tokens_R/A/H and cost_usd are NI (not instrumented)",
    }


def job_telemetry_metrics() -> dict:
    """Briefing-efficiency and build/verify/rework telemetry from jobs.csv.

    Computed only where real (non-NI) numbers exist — never fabricated. Skips
    tokens_R/A/H and cost_usd entirely (still NI-driven, see the ledger README's
    coverage-gap list); does not attempt to reconstruct them."""
    jobs = _read_csv(JOBS_CSV)
    if jobs is None:
        return {"available": False, "note": f"{JOBS_CSV.relative_to(ROOT)} missing"}

    PHASES = ("build", "verify", "rework")
    phase_tokens = {p: 0 for p in PHASES}
    phase_jobs = {p: 0 for p in PHASES}
    tokens_by_issue: dict[str, int] = {}
    tool_uses: list[int] = []
    discovery_calls: list[int] = []
    codex_jobs = 0
    verify_tokens_total = 0
    confirmed_defects_total = 0

    for row in jobs:
        issue = (row.get("issue") or "?").strip() or "?"
        phase = (row.get("phase") or "").strip()
        tok = _to_int(row.get("tokens_total"))
        tu = _to_int(row.get("tool_uses"))
        dc = _to_int(row.get("discovery_calls"))
        role = (row.get("agent_role") or "").lower()
        defects = _to_int(row.get("defects_found")) or 0
        fps = _to_int(row.get("false_positives")) or 0

        if tok is not None:
            tokens_by_issue[issue] = tokens_by_issue.get(issue, 0) + tok
            if phase in phase_tokens:
                phase_tokens[phase] += tok
            if phase == "verify":
                verify_tokens_total += tok
        if phase in phase_jobs:
            phase_jobs[phase] += 1
        if tu is not None:
            tool_uses.append(tu)
        if dc is not None:
            discovery_calls.append(dc)
        if "codex" in role:
            codex_jobs += 1
        confirmed_defects_total += max(defects - fps, 0)

    total_phase_tokens = sum(phase_tokens.values())
    proportions = {
        p: (phase_tokens[p] / total_phase_tokens) if total_phase_tokens else None
        for p in PHASES
    }
    return {
        "available": True,
        "total_jobs": len(jobs),
        "tokens_per_merged_issue": summarize(list(tokens_by_issue.values())),
        "phase_tokens": phase_tokens,
        "phase_jobs": phase_jobs,
        "phase_proportions": proportions,
        "median_tool_uses": statistics.median(tool_uses) if tool_uses else None,
        "tool_uses_n": len(tool_uses),
        "median_discovery_calls": statistics.median(discovery_calls) if discovery_calls else None,
        "discovery_calls_n": len(discovery_calls),
        "codex_invocation_rate_ledger": (codex_jobs / len(jobs)) if jobs else None,
        "verification_tokens_per_confirmed_defect": (
            (verify_tokens_total / confirmed_defects_total) if confirmed_defects_total else None
        ),
        "note": ("tokens_R/A/H and cost_usd remain NI and are not fabricated here; "
                 "median_tool_uses/median_discovery_calls only average rows with a "
                 "real (non-NI) value — discovery_calls is NI for every job logged "
                 "before #141"),
    }


def findings_metrics() -> dict:
    findings = _read_csv(FINDINGS_CSV)
    if findings is None:
        return {"available": False, "note": f"{FINDINGS_CSV.relative_to(ROOT)} missing"}
    by_stage: dict[str, int] = {}
    false_pos_by_stage: dict[str, int] = {}
    false_pos = 0
    codex_confirmed = 0
    claude_confirmed = 0
    for row in findings:
        stage = row.get("detecting_stage") or "?"
        by_stage[stage] = by_stage.get(stage, 0) + 1
        disp = (row.get("final_disposition") or "").lower()
        is_fp = "false positive" in disp or "false-positive" in disp
        if is_fp:
            false_pos += 1
            false_pos_by_stage[stage] = false_pos_by_stage.get(stage, 0) + 1
        else:
            if "codex" in stage.lower():
                codex_confirmed += 1
            else:
                claude_confirmed += 1
    confirmed_by_stage = {
        s: by_stage[s] - false_pos_by_stage.get(s, 0) for s in by_stage
    }
    return {
        "available": True,
        "total_findings": len(findings),
        "by_stage": by_stage,
        "false_positives": false_pos,
        "false_positives_by_stage": false_pos_by_stage,
        "confirmed_by_stage": confirmed_by_stage,
        "codex_confirmed_defects": codex_confirmed,
        "claude_conformance_confirmed_defects": claude_confirmed,
        "note": ("defect yield split by detecting_stage: any stage name containing "
                 "'codex' counts toward codex_confirmed_defects, everything else "
                 "toward claude_conformance_confirmed_defects; both read 0 rather "
                 "than NI when findings.csv genuinely has no rows for that source"),
    }


def human_attention_metrics(prs: list[dict], cost: dict) -> dict:
    """The headline target: human attention per merged unit.

    Not in any API. Read from the optional human-minutes.csv if present; else
    make the gap explicit rather than inventing a number."""
    merged_issues = len(prs)
    log = _read_csv(HUMAN_MINUTES_CSV)
    result = {
        "merged_prs_in_window": merged_issues,
        "human_minutes_log_present": log is not None,
    }
    if log is None:
        result["note"] = (
            f"{HUMAN_MINUTES_CSV.relative_to(ROOT)} not present — human interventions per PR "
            f"and human minutes per merged issue are {NOT_TRACKED}. This is the headline "
            "optimization target; see docs/orchestration/metrics.md for the CSV header to add.")
        result["human_minutes_per_issue"] = None
        result["interventions_per_pr"] = None
        return result
    total_minutes = 0
    total_interventions = 0
    rows = 0
    for row in log:
        m = _to_int(row.get("human_minutes"))
        i = _to_int(row.get("interventions"))
        if m is not None:
            total_minutes += m
        if i is not None:
            total_interventions += i
        rows += 1
    result["logged_rows"] = rows
    result["total_human_minutes"] = total_minutes
    result["total_interventions"] = total_interventions
    result["human_minutes_per_issue"] = (total_minutes / rows) if rows else None
    result["interventions_per_pr"] = (total_interventions / rows) if rows else None
    result["note"] = "computed from human-minutes.csv (manually logged)"
    return result


# --------------------------------------------------------------------------- #
# Assembly.
# --------------------------------------------------------------------------- #
def collect(limit: int, since: datetime | None) -> dict:
    slug = repo_slug()
    prs = fetch_merged_prs(limit, since)
    data: dict = {
        "meta": {
            "generated_at": datetime.now(timezone.utc).isoformat(),
            "repo": slug,
            "limit": limit,
            "since": since.date().isoformat() if since else None,
            "gh_available": gh_available(),
            "target": "minimum human attention per correctly-merged unit of capability",
        }
    }
    if prs is None:
        data["prs_available"] = False
        data["prs_note"] = "`gh pr list` returned nothing (gh unavailable, unauthenticated, or no merged PRs)"
        prs = []
    else:
        data["prs_available"] = True
        data["merged_pr_count"] = len(prs)

    ci = ci_run_metrics(limit, since)
    data["cycle_time"] = cycle_time_metrics(prs)
    data["ready_to_pr"] = ready_to_pr_metrics(prs, slug)
    data["ci"] = ci
    data["first_try"] = first_try_metrics(prs, ci)
    data["agent_verification"] = agent_verification_metrics(prs, slug)  # CURRENT architecture
    data["review_scope"] = review_scope_metrics(data["agent_verification"])
    data["gate_catches"] = gate_catch_metrics(prs, slug)  # HISTORICAL-COMPAT — see docstring
    data["cost"] = cost_metrics()
    data["job_telemetry"] = job_telemetry_metrics()
    data["findings"] = findings_metrics()
    data["human_attention"] = human_attention_metrics(prs, data["cost"])
    return data


# --------------------------------------------------------------------------- #
# Markdown rendering.
# --------------------------------------------------------------------------- #
def _stat_line(stats: dict | None, unit_fmt) -> str:
    if not stats:
        return "no data"
    return (f"n={stats['n']} · median {unit_fmt(stats['median'])} · "
            f"mean {unit_fmt(stats['mean'])} · min {unit_fmt(stats['min'])} · "
            f"max {unit_fmt(stats['max'])}")


def render_markdown(d: dict) -> str:
    m = d["meta"]
    L: list[str] = []
    L.append("# Orchestration metrics")
    L.append("")
    L.append(f"_Target: **{m['target']}** — not LOC, not raw PR count._")
    L.append("")
    L.append(f"- Repo: `{m['repo'] or 'unknown'}`")
    L.append(f"- Generated: {m['generated_at']}")
    L.append(f"- Window: last {m['limit']} merged PRs"
             + (f", merged on/after {m['since']}" if m['since'] else ""))
    if not m["gh_available"]:
        L.append("- **`gh` not available/authenticated — GitHub metrics degraded to notes.**")
    if not d.get("prs_available"):
        L.append(f"- **{d.get('prs_note')}**")
    L.append("")

    # 1. Cycle time.
    L.append("## Cycle time")
    ct = d["cycle_time"]["stats"]
    L.append(f"- **PR opened → merged** (exact): {_stat_line(ct, fmt_hours)}")
    r2p = d["ready_to_pr"]
    if r2p.get("available"):
        L.append(f"- **Issue READY → PR opened** (approximate): "
                 f"{_stat_line(r2p['stats'], fmt_hours)} · skipped {r2p.get('skipped', 0)} "
                 f"PRs without a usable `ready` event")
    else:
        L.append(f"- **Issue READY → PR opened**: {NOT_TRACKED} — {r2p.get('note')}")
    L.append("")

    # 2. Throughput / quality.
    L.append("## Throughput & quality")
    L.append(f"- **Merged PRs in window:** {d.get('merged_pr_count', 0)}")
    ft = d["first_try"]
    L.append(f"- **First-try vs needed-correction** ({ft['note']}):")
    L.append(f"  - first-try: {ft['first_try']}")
    L.append(f"  - needed-correction: {ft['needed_correction']}")
    L.append(f"  - undetermined: {ft['undetermined']}")
    ci = d["ci"]
    if ci.get("available"):
        rate = ci.get("failure_rate")
        rate_s = f"{rate * 100:.1f}%" if rate is not None else "n/a"
        L.append(f"- **CI failure rate** (`{CI_WORKFLOW}`, exact for window): "
                 f"{ci['failed_runs']}/{ci['total_runs']} completed runs = {rate_s}")
    else:
        L.append(f"- **CI failure rate**: {NOT_TRACKED} — {ci.get('note')}")
    L.append("")

    # 3a. Verification effectiveness — CURRENT architecture.
    L.append("## Verification effectiveness — current architecture (`agent-verification`)")
    av = d["agent_verification"]
    if av.get("available"):
        L.append(f"- Inspected {av['prs_inspected']} PR heads: "
                 f"{av['prs_with_status']} carry an `agent-verification` commit status, "
                 f"{av['prs_with_evidence']} carry the canonical per-gate evidence block.")
        if av["status_states"]:
            L.append("- Status states: " + ", ".join(
                f"`{k}`={v}" for k, v in sorted(av["status_states"].items())))
        if av["route_distribution"]:
            L.append("- Route distribution: " + ", ".join(
                f"`{k}`={v}" for k, v in sorted(av["route_distribution"].items())))
        if av["gate_seen"]:
            L.append("- Per-gate pass rate (from evidence blocks):")
            for g in sorted(av["gate_seen"]):
                L.append(f"  - `{g}`: {av['gate_pass'].get(g, 0)}/{av['gate_seen'][g]} pass")
        L.append(f"- _{av['note']}._")
        rs = d["review_scope"]
        if rs.get("available"):
            L.append(f"- **% PRs needing semantic AI review:** {rs['pct_needing_semantic_review']:.1f}% "
                     f"(n={rs['n']})")
            L.append(f"- **% PRs with zero AI verification beyond implementation:** "
                     f"{rs['pct_zero_ai_verification']:.1f}%")
            L.append(f"- **Codex invocation rate (evidence-based):** {rs['codex_invocation_rate']:.2f}")
        else:
            L.append(f"- **% PRs needing semantic AI review / zero AI verification:** "
                     f"{NOT_TRACKED} — {rs.get('note')}")
    else:
        L.append(f"- {NOT_TRACKED} — {av.get('note')}")
    L.append("")

    # 3b. Verification effectiveness — HISTORICAL, pre-#90/#91 check-runs.
    L.append("## Verification effectiveness — HISTORICAL (pre-#90/#91 check-runs)")
    L.append("_Not the current architecture — see the section above. Retained only for PRs "
             "that predate #90/#91; do not read a 0 here as \"verification caught nothing\"._")
    gc = d["gate_catches"]
    if gc.get("available") and gc.get("gate_check_runs_present"):
        L.append(f"Gate `failure` conclusions across {gc['prs_inspected']} inspected PR heads:")
        for g in GATE_NAMES:
            L.append(f"- `{g}`: {gc['catches'][g]} failure(s) / {gc['seen'][g]} check-run(s) seen")
    else:
        L.append(f"- {NOT_TRACKED} — {gc.get('note')}")
        L.append("  Gate names watched: " + ", ".join(f"`{g}`" for g in GATE_NAMES) + ".")
    L.append("")

    # 4. Cost (tokens).
    L.append("## Cost (agent tokens)")
    cost = d["cost"]
    if cost.get("available"):
        L.append(f"- **Total agent-job output tokens:** {cost['total_tokens']:,} "
                 f"across {cost['jobs_with_tokens']}/{cost['total_jobs']} jobs")
        L.append(f"- _Note: {cost['note']}._")
        L.append("")
        L.append("| Layer | Jobs | Tokens | Avg/job |")
        L.append("|---|--:|--:|--:|")
        for layer in sorted(cost["by_layer"]):
            g = cost["by_layer"][layer]
            L.append(f"| {layer} | {g['jobs']} | {g['tokens']:,} | {g['avg_tokens']:,.0f} |")
        L.append("")
        L.append("| Phase | Jobs | Tokens | Avg/job |")
        L.append("|---|--:|--:|--:|")
        for phase in sorted(cost["by_phase"]):
            g = cost["by_phase"][phase]
            L.append(f"| {phase} | {g['jobs']} | {g['tokens']:,} | {g['avg_tokens']:,.0f} |")
    else:
        L.append(f"- {NOT_TRACKED} — {cost.get('note')}")
    L.append("")

    # Job telemetry — briefing efficiency + build/verify/rework split.
    L.append("## Job telemetry (briefing efficiency, build/verify/rework)")
    jt = d["job_telemetry"]
    if jt.get("available"):
        tpi = jt["tokens_per_merged_issue"]
        L.append(f"- **Agent tokens per Issue:** {_stat_line(tpi, lambda v: f'{v:,.0f}')}")
        L.append("- **Build/verify/rework token proportions:** " + ", ".join(
            f"{p}={jt['phase_proportions'][p] * 100:.1f}%" if jt['phase_proportions'][p] is not None
            else f"{p}={NOT_TRACKED}" for p in ("build", "verify", "rework")))
        mtu = jt["median_tool_uses"]
        L.append(f"- **Median tool uses/job:** " + (f"{mtu:.0f} (n={jt['tool_uses_n']})" if mtu is not None else NOT_TRACKED))
        mdc = jt["median_discovery_calls"]
        L.append(f"- **Median discovery calls/job:** " + (f"{mdc:.0f} (n={jt['discovery_calls_n']})" if mdc is not None else NOT_TRACKED))
        civr = jt["codex_invocation_rate_ledger"]
        L.append(f"- **Codex invocation rate (ledger jobs):** " + (f"{civr:.2f}" if civr is not None else NOT_TRACKED))
        vtpd = jt["verification_tokens_per_confirmed_defect"]
        L.append(f"- **Verification tokens per confirmed defect:** " + (f"{vtpd:,.0f}" if vtpd is not None else NOT_TRACKED))
        L.append(f"- _{jt['note']}._")
    else:
        L.append(f"- {NOT_TRACKED} — {jt.get('note')}")
    L.append("")

    fnd = d["findings"]
    if fnd.get("available"):
        L.append(f"- **Findings logged:** {fnd['total_findings']} "
                 f"(false positives: {fnd['false_positives']})")
        for stage, n in sorted(fnd["by_stage"].items()):
            L.append(f"  - `{stage}`: {n} (confirmed: {fnd['confirmed_by_stage'].get(stage, 0)})")
        L.append(f"- **Codex-confirmed defects:** {fnd['codex_confirmed_defects']} · "
                 f"**Claude-conformance-confirmed defects:** {fnd['claude_conformance_confirmed_defects']}")
    else:
        L.append(f"- **Findings:** {NOT_TRACKED} — {fnd.get('note')}")
    L.append("")

    # 5. Human attention — the headline.
    L.append("## Human attention (headline target)")
    ha = d["human_attention"]
    if ha.get("human_minutes_log_present"):
        hpi = ha.get("human_minutes_per_issue")
        ipp = ha.get("interventions_per_pr")
        L.append(f"- **Human minutes / merged issue:** "
                 f"{hpi:.1f} (from {ha['logged_rows']} logged rows)" if hpi is not None
                 else "- **Human minutes / merged issue:** no rows logged")
        L.append(f"- **Human interventions / PR:** {ipp:.2f}" if ipp is not None
                 else "- **Human interventions / PR:** no rows logged")
        L.append(f"- _{ha['note']}._")
    else:
        L.append(f"- **Human minutes / merged issue:** {NOT_TRACKED}")
        L.append(f"- **Human interventions / PR:** {NOT_TRACKED}")
        L.append(f"- {ha.get('note')}")
    L.append("")
    L.append("---")
    L.append("_Heuristic and approximate metrics are labelled inline. `NI`/`not tracked` means "
             "not measured — never zero. See docs/orchestration/metrics.md._")
    return "\n".join(L) + "\n"


# --------------------------------------------------------------------------- #
def main() -> int:
    ap = argparse.ArgumentParser(description="Compute orchestration-system metrics.")
    ap.add_argument("--limit", type=int, default=20, help="recent merged PRs to inspect (default 20)")
    ap.add_argument("--since", help="only PRs merged on/after this date (YYYY-MM-DD)")
    ap.add_argument("--json", action="store_true", help="emit JSON instead of markdown")
    args = ap.parse_args()

    since = None
    if args.since:
        try:
            since = datetime.strptime(args.since, "%Y-%m-%d").replace(tzinfo=timezone.utc)
        except ValueError:
            print(f"--since must be YYYY-MM-DD, got {args.since!r}", file=sys.stderr)
            return 2

    data = collect(args.limit, since)

    if args.json:
        # Drop bulky raw run payloads from the JSON surface; keep the metrics.
        if isinstance(data.get("ci"), dict):
            data["ci"].pop("runs", None)
        print(json.dumps(data, indent=2, default=str))
    else:
        print(render_markdown(data), end="")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
