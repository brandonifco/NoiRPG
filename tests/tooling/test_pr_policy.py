#!/usr/bin/env python3
"""Unit/fixture tests for .github/workflows/pr_policy.py (Issue #136).

pr_policy.py is one half of the collapsed verification-evidence model: it must
emit STATIC PR metadata only (route, required gates, body-policy result) and
must never claim a verification gate passed — that is tools/agent-verify.sh's
job, covered separately by tests/tooling/test_agent_verify.sh.

No external test framework is set up in this repo (no pytest, no conftest.py)
so this uses only the standard library, matching how the other tools/*.py
scripts here are exercised: as real subprocess invocations against a fixture
PR body, the same way pr-policy.yml runs the script in CI.

Run directly:
    python3 -m unittest tests/tooling/test_pr_policy.py -v
or via the repo-root test suite:
    python3 -m unittest discover -s tests/tooling -p 'test_*.py' -v
"""
from __future__ import annotations

import json
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
SCRIPT = ROOT / ".github" / "workflows" / "pr_policy.py"

COMPLETE_BODY = """\
## Exact behavioral claim
pr_policy.py now emits static PR metadata only; it no longer claims any
verification gate passed.

## Scope
Only the evidence dict shape in pr_policy.py changed. Route derivation and
body-policy checks did not change.

## Rules conformance
N/A — no rules-engine files touched.

## Tests and evidence
`python3 -m unittest tests/tooling/test_pr_policy.py -v` — all tests pass.

## Decisions and tradeoffs
None beyond the evidence-schema collapse itself.

## Known limitations
None known.

## Agent provenance
orchestration-dev

Closes #136
"""

INCOMPLETE_BODY = """\
Closes #136

Did a thing.
"""


def run_pr_policy(body: str, out_path: Path, base: str, head: str) -> subprocess.CompletedProcess:
    with tempfile.NamedTemporaryFile("w", suffix=".md", delete=False, dir=out_path.parent) as fh:
        fh.write(body)
        body_file = fh.name
    try:
        return subprocess.run(
            [
                sys.executable,
                str(SCRIPT),
                "--body-file", body_file,
                "--base", base,
                "--head", head,
                "--out", str(out_path),
            ],
            cwd=ROOT,
            capture_output=True,
            text=True,
        )
    finally:
        Path(body_file).unlink(missing_ok=True)


class PrPolicyStaticEvidenceTests(unittest.TestCase):
    """Pin the evidence shape: static PR metadata only, no gateStates."""

    @classmethod
    def setUpClass(cls) -> None:
        # An explicit base == head gives an empty diff, so the route is the
        # deterministic "tooling" baseline regardless of what happens to be
        # dirty in the working tree while this test runs.
        cls.head = subprocess.check_output(
            ["git", "rev-parse", "HEAD"], cwd=ROOT, text=True
        ).strip()
        cls.tmpdir = tempfile.TemporaryDirectory()
        cls.out_path = Path(cls.tmpdir.name) / "evidence.json"

    @classmethod
    def tearDownClass(cls) -> None:
        cls.tmpdir.cleanup()

    def test_complete_body_passes_and_evidence_has_exact_static_keys(self) -> None:
        result = run_pr_policy(COMPLETE_BODY, self.out_path, self.head, self.head)
        self.assertEqual(result.returncode, 0, msg=result.stdout + result.stderr)

        evidence = json.loads(self.out_path.read_text())

        expected_keys = {
            "schemaVersion", "issue", "baseSha", "headSha", "route",
            "escalated", "issueRoute", "requiredGates", "testsReported",
            "prPolicy",
        }
        self.assertEqual(set(evidence.keys()), expected_keys)

        # The old, App/state-machine-shaped keys must be gone entirely.
        for stale_key in ("gateStates", "base", "head", "tests"):
            self.assertNotIn(stale_key, evidence)

    def test_schema_version_present_and_stable(self) -> None:
        result = run_pr_policy(COMPLETE_BODY, self.out_path, self.head, self.head)
        self.assertEqual(result.returncode, 0, msg=result.stdout + result.stderr)
        evidence = json.loads(self.out_path.read_text())
        self.assertEqual(evidence["schemaVersion"], 1)

    def test_no_gate_is_ever_marked_pass_by_pr_policy(self) -> None:
        """The defect this issue closes: pr-policy hardcoding ci -> pass."""
        result = run_pr_policy(COMPLETE_BODY, self.out_path, self.head, self.head)
        self.assertEqual(result.returncode, 0, msg=result.stdout + result.stderr)
        evidence = json.loads(self.out_path.read_text())

        self.assertIsInstance(evidence["requiredGates"], list)
        self.assertIn("ci", evidence["requiredGates"])
        # requiredGates is a flat list of gate names, not a name->state map —
        # there is nowhere left in this object to spell {"ci": "pass"}, and the
        # old gateStates key (which did) is gone (asserted above).
        for gate in evidence["requiredGates"]:
            self.assertIsInstance(gate, str)
        self.assertNotIn("gateStates", evidence)

    def test_metadata_reflects_route_and_head(self) -> None:
        result = run_pr_policy(COMPLETE_BODY, self.out_path, self.head, self.head)
        self.assertEqual(result.returncode, 0, msg=result.stdout + result.stderr)
        evidence = json.loads(self.out_path.read_text())

        self.assertEqual(evidence["headSha"], self.head)
        self.assertEqual(evidence["baseSha"], self.head)
        self.assertEqual(evidence["issue"], 136)
        self.assertEqual(evidence["route"], "tooling")
        self.assertEqual(sorted(evidence["requiredGates"]), ["ci"])
        self.assertEqual(evidence["prPolicy"], "pass")
        self.assertIn("issueRoute", evidence)  # present even when null

    def test_incomplete_body_is_still_rejected(self) -> None:
        """The pre-existing incomplete-body gate must still work unchanged."""
        result = run_pr_policy(INCOMPLETE_BODY, self.out_path, self.head, self.head)
        self.assertEqual(result.returncode, 1, msg=result.stdout + result.stderr)
        self.assertIn("::error title=pr-policy::", result.stdout)

        evidence = json.loads(self.out_path.read_text())
        self.assertEqual(evidence["prPolicy"], "fail")


class PrPolicyOneClosingIssueTests(unittest.TestCase):
    """Issue #168: exactly one closing Issue is required — not zero, not two+."""

    @classmethod
    def setUpClass(cls) -> None:
        cls.head = subprocess.check_output(
            ["git", "rev-parse", "HEAD"], cwd=ROOT, text=True
        ).strip()
        cls.tmpdir = tempfile.TemporaryDirectory()
        cls.out_path = Path(cls.tmpdir.name) / "evidence.json"

    @classmethod
    def tearDownClass(cls) -> None:
        cls.tmpdir.cleanup()

    def test_two_closing_issues_fails_with_new_violation(self) -> None:
        body = COMPLETE_BODY.replace("Closes #136", "Closes #136\nCloses #200")
        result = run_pr_policy(body, self.out_path, self.head, self.head)
        self.assertEqual(result.returncode, 1, msg=result.stdout + result.stderr)
        self.assertIn("Multiple closing Issues (#136, #200)", result.stdout)

        evidence = json.loads(self.out_path.read_text())
        self.assertEqual(evidence["prPolicy"], "fail")

    def test_one_closing_issue_passes_unchanged(self) -> None:
        result = run_pr_policy(COMPLETE_BODY, self.out_path, self.head, self.head)
        self.assertEqual(result.returncode, 0, msg=result.stdout + result.stderr)

        evidence = json.loads(self.out_path.read_text())
        self.assertEqual(evidence["prPolicy"], "pass")
        self.assertEqual(evidence["issue"], 136)

    def test_zero_closing_issues_still_fails_with_existing_message(self) -> None:
        body = COMPLETE_BODY.replace("Closes #136\n", "")
        result = run_pr_policy(body, self.out_path, self.head, self.head)
        self.assertEqual(result.returncode, 1, msg=result.stdout + result.stderr)
        self.assertIn("Linked Issue missing", result.stdout)

        evidence = json.loads(self.out_path.read_text())
        self.assertEqual(evidence["prPolicy"], "fail")


if __name__ == "__main__":
    unittest.main()
