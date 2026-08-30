# Per-layer rollups

Derived from [`jobs.csv`](jobs.csv). See [`README.md`](README.md) for method.

## Layer 3 — Characters (issue #40, PR #41)

| Category | Jobs | Tokens | Share |
|---|---|--:|--:|
| Build | 1 | 198,699 | 21.7% |
| Verify | 3 | 179,680 | 19.7% |
| Rework | 2 | 535,347 | 58.6% |
| **Total** | **6** | **913,726** | |

- **Tool uses:** 276 · **Agent wall-clock:** ~37.2 min · **Tests:** 1728 → 1737.
- **Rework ratio** (rework ÷ (build + rework)): **0.73** — nearly ¾ of implementation spend was
  correcting the first pass.
- **Verify ROI:** the 179,680 verify tokens surfaced 2 rules-fidelity defects that would otherwise
  have shipped silently (a 100%+ skill that can never improve; trainable-past-75% skills).
- **Notable outlier:** the cap-wiring rework job (289,172) — a single-field data change — was the
  most expensive job in the layer, exceeding the original build. Semantic size ≠ token size.
- **Lever identified:** front-loading `rules-extractor` transcription *before* the build (as issue #40
  prescribed) would move rules-fidelity spend from the expensive rework column into the cheap build
  column. Layer 3 led with the build per user instruction; test this on the next layer.
