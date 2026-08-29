# Architecture Decision Records

Short records of durable choices. Link them from Issues and PRs rather than
restating them.

### The sourced / house-rule convention

Every mechanical claim in a record must be marked one of two ways:

- **Sourced** — cite the chapter it was verified against. Not "the book says", but which
  chapter, checked when the record was written.
- **House rule** — state plainly that the book is silent and the decision is ours, and
  give the reasoning.

An unmarked assertion is a defect even when it happens to be right. ADR 0007 was rewritten
twice because unchecked claims sat beside verified ones with nothing on the page telling
them apart; both failures are preserved in that record. ADR 0007 is the worked example of
the convention.

One file per decision, numbered. Status is `Proposed`, `Accepted`, or
`Superseded by NNNN`. A decision that turns out wrong gets a new record that
supersedes it — the original is not edited or deleted.

| # | Decision | Status |
|---|---|---|
| [0001](0001-source-text.md) | ORC Content Document is the sole rules source | Accepted |
| [0002](0002-scope-filter.md) | Cut ~60% of the source book | Accepted |
| [0003](0003-deterministic-rolls.md) | All randomness seeded and logged | Accepted |
| [0004](0004-agent-team.md) | Model-routed agent team with cross-vendor verification | Accepted |
| [0005](0005-target-framework.md) | Target net10.0, pinned via global.json | Accepted |
| [0006](0006-skill-bonus-system.md) | Full Skill Category Bonuses, applied by subtraction | Accepted |
| [0007](0007-modifier-pipeline.md) | Modifier ordering, and difficulty that does not stack | Accepted |
| [0008](0008-abilities.md) | Layer 1 abilities: floor eligibility, rules data, and experience checks | Accepted |
