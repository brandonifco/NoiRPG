# 0009. Drop Fate Points

## Status

Accepted — 2026-08-29. Resolves #5.

## Context

**Sourced:** Ch 5: System, "Fate Points" (pp. 133–134) defines Fate Points as
uses of power points: rerolling percentile rolls, substituting a Difficult Luck
roll, ignoring damage, shifting success levels, maximizing weapon damage, and
introducing narrative elements. Each use spends power points at a printed or
gamemaster-determined cost.

**Sourced:** Ch 2: Characters, "Power Points (Max = POW)" (p. 15) defines power
points as a spendable resource used to cast or resist spells.

**Accepted project scope:** NoiRPG cuts that resource with Chapter 4; see ADR
0002. The printed Fate Point option therefore has no currency in this ruleset.

**Project design:** NoiRPG already addresses failed checks through authored
failure branches and the Three Doors clue-routing rule. ADR 0003 and the locked
roll-integrity decision make scene-entry results deterministic, so reloading is
not an alternate reroll mechanism. The tick-on-use advancement rule can record
meaningful skill use whether the check succeeds or fails.

## Decision

Fate Points are OFF. NoiRPG will not implement the printed power-point option and
will not rebase it onto Composure, POW, or a new Luck currency.

This is a **house scope decision**. The source book offers Fate Points as an
option; it does not prescribe whether a setting must use them. Dropping the option
preserves the established role of failure branches and avoids designing a second
resource economy before Composure exists.

## Consequences

**Sourced option excluded:** Ch 5, "Fate Points" (pp. 133–134) supplies the
reroll, Luck substitution, damage avoidance, grade shifting, maximum-damage, and
narrative uses. Because the option is OFF, none enters NoiRPG.

**Project-design consequences:**

- Failed rolls and adverse combat results remain binding within the established
  deterministic roll policy.
- Case design must continue to turn consequential failure into a changed
  situation rather than a blocked investigation. The Three Doors rule remains
  the protection against a single failed clue check stopping a case.
- Severe irreversible risks should be telegraphed and offer meaningful ways to
  prepare or choose another approach. This is a content-design consequence, not
  a replacement metacurrency.
- Composure remains a measure of psychological and moral deterioration. It is not
  consumed as tactical reroll fuel.
- Adding a player-controlled outcome currency later would require a new decision
  that supersedes this record and specifies its economy and interaction with
  deterministic rolls.

## Alternatives considered

The following are **project-design judgments**, not claims sourced from BRP.

**Rebase onto Composure.** Rejected. It would encourage treating psychological
deterioration as tactical fuel and would constrain a subsystem deliberately
deferred until the core loop is proven.

**Create a separate Luck or POW-derived pool.** Rejected. It adds a resource whose
main purpose overlaps authored failure handling and introduces balancing and
hoarding pressure, especially around lethal combat.

**Keep the decision deferred.** Rejected. Roll determinism is settled, and the
remaining Composure dependency is avoided by deciding not to use it as currency.
