# Per-layer rollups

Derived from [`jobs.csv`](jobs.csv) and [`findings.csv`](findings.csv). See [`README.md`](README.md)
for schema and instrumentation gaps. `NI` = not instrumented (not zero).

## Layer 3 — Characters (issue #40, PR #41)

### Build / verify / rework (core lens)

| Category | Jobs | Tokens | Share |
|---|---|--:|--:|
| Build | 1 | 198,699 | 21.7% |
| Verify | 3 | 179,680 | 19.7% |
| Rework | 2 | 535,347 | 58.6% |
| **Total** | **6** | **913,726** | |

- **Tool uses:** 276 · **Agent wall-clock:** ~37.2 min · **Tests:** 1728 → 1737.
- **Rework ratio** (rework ÷ (build + rework)): **0.73**.
- **By model tier** (token share): sonnet/medium implementer **80.3%** (734,046), opus/high
  conformance **13.3%** (121,402), haiku/low scope-gate **6.4%** (58,278).

### §6.5 net-value metric skeleton (what the paper needs per layer)

Populated where measurable now; `NI` where the instrument is not yet built (see README gap list).

| Metric | Layer 3 value | Source |
|---|---|--:|
| Cost per merged issue (output tokens) | 913,726 (agent jobs only; orchestrator excluded) | `jobs.csv` |
| Correction rounds | 2 (defect-fix, then invariant-fix) | `jobs.csv` phase=rework |
| First-pass acceptance | No — build shipped 2 latent defects (F1, F2) + 1 invariant nick (F3) | `findings.csv` |
| Review iterations | 2 conformance passes (audit + re-audit) + 1 scope gate | `jobs.csv` phase=verify |
| Defects caught per verify layer | conformance: 2 (audit) + 1 (re-audit); scope-gate: 0 | `findings.csv` |
| Verify false-positive rate | 0/3 findings were false positives | `findings.csv` |
| Marginal unique-defect yield (conformance) | 3 unique findings none of the deterministic gates (build tests, CI) or the scope-gate caught | `findings.csv` |
| Deterministic controls added | 9 tests + 1 new ruleset (ExperienceRuleset) promoted (P5) | `jobs.csv` |
| Repeated-error occurrences | 0 | `jobs.csv` |
| Escaped-defect / reopen rate | pending (measured after merge) | — |
| Maintenance effort per control | NI | needs future-layer tracking |
| R/A/H token accounting | NI | needs turn-level token instrument (README gap) |
| Human minutes | NI | not measured |
| Cost (USD) | NI | needs input-token + pricing |

### Findings this layer (see `findings.csv`)

- **F1** — improvement roll ignored the >=100% cap (BRP p.138). Caught by conformance audit; fixed.
- **F2** — Teach missing the 75% training cap (BRP p.139). Caught by conformance audit; fixed.
- **F3** — training cap hardcoded, not data-sourced (invariant 7). Caught by conformance re-audit; fixed.

All three escaped the build's own tests and the cheap scope gate; all were surfaced by the
opus/high conformance stage — a concrete data point for the paper's P4/P7 (risk-proportional,
failure-diverse verification) and its §6.5 "marginal unique-defect yield" question.

### Observations for the paper (Layer 3)

- **Rework > build (0.73 ratio).** First-pass rules *fidelity* was wrong twice; correcting rules
  against printed text is expensive (re-read book, update sim, re-test). Rules-fidelity spend is the
  leak, not scaffolding.
- **Lever to test next layer:** front-load `rules-extractor` (haiku, cheap) to pin the book numbers
  *before* the build, as issue #40 prescribed — predicted to move fidelity cost from the expensive
  rework column into the cheap build column. Layer 3 led with the build; treat the next layer as the
  A/B counterpart and compare rework ratios.
- **Instrumentation debt:** the C2 R/A/H split could not be captured from summary telemetry — the
  same limitation the paper concedes for task #9. Fixing the ledger's turn-level token capture is
  the prerequisite for turning C2 from a single anecdote into the 30–100-task evidence the reviewer
  asked for.
