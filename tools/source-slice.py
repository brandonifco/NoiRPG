#!/usr/bin/env python3
"""source-slice — deterministic page-range extraction from the authoritative BRP source.

Every agent that needs source text should get the same excerpt, generated the same
way, rather than re-reading and re-transcribing the 303-page PDF independently. This
tool does exactly one thing: slice a page range out of
`BasicRoleplaying-ORC-Content-Document.pdf` and emit it with a header identifying
what it is, so the excerpt is self-describing and reproducible.

It is a page-slice tool, not a search tool. It does not index, search, or reason
about the document — see `docs/source-handling.md` for how pages are located
(exact pages named in the Issue, or `rules-extractor` locates a section once and
reports the range).

    tools/source-slice.py --pages 130
    tools/source-slice.py --pages 130-132 --layout
    tools/source-slice.py --pages 130 --bbox
    tools/source-slice.py --pages 5-9 --output /tmp/packet.txt
    tools/source-slice.py --pages 130 --expect "Hit Points by Location"

`--expect <regex>` (repeatable) is an opt-in sanity check on the already-chosen
page range, not a search feature: it does not decide which pages to slice, it
only asserts that a regex the caller already expects to be present actually
appears in the sliced body text. If any given pattern is missing, the tool
exits non-zero and names the missing anchor instead of silently handing back a
packet that does not cover what it claims to. See `docs/source-handling.md`.

The source file is hardcoded. This tool NEVER accepts an arbitrary --file — the
superseded `BRP SRD 1.0.2.pdf` must never be reachable through this path. Before
any extraction, the source file is verified against the pinned SHA-256 in
`.github/authoritative-source.sha256`; a mismatch or missing file fails loudly and
extracts nothing.
"""
from __future__ import annotations

import argparse
import hashlib
import os
import re
import subprocess
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]

# The one authoritative source. Hardcoded on purpose — see module docstring.
SOURCE_FILENAME = "BasicRoleplaying-ORC-Content-Document.pdf"
SOURCE_PATH = ROOT / SOURCE_FILENAME

# Default location of the pinned hash. Overridable via SOURCE_SLICE_SHA_FILE for
# tests only, so a test can exercise the "pinned hash does not match" failure path
# without touching the real pinned file. There is no equivalent override for the
# source file itself.
DEFAULT_SHA_FILE = ROOT / ".github" / "authoritative-source.sha256"


class SourceSliceError(RuntimeError):
    """Raised for any failure that should abort extraction before it starts."""


def _sha_file_path() -> Path:
    override = os.environ.get("SOURCE_SLICE_SHA_FILE")
    return Path(override) if override else DEFAULT_SHA_FILE


def _sha256_of(path: Path) -> str:
    h = hashlib.sha256()
    with open(path, "rb") as f:
        for chunk in iter(lambda: f.read(1 << 20), b""):
            h.update(chunk)
    return h.hexdigest()


def read_pinned_hash(sha_file: Path) -> str:
    """Parse the first '<hex>  <filename>' line of the pinned-hash file."""
    if not sha_file.is_file():
        raise SourceSliceError(f"pinned hash file not found: {sha_file}")
    text = sha_file.read_text(encoding="utf-8")
    for line in text.splitlines():
        line = line.strip()
        if not line:
            continue
        m = re.match(r"^([0-9a-fA-F]+)\s+\S*(" + re.escape(SOURCE_FILENAME) + r")?$", line)
        if m:
            return m.group(1).lower()
        # Fall back to "first whitespace-delimited token looks like a hex digest".
        parts = line.split()
        if parts and re.match(r"^[0-9a-fA-F]{32,}$", parts[0]):
            return parts[0].lower()
    raise SourceSliceError(f"could not parse a hash out of {sha_file}")


def verify_source() -> str:
    """Verify SOURCE_PATH against the pinned hash. Returns the verified hash.

    Raises SourceSliceError (never extracts) on any mismatch or missing file.
    """
    if not SOURCE_PATH.is_file():
        raise SourceSliceError(f"authoritative source not found: {SOURCE_PATH}")

    sha_file = _sha_file_path()
    pinned = read_pinned_hash(sha_file)
    actual = _sha256_of(SOURCE_PATH)

    if actual != pinned:
        raise SourceSliceError(
            "authoritative source failed SHA-256 verification against "
            f"{sha_file}\n  pinned:  {pinned}\n  actual:  {actual}\n"
            "Refusing to extract from an unverified source."
        )
    return actual


def parse_pages(spec: str) -> tuple[int, int]:
    spec = spec.strip()
    m = re.match(r"^(\d+)(?:-(\d+))?$", spec)
    if not m:
        raise SourceSliceError(f"invalid --pages value: {spec!r} (want N or A-B)")
    first = int(m.group(1))
    last = int(m.group(2)) if m.group(2) else first
    if first < 1 or last < first:
        raise SourceSliceError(f"invalid page range: {spec!r}")
    return first, last


def extract_mode(layout: bool, bbox: bool) -> str:
    if bbox and layout:
        return "bbox+layout"
    if bbox:
        return "bbox"
    if layout:
        return "layout"
    return "plain"


def run_pdftotext(first: int, last: int, mode: str) -> str:
    args = ["pdftotext", "-f", str(first), "-l", str(last)]
    if mode == "bbox+layout":
        args.append("-bbox-layout")
    elif mode == "bbox":
        args.append("-bbox")
    elif mode == "layout":
        args.append("-layout")
    # "plain" adds nothing: docs/source-handling.md notes -layout scrambles the
    # single-column body text, so plain mode deliberately omits it.
    args += [str(SOURCE_PATH), "-"]

    try:
        result = subprocess.run(args, capture_output=True, text=True, check=False)
    except FileNotFoundError as e:
        raise SourceSliceError(f"pdftotext not available: {e}") from e

    if result.returncode != 0:
        raise SourceSliceError(
            f"pdftotext failed (exit {result.returncode}): {result.stderr.strip()}"
        )
    return result.stdout


def check_expected_anchors(body: str, patterns: list[str]) -> None:
    """Raise SourceSliceError naming any --expect pattern absent from body.

    This is a presence check on the already-sliced text, not a search: it does
    not locate pages or sections, it only confirms the page range the caller
    already chose actually contains what they expect it to.
    """
    missing = []
    for pattern in patterns:
        try:
            found = re.search(pattern, body) is not None
        except re.error as e:
            raise SourceSliceError(f"invalid --expect regex {pattern!r}: {e}") from e
        if not found:
            missing.append(pattern)
    if missing:
        joined = "\n  - ".join(missing)
        raise SourceSliceError(
            "sliced text is missing expected anchor(s) — the page range may be "
            f"wrong or truncated:\n  - {joined}"
        )


def build_packet(pages_spec: str, first: int, last: int, mode: str, pinned_hash: str, body: str) -> str:
    header = (
        "# BRP source packet\n"
        f"# authoritative-file: {SOURCE_FILENAME}\n"
        f"# authoritative-sha256: {pinned_hash}\n"
        f"# pages: {pages_spec}\n"
        f"# mode: {mode}\n"
        "#\n"
        "# Generated by tools/source-slice.py. Ephemeral by default — see\n"
        "# docs/source-handling.md before committing a source packet.\n"
    )
    return header + body


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(
        prog="source-slice.py",
        description="Deterministically slice a page range out of the authoritative BRP source.",
    )
    parser.add_argument("--pages", required=True, help="page number 'N' or range 'A-B'")
    parser.add_argument("--layout", action="store_true", help="preserve column/table layout (pdftotext -layout)")
    parser.add_argument("--bbox", action="store_true", help="emit bounding-box glyph data (pdftotext -bbox)")
    parser.add_argument("--output", metavar="FILE", help="write the packet to FILE instead of stdout")
    parser.add_argument(
        "--expect",
        metavar="REGEX",
        action="append",
        default=[],
        help="regex that must be present in the sliced text (repeatable); "
        "fails loudly if any are absent (opt-in citation sanity check)",
    )
    args = parser.parse_args(argv)

    try:
        first, last = parse_pages(args.pages)
        pinned_hash = verify_source()
        mode = extract_mode(args.layout, args.bbox)
        body = run_pdftotext(first, last, mode)
        if args.expect:
            check_expected_anchors(body, args.expect)
        packet = build_packet(args.pages, first, last, mode, pinned_hash, body)
    except SourceSliceError as e:
        print(f"source-slice: {e}", file=sys.stderr)
        return 1

    if args.output:
        Path(args.output).write_text(packet, encoding="utf-8")
    else:
        try:
            sys.stdout.write(packet)
        except BrokenPipeError:
            # Downstream consumer (e.g. `| head`) closed early; not our error.
            try:
                sys.stdout.close()
            except Exception:
                pass
            return 0
    return 0


if __name__ == "__main__":
    sys.exit(main())
