# 0022. Skill category bonus: applied in the engine as base + category bonus, by subtraction for authored ratings

## Status

Accepted — 2026-08-31. Resolves #110. Applies the policy decided in ADR 0006 (which chose *full*
skill category bonuses, applied by subtraction), and follows the live-recompute precedent of ADR
0008 (derived characteristics) and the ruleset-as-data invariant of ADR 0002.

## Context

ADR 0006 decided *which* characteristic-to-skill system NoiRPG uses and settled the arithmetic, but
that decision lived only in `tools/skill_bonus.py` (a Python audit tool). The C# engine never
applied it: `CharacterBuilder.Build` set every skill to `printed base + professional + personal
points` with no category-bonus term, so **player-built characters silently lacked their category
bonuses**. No test caught the gap, because the only characters carrying category bonuses were the
authored background packages in `case_validator.py`, whose ratings are *already final-effective*
(base derived by subtraction) — so nothing was ever added on top of them to be wrong.

This record covers making the ADR 0006 policy real in the engine: a data-driven bonus policy, its
application to a character's effective rating, its live recompute on characteristic change, and the
rule that keeps it from being double-applied to authored ratings.

Only Ch 2: Characters, "Skill Category Bonuses (Option)" — the "Skill Category Modifiers" and
"Skill Bonus Table" — was consulted. The printed page numbers were re-verified against the PDF with
`pdftotext -f/-l`: "Skill Category Bonuses (Option)", the Skill Category Modifiers table, and the
Skill Bonus Table (values 1–20) are on **p.18**; the table's value-21 row, its `+1%/point` /
`+1%/2 points` continuation, and "Simpler Skill Bonuses" are on **p.19**. The prep note's "pp.18–19"
holds. The chapter's own worked example is reproduced end to end in test.

## Decision

### The formula — sourced (Ch 2, "Skill Category Bonuses (Option)", pp.18–19)

For a characteristic measured from a neutral value of 10:

- **Primary** characteristic: +1% for every point over 10, −1% for every point under 10.
- **Secondary** characteristic: +1% for every **2** points over 10, −1% for every 2 points under
  10, **magnitude rounded down** (−3 points → −1, not −2).
- **Negative** characteristic: an **inverted primary** — subtracted rather than added (+1% per point
  *under* 10, −1% per point over).

The category bonus is the sum across a category's primary, secondaries, and negatives. It can be
negative. This is the "full" option; ADR 0006 rejected the "simpler" (½ primary, always positive)
and "neither" (bonus always 0) options.

### The category → characteristic table — sourced (the printed "Skill Category Modifiers", p.18)

| Category | Primary | Secondary | Negative |
|---|---|---|---|
| Combat | DEX | INT, STR | — |
| Communication | INT | POW, CHA | — |
| Manipulation | DEX | INT, STR | — |
| Mental | INT | POW, EDU | — |
| Perception | INT | POW, CON | — |
| Physical | DEX | STR, CON | SIZ |

Mental's second secondary is **EDU** per ADR 0006 (the book prints the slot; NoiRPG fills it with
the optional Education characteristic). The book's worked example leaves EDU "not used", which is
equivalent to EDU at the neutral value 10 (a zero contribution).

### Where it lives — data + a Core policy

- **Data** (`src/Brp.Data/skill-category-bonus-ruleset.json`): the neutral value (10), the two
  divisors (1 for primary/negative, 2 for secondary), and the category→characteristic map. Nothing
  is a hardcoded C# constant (AGENTS.md invariant 7). `NoirSkillCategoryBonusRuleset.Load()` builds
  it.
- **Policy** (`Brp.Core.Skills.SkillCategoryBonusRuleset`): `BonusFor(SkillCategory, AbilitySet)`
  computes the bonus. It is data-configured, so ADR 0006's "neither" option is expressible (an
  all-neutral or empty map yields a zero bonus); "simpler" would be a different policy shape and is
  not built, matching ADR 0006's choice. No game-engine dependency (invariant 6).

### How it is applied — effective rating = base + category bonus, recomputed live

`CharacterSkill.CurrentRating` remains the character's **base** rating (printed base + points spent,
moved only by advancement). The effective rating is computed on read:

```
effective rating = CurrentRating + BonusFor(Definition.Category, abilities)
```

exposed as `CharacterSkill.EffectiveRating(abilities, bonuses)` and
`Character.EffectiveRating(skillId, bonuses)`. Because the bonus is read live from the mutable
`AbilitySet`, the effective rating **recomputes whenever a characteristic changes** — the same
"changes immediately" treatment ADR 0008 gives hit points (Ch 2 p.13). `CharacterBuilder` therefore
does **not** bake the bonus into `CurrentRating`; baking it would freeze it against later drain.

### No double-apply to authored ratings — by subtraction (ADR 0006)

Authored content (background packages) treats its numbers as **final effective ratings** and stores
the base by subtraction: `base = effective − category bonus`. The seam is
`CharacterSkill.FromEffectiveRating(definition, effectiveRating, abilities, bonuses)`. Reading
`EffectiveRating` back then reproduces the authored number exactly — the bonus is never added a
second time. This preserves the audited door coverage ADR 0006 protected: adding the engine bonus
does not shift a single authored rating.

## Consequences

- Player-built characters now get their category bonuses; the #110 gap is closed. The base rating,
  the number advancement moves, is unchanged — advancement still rolls against `CurrentRating`
  (base), not the effective rating; whether improvement should roll against the effective rating is
  a separate question left to a future issue, not decided here.
- The effective rating is now *exposed* but not yet *consumed* by a roll pipeline: `SkillRoll.Resolve`
  still takes its effective chance from the caller (the pre-existing state), so a future glue layer
  can feed it `Character.EffectiveRating`. Nothing in this issue changes the CLI.
- A bonus can be negative and, for a low-base skill, can push an effective rating below 0. The value
  is returned unclamped, matching `tools/skill_bonus.py`; a sub-zero chance simply always fails at
  resolution.

## Verification

`Brp.Data.Tests.NoirSkillCategoryBonusRulesetTests` reproduces both printed tables in full — the
Skill Category Modifiers map (all six rows) and the Skill Bonus Table (values 1–21 × primary /
secondary / negative columns) — plus the chapter's worked example, matching `tools/skill_bonus.py`.
`Brp.Core.Tests` covers the policy's validation and round-down rule. `Brp.Rules.Tests` covers a
player-built character's effective rating, recompute on drain, and the authored no-double-apply
seam.
