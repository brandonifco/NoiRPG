# 0008 — Layer 1 abilities

Status: Accepted

Issue: #29. Approved by the project owner on 2026-08-29 before implementation.
The owner also confirmed that the Phase 1 go/no-go gate has cleared for this work.

## Characteristic rolls and the skill floor

**Sourced interpretation:** Ch 5: System, "Skill Rolls" (p. 128), grants the
01–05 success floor to skills with a printed base chance of at least 5%.
"Characteristic Rolls" (p. 129) describes a separate kind of action roll.
We do not extend the skill-only floor to characteristic rolls.

The implementation composes the modifier pipeline with `SkillResolver` directly,
passing zero as its floor-only base-chance argument. This zero is an adapter
sentinel, not a claim that characteristics have a printed skill base chance.
The actual chance remains characteristic value times multiplier, modified through
the pipeline. This follows #27's existing interim approach without resolving its
Layer 2 API question or changing `ModifierChain.Resolve`.

**Sourced:** Ch 5, "Evaluating Success or Failure" and "Failure" (p. 127),
apply the five grades and 96+ failure rule to these action rolls. They do not
use the resistance-roll exception.

## Rules data

Create `src/Brp.Data/` in this issue. Characteristic definitions, the Damage
Modifier Table, and other shipped rules values belong in JSON rather than C#
constants, as required by AGENTS.md invariant 7. This is an architectural choice.
The table's source is Ch 2: Characters, "Damage Modifier (STR+SIZ, see table)"
(p. 13), including its open-ended continuation.

## Experience checks

**Sourced:** Ch 5: System, "Skill Improvement" (p. 138), explicitly excludes
characteristic rolls from the skill experience-check mechanism. Ability resolution
does not award an experience check. Characteristic improvement has separate rules
("Increasing Characteristics" and "POW Increases", pp. 139–140), outside #29.
The locked tick-on-use skill rule does not extend to characteristic rolls.

## Verification

The cited Chapter 5 passages and Chapter 2 damage table were checked against
`BasicRoleplaying-ORC-Content-Document.pdf` on 2026-08-29. The implementation must
reproduce every printed damage-table row and both sides of every band edge in tests.
