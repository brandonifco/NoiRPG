# 0001. ORC Content Document is the sole rules source

## Status

Accepted — 2026-08-29

## Context

Two Chaosium documents were in the repository at different points:

- **BRP SRD 1.0.2** (2020, 23 pp.), under the BRP Open Game License.
- **Basic Roleplaying ORC Content Document** (2023, 303 pp.), under the ORC License —
  essentially the full text of *Basic Roleplaying: Universal Game Engine*.

They are not interchangeable. The 2020 SRD has four degrees of success; the ORC
document has five, adding critical success. Their success-threshold tables are
banded differently and yield different results for the same skill rating. The 2020
SRD also lacks major wounds, hit locations, chases, fatigue, Passions, Allegiance,
and Augments.

The licenses differ materially. BRP-OGL carries a Prohibited Content list that
excludes Sanity and Passions, and requires displaying a BRP logo. ORC has neither
restriction, and Sanity and Passions are inside its licensed text.

## Decision

The **ORC Content Document is the sole source** for mechanics.

`BRP SRD 1.0.2.pdf` is gitignored and treated as superseded. It is not deleted from
local working copies, but it must not be read for mechanics.

## Alternatives considered

**Build from the 2020 SRD.** Rejected: it is far smaller and simpler, which is
superficially attractive for scope, but it lacks the mechanics the game design
already depends on (persistent injuries, Passions for vices/obligations, Sanity as
the basis for Composure), and its license restricts exactly those.

**Keep both, take the simpler rule where they differ.** Rejected: silently mixing
two rule sets produces an engine that matches neither, and makes conformance
testing meaningless.

## Consequences

- Composure/Corruption derives from Sanity; vices and obligations derive from
  Passions. Both are licensed content and need re-skinning, not original design.
- The characteristic is **CHA**, not APP.
- Resolution kernel formulas are those in `engine-implementation-plan.md` §2,
  verified against all 24 rows of the Skill Results Table.
- Any test fixture written against the 2020 SRD must be discarded, not adapted.
- Attribution: an ORC notice plus a "Powered by BRP" credit. No logo obligation.
- The source book is 303 pages against a game that needs perhaps 40% of it, which
  makes scope control the dominant project risk. See [0002](0002-scope-filter.md).
