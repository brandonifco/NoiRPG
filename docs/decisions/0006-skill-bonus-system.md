# 0006. Full Skill Category Bonuses, applied by subtraction

## Status

Accepted — 2026-08-29. Resolves #1.

## Context

The source offers two mutually exclusive optional systems for letting characteristics
influence skill ratings, and permits using neither:

- **Skill Category Bonuses** — each of the six skill categories has a primary
  characteristic (±1% per point from 10), one or two secondaries (±1% per 2 points,
  magnitude rounded down), and sometimes a negative one (inverted). Bonuses can be
  negative.
- **Simpler Skill Bonuses** — the category bonus is half the primary characteristic,
  rounded up. Always positive, roughly +5 at average characteristics.
- **Neither** — the skill rating is exactly the number shown.

This blocks Layer 2, because it changes how every skill's effective rating is computed.

It also collides with authored content. `tools/case_validator.py` holds three
background packages as concrete skill ratings, and `cases/SCHEMA.md` opens a skill
door at a `min_rating` of 40. Any bonus applied on top of the authored numbers shifts
ratings across that threshold. Measured before deciding: Simpler would have opened
the ex-cop's Law door and the ex-soldier's Streetwise door (both 35, both crossing 40
at +5), and the ex-cop's Fast Talk sits exactly on 40, so a negative bonus closes it.

The Overpass build audit has already needed one repair pass for the ex-soldier coming
out fallback-heavy, so silently perturbing door coverage was the main risk.

## Decision

**Use full Skill Category Bonuses.**

**Apply them by subtraction, not addition.** The authored ratings in
`case_validator.py` are treated as **final effective ratings**; base ratings are
derived:

```
base = effective - category_bonus
```

Characteristics come from the source's point-based creation at Normal power level:
every characteristic starts at 10, 24 points to spend, DEX/INT/POW cost 3 per point,
STR/CON/SIZ/CHA cost 1, reductions refund at the same rate.

`tools/skill_bonus.py` holds the characteristic sets, the category mapping, and the
formulas. `--check` asserts the point-buy totals are exact and that base plus bonus
reproduces every authored rating.

### Skill categories for the canonical 18

Taken from the source's own skill-list-by-category via each NoiRPG skill's book
equivalent. Three of these are counter-intuitive and were wrong in a first pass:

| Skill | Category | Note |
|---|---|---|
| Research | **Perception** | Not Mental. Likely the most-rolled skill in the game. |
| First Aid | **Mental** | Not Manipulation. |
| Drive | **Physical** | Not Manipulation. |

The rest: Streetwise, Law, Accounting → Mental. Insight, Spot → Perception. Fast Talk,
Persuade, Intimidate → Communication. Photography, Locksmith → Manipulation. Firearms,
Brawl → Combat. Shadow, Stealth, Dodge → Physical.

Intimidate is the only skill with no book equivalent; Communication is the natural home.

## Alternatives considered

**Neither.** Simplest, and the strongest fit for the transparency pillar — the
displayed percentage would be the whole truth with nothing derived behind it. Rejected
in favour of build differentiation: characteristics doing real work is what makes the
point-buy step a meaningful choice rather than a formality.

**Simpler Skill Bonuses.** Rejected: it is uniformly positive, so it adds a flat
inflation to every character rather than differentiating them, which buys the cost of
a bonus system without the benefit.

**Applying the bonus additively on top of the authored ratings.** Rejected. It would
have shifted two doors open in an already-audited case and forced a rebalance of
content that is currently validated.

## Consequences

- **Audited cases are unaffected by construction.** Effective ratings are unchanged,
  `case_validator.py` needs no edit, and the Overpass audit reproduces exactly —
  verified, including the ex-soldier's single intended fallback on cc3.
- **INT is the dominant characteristic and this needs watching.** It is primary for
  Communication, Mental, and Perception, and secondary for Combat and Manipulation —
  five of six categories, and 10 of the canonical 18 skills. In an investigation game
  where Mental and Perception skills carry the load, an INT-maximising build may
  dominate despite INT already costing 3 points. Revisit if playtesting shows builds
  collapsing toward one shape.
- SIZ only ever hurts (negative for Physical, nothing else), and CHA touches only
  Communication. Both are cheap at 1 point per point, which is roughly right.
- Layer 2 is unblocked. `SkillDefinition` must carry a category, and effective rating
  becomes base plus category bonus, recomputed whenever a characteristic changes.
- The engine must implement this as a ruleset-configurable policy, since ADR 0002
  keeps rules values as data. The other two options stay expressible.
