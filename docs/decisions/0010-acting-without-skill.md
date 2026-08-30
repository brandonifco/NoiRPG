# 0010. Acting Without Skill is off for core clues

## Status

Accepted — 2026-08-29. Resolves #6.

## Context

**Sourced:** Ch 3: Skills, "Acting without Skill" offers two options for
attempting a skill at 0% rating, if the gamemaster agrees success is possible:
*Skill Category Bonus* (use the category modifier as the base chance; an
experience check follows on success) and *Wild Chance* (a flat 1% try, or a
`POW×1` "Hail Mary" when blind luck applies, with no experience check). The
book lists it among the optional rules and warns, verbatim, that "Freak luck
might break suspension of disbelief in some games."

**Project design:** NoiRPG already guarantees that every core clue is reachable
without this rule. The **Three Doors rule** — specified in `cases/SCHEMA.md` and
enforced by `tools/case_validator.py` — requires each core clue to expose at
least two `skill` doors with distinct skills plus at least one `fallback` door.
Fallbacks are the deliberate skill-free path: never gated on a skill, and priced
in time, obligation, or decay rather than left free. `cases/overpass.yaml` is the
reference implementation, and `case-board-test.md` records a door-coverage audit
that caught and fixed a silently orphaned build.

So the access problem that Acting Without Skill would otherwise solve is already
solved structurally. A lucky roll on an untrained skill is precisely the
arbitrary outcome the clue rule exists to prevent, and it would route around the
guarantee the schema enforces.

## Decision

Acting Without Skill is **OFF for core clue access**. Neither option (Skill
Category Bonus as base chance, nor Wild Chance) grants a route to a core clue.
Core-clue reachability is provided solely by the Three Doors rule.

The narrower question — whether Acting Without Skill has any role for **non-core**
content (optional leads, texture, colour) — is **deferred**, not decided against.
It is revisited when a second authored case exists; a single case is not enough
evidence that texture content needs an additional access route.

This is a **house scope decision**. The book offers Acting Without Skill as an
option and does not prescribe that a setting must use it. Choosing OFF for core
access preserves the established role of the Three Doors rule.

## Consequences

**Sourced option excluded (for core access):** Ch 3, "Acting without Skill"
supplies the Skill Category Bonus and Wild Chance paths. Because the option is
OFF for core clues, neither reaches a core clue in any authored case.

**Project-design consequences:**

- Case authoring continues to satisfy the Three Doors rule for every core clue;
  the validator remains the mechanical guarantee. Acting Without Skill is not an
  admissible substitute for a required `skill` or `fallback` door.
- Untrained characters reach core clues through fallbacks, priced as fallbacks
  always are — not through a freak untrained success.
- The texture question stays open. If a later case demonstrates that optional,
  non-core content is starved without an untrained-access route, a new decision
  supersedes this one and specifies which option applies and where — bounded so
  it can never touch core clues.

## Alternatives considered

These are **project-design judgments**, not claims sourced from BRP.

**On for texture only, never for core clues.** Not rejected — deferred. It is the
live candidate for the open texture question, held until a second case gives
evidence for or against it.

**On generally, including core clues.** Rejected. It reintroduces the arbitrary
single-roll outcome the Three Doors rule was built to prevent and undercuts the
schema's reachability guarantee.

**Close the question entirely (OFF everywhere, permanently).** Rejected as
premature. Foreclosing texture use now decides with one case what wants more
evidence; deferral costs nothing because core access is already covered.
