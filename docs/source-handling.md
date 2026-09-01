# Working with the source book

Everything mechanical in this engine comes from one document. This file is how to get
things out of it correctly, and what is already known to be wrong with it.

## The one source, and the one forbidden one

**Use:** `BasicRoleplaying-ORC-Content-Document.pdf` (repo root). 303 pages, ORC licensed.

**Never use:** `BRP SRD 1.0.2.pdf`. It is a different, superseded 2020 document under a
different licence. It has **four** degrees of success where ours has five, and different
threshold rounding — half-up where ours is ceiling, which diverges at **54 of 120**
possible chance values. Code derived from it looks entirely correct and is wrong more
often than right. It is gitignored; if a copy appears, do not read it for mechanics.

`orc-scope-filter.md` decides what we implement. Roughly 60% of the book is out of scope.

## Extraction recipe

```bash
pdftotext BasicRoleplaying-ORC-Content-Document.pdf /tmp/orc.txt
```

**No `-layout` for body text.** The document is single-column and linear; `-layout`
scrambles reading order.

**Page breaks are form feeds (`\f`).** They silently break naive greps — a heading at
the top of a page will not match `^Heading` because the line begins with `\f`. Strip or
account for them.

**Wide tables are the exception.** The Resistance Table is a 24×24 grid that the
whitespace dump misaligns. For those, extract the single page with layout preserved:

```bash
pdftotext -layout -f 130 -l 130 BasicRoleplaying-ORC-Content-Document.pdf -
```

**For a disputed cell, go to coordinates.** `pdftotext -bbox` gives glyph positions, so
a value can be assigned to a column by matching its x-position against the header row.
This is how the resistance misprint below was confirmed to be a real printing error
rather than an extraction artifact. Rendering the region at 300 dpi and reading it
visually is the final check.

## Generating a source packet: `tools/source-slice.py`

Do not re-read and re-transcribe the 303-page PDF by hand every time an agent needs
an excerpt. `tools/source-slice.py` deterministically slices a page range out of
`BasicRoleplaying-ORC-Content-Document.pdf` and emits it with a header identifying
the authoritative filename, its pinned SHA-256, the requested pages, and the
extraction mode:

```bash
tools/source-slice.py --pages 130                     # single page, plain text
tools/source-slice.py --pages 130-132 --layout         # wide/tabular pages
tools/source-slice.py --pages 130 --bbox                # disputed-cell escalation
tools/source-slice.py --pages 130 --output /tmp/p130.txt
```

Before extracting anything, it verifies the PDF against the pinned hash in
`.github/authoritative-source.sha256` and fails loudly (extracting nothing) if the
hash does not match. It never accepts an arbitrary `--file` — the source filename is
hardcoded so the superseded `BRP SRD 1.0.2.pdf` is not reachable through this tool.
It is a page-slice tool, not a search or indexing system: it does not decide *which*
pages matter, only extracts the ones it is told.

**Locating the pages.** Two paths, depending on what the Issue says:

- **Issue names exact pages.** The orchestrator runs `source-slice.py` directly and
  hands the resulting packet to `engine-dev`, `rules-conformance`, and Codex.
- **Issue names only a section** (e.g. "the Resistance Table" without a page number).
  `rules-extractor` locates it once and reports back the page range — it does not
  transcribe the content itself. The orchestrator then generates the packet from
  that range the same way, so the extraction is deterministic and reviewable.

**Independence rule.** Agents may share the same primary-source excerpt — that is
the point of generating one packet instead of several independent transcriptions.
But `codex-conformance` must never be fed `rules-conformance`'s reasoning, notes, or
conclusions about that excerpt. It gets the same raw packet and re-derives its
verification independently; that independence is what makes it a useful
cross-vendor check rather than an echo of the first reviewer.

**Escalation for disputed cells.** Plain mode is the default (see below — `-layout`
scrambles the single-column body). Wide tables use `--layout`. A disputed cell
escalates to `--bbox` for glyph coordinates, then to a `pdftoppm`-rendered image
read visually, exactly as documented for the resistance-table misprint below.

**Packets are ephemeral.** Generate and discard by default; do not commit a source
packet to the repo unless there is a specific, stated reason to keep it (e.g. it is
itself the artifact under review for a documentation defect in the extraction).

## The discipline

**Where the book prints a table, reproduce the whole table as test data.** One case per
printed row or cell, data-driven, so a transcription error surfaces as a named failing
row rather than hiding inside a loop. Never spot-check a table.

**Derive closed forms, then try to falsify them.** Every closed form in this engine was
checked against every value in the printed range, not against sample points. Report the
count you actually verified.

**The printed table beats the prose.** The book's descriptive sentences have contradicted
its own tables twice (see below). Tables are normative; prose hedges.

**Cite the chapter and section for every mechanic you implement.** A misattributed
citation is a defect even when the behaviour is right — it is the audit trail the next
verification pass will trust.

## Known errata in the book

Both were established at real cost. Neither is a transcription mistake on our side.

### 1. The fumble prose contradicts the Skill Results Table

Ch 5 describes the fumble chance as one twentieth of the chance of failure. Read
literally that is one row too narrow at every multiple of 20, and at a 100% chance it
yields no fumble range at all — despite the same paragraph stating that a roll of 00
always fumbles.

**The table is authoritative.** Its preamble is normative where the prose hedges with
"usually", and the prose's literal reading self-contradicts. Implemented per the table;
see `ResolutionPolicy` and ADR 0007.

### 2. One resistance table cell is misprinted

The cell at **passive 15, active 24** prints a value 10 lower than it should. Both the
closed form and the row's own progression give the higher value, and the fifteen other
cells sharing that difference all print it.

Confirmed by two independent monotonicity violations — the row decreases at its final
column, and the column breaks an otherwise perfect descent — plus bounding-box
coordinates placing the glyph under the correct column header, plus a 300 dpi render.

`ResistanceTableTests` pins **both** the printed value and the engine's value and asserts
they differ. That test fails loudly if either the transcription is "corrected" to match
the formula or the engine silently starts matching the misprint.

## Recurring defect classes

Every one of these has bitten this project at least once. They are what a verification
pass should look for first.

| Class | What it looks like |
|---|---|
| **Unchecked assertion** | A mechanical claim written from memory, sitting beside verified claims with nothing distinguishing them. Caused two full rewrites on #11. |
| **Misattributed citation** | Behaviour is right, but the cited chapter or page does not say what is claimed. Found on #10, #11, and #12. |
| **Contaminated inheritance** | A formula copied from a document derived from the superseded book. |
| **Prose over table** | Implementing a descriptive sentence instead of the printed grid it describes. |
| **Silent table match** | Matching a misprint without recording that it is one, so the next reader cannot tell code from book. |
