# 0003. All randomness is seeded, injected, and logged

## Status

Accepted — 2026-08-29

## Context

The game is percentile-dice driven and the design commits to showing the player the
real probability of every check. Four separate needs push toward the same mechanism:

1. Balance questions the design cannot answer analytically — most urgently, whether
   experience-based advancement moves skill ratings enough to be felt over 8–12
   cases (`design-review-notes.md` §5).
2. Reproducible bug reports from a shipped game.
3. Regression tests over a rules engine whose behavior is inherently stochastic.
4. The save-scumming problem (`design-review-notes.md` §4). Lethal percentile
   systems invite reloading before a bad roll.

## Decision

All randomness flows through an injected, seedable, serializable-state entropy
source. No `System.Random` statics and no ambient time in `Brp.Core` or `Brp.Rules`.
Every roll is appended to an event log carrying sequence number and context.

Invariant: **the same seed plus the same call sequence produces a byte-identical
roll log.**

## Alternatives considered

**Ambient static RNG.** Rejected: makes tests order-dependent and replay impossible.

**Determinism only in test builds.** Rejected: the shipped game is exactly where
reproducible bug reports and any anti-reload policy are needed.

## Consequences

- The save-scumming policy — pre-seeded rolls, autosave-only, or resolution at scene
  entry — becomes a configuration choice rather than an architectural change. The
  engine supports all three; the game must still pick one.
- Monte-Carlo balance simulation is nearly free once the kernel exists, and should
  run before the combat layer is written so its results can still change the design.
- Every roll result must carry full provenance — base chance, each modifier applied,
  effective chance, thresholds, grade — not a bare boolean.
