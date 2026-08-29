#!/usr/bin/env python3
"""Write a concise, agent-readable job summary to GITHUB_STEP_SUMMARY.

Deliberately short. Its purpose is to let an agent decide its next action from
tens of lines rather than thousands; the full log and the .trx artifact remain
available for the cases that genuinely need them.
"""

from __future__ import annotations

import os
import pathlib
import xml.etree.ElementTree as ET

NS = {"t": "http://microsoft.com/schemas/VisualStudio/TeamTest/2010"}
RERUN = "dotnet test --filter FullyQualifiedName~{name}"


def find_trx() -> pathlib.Path | None:
    results = sorted(pathlib.Path("TestResults").rglob("*.trx"))
    return results[0] if results else None


def parse(trx: pathlib.Path) -> tuple[dict[str, str], list[tuple[str, str]]]:
    """Return (counters, [(failing test name, first useful error line)])."""
    root = ET.parse(trx).getroot()

    counters_el = root.find(".//t:ResultSummary/t:Counters", NS)
    counters = dict(counters_el.attrib) if counters_el is not None else {}

    failures: list[tuple[str, str]] = []
    for result in root.findall(".//t:UnitTestResult", NS):
        if result.get("outcome") != "Failed":
            continue
        name = result.get("testName", "(unnamed)")
        message = result.find(".//t:Output/t:ErrorInfo/t:Message", NS)
        first_line = "(no message)"
        if message is not None and message.text:
            for line in message.text.splitlines():
                if line.strip():
                    first_line = line.strip()
                    break
        failures.append((name, first_line))
    return counters, failures


def mark(outcome: str) -> str:
    return {"success": "passed", "failure": "FAILED", "skipped": "skipped"}.get(
        outcome, outcome or "not run"
    )


def main() -> None:
    lines: list[str] = []
    build = os.environ.get("BUILD_RESULT", "")
    test = os.environ.get("TEST_RESULT", "")
    fmt = os.environ.get("FORMAT_RESULT", "")

    lines.append(f"Build:  {mark(build)}, 0 warnings (warnaserror)")

    trx = find_trx()
    failures: list[tuple[str, str]] = []
    if trx is None:
        lines.append(f"Tests:  {mark(test)} (no .trx produced)")
    else:
        counters, failures = parse(trx)
        # .trx names the skipped counter "notExecuted"; "error" counts tests that
        # failed outside an assertion (fixture blew up), which is worth surfacing
        # separately because the cause is usually different.
        errored = int(counters.get("error", 0) or 0)
        lines.append(
            "Tests:  {passed} passed, {failed} failed, {skipped} skipped, {total} total".format(
                passed=counters.get("passed", "?"),
                failed=counters.get("failed", "?"),
                skipped=counters.get("notExecuted", "?"),
                total=counters.get("total", "?"),
            )
            + (f" ({errored} errored)" if errored else "")
        )

    lines.append(f"Format: {mark(fmt)}")

    if failures:
        lines.append("")
        lines.append("Failures:")
        # Cap the list so one broken assumption cannot flood the summary. The
        # artifact holds the rest.
        for name, message in failures[:5]:
            short = name.rsplit(".", 1)[-1]
            lines.append(f"  {name}")
            lines.append(f"    {message[:160]}")
            lines.append(f"    rerun: {RERUN.format(name=short)}")
        if len(failures) > 5:
            lines.append(f"  ... and {len(failures) - 5} more (see test-results artifact)")

    if fmt == "failure":
        lines.append("")
        lines.append("Formatting differs. Fix with: dotnet format")

    body = "```\n" + "\n".join(lines) + "\n```\n"
    summary_path = os.environ.get("GITHUB_STEP_SUMMARY")
    if summary_path:
        with open(summary_path, "a", encoding="utf-8") as handle:
            handle.write(body)
    print(body)


if __name__ == "__main__":
    main()
