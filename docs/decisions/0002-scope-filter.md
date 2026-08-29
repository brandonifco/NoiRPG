# 0002. Cut roughly 60% of the source book

## Status

Accepted — 2026-08-29

## Context

The ORC Content Document is 303 pages covering every genre BRP supports: fantasy,
science fiction, superheroes, horror, historical. NoiRPG is modern-day noir with no
magic and no unrealistic technology.

Measured against the extracted text: the Powers chapter is 13.5% of the book,
Equipment 32.8% (mostly pre-modern and science-fiction gear), and Creatures 17.9%
(a fantasy bestiary).

## Decision

Adopt `orc-scope-filter.md` as binding scope. Chapter-level verdicts and the
optional-rule toggle list live there and are maintained there, not duplicated here.

Headline cuts: the Powers chapter entirely; about 80% of Equipment; about 90% of
Creatures. Roughly 40% of the book becomes engine work.

## Alternatives considered

**Implement the engine completely, then configure NoiRPG as one setting on top.**
Rejected. It is the more general design and would be right for a commercial BRP
engine, but it more than doubles the work for a solo project and front-loads all of
it into subsystems this game will never call.

**Cut nothing formally; just don't get around to the unused parts.** Rejected.
Without a written boundary, scope decisions get re-litigated every session, and
agents implement plausible-looking out-of-scope mechanics because nothing said not to.

## Consequences

- Deleting content is deleting ruleset data rows, not editing code — which is why
  rules values must be data (see `AGENTS.md`).
- Cutting Powers forces scattered downstream deletions: power points as a spendable
  pool, the Projection skill, powered equipment, and Fate Points (which are built on
  the power-point economy and would need re-basing to survive).
- POW survives as a characteristic — it drives the Luck roll and POW-vs-POW
  resistance rolls — even though the power-point pool does not.
- Modern-era skill base chances are taken over historical ones throughout.
