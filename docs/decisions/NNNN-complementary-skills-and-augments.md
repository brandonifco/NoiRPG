# NNNN. Complementary Skills and Augments are two mechanics, not one

## Status

Accepted — 2026-09-01. Resolves #114.

## Context

Ch 3: Skills, "Augments and Complementary skills" (p.34), prints one prose section covering
two distinct mechanics, and the book's own front-matter Optional Rule Checklist (header p.229;
the "Skills" subsection listing this entry is p.230) lists only one toggle for both —
**"Complimentary Skills"** — under Skills. `orc-scope-filter.md` turned that single checklist
entry ON, glossing it as "(+1/5 rating)".

Issue #114 asked for a single mechanic combining the +1/5 numeric bonus with an experience
rule ("a skill used to augment gets no experience check if the primary roll fails"). Read
against the book, those two clauses belong to two different sub-rules in the same section that
cannot both be true of one mechanic:

- **Complementary Skills** (p.34): "your character may temporarily add 1/5 of your rating in a
  complementary skill to your rating in another skill for skill rolls." The helper skill is
  never rolled — it contributes a flat number. Experience: "If the main skill roll is a
  success, your character receives an experience check only to the main skill, not to the
  complementary skill used." Nothing conditions this on the primary's outcome, because the
  helper is never itself checked for experience in the first place.
- **Augment** (p.34): "you can attempt a roll of one complementary skill to support, or
  augment, another primary skill roll." A success shifts the primary roll's difficulty one
  step easier; a failure shifts it one step harder ("only one degree of adjustment is
  possible"). Experience: "If successful with the augmenting skill roll, you may check it for
  experience as normal, as well as with the primary skill. If the primary roll fails, the
  augmenting skill does not receive an experience check." This is the sentence Issue #114's
  audit finding actually describes — it only makes sense for a mechanic where the helper skill
  is itself rolled.

Both sentences are true of the book; the issue's numeric claim (+1/5) and its experience claim
(gated on primary failure) each cite a different one. Because the book's own checklist treats
"Complimentary Skills" as the one optional rule governing this whole section, and both
sub-rules are explicitly mutually exclusive per use ("You cannot augment a skill and use a
complementary skill bonus simultaneously for the same skill roll"), the resolution is to
implement both, faithfully and separately, rather than picking one and silently dropping half
of the issue's stated scope.

## Decision

Two independent, book-cited mechanics under `Brp.Rules.Skills`:

- **`ComplementarySkill.Bonus`** — the static +1/5 fraction. Returns a `Permanent`
  `AdditiveModifier` so it composes through the existing `ModifierPipeline` (ADR 0007) exactly
  like the textually identical First Aid fraction (`docs/decisions/0023-healing-and-recovery.md`).
  The helper skill is never rolled, so nothing calls `ExperienceSystem.RecordUse` for it — the
  book's "not to the complementary skill used" falls out of the architecture rather than
  needing its own gate.
- **`Augment.DifficultyShift`** — the roll-based one-step difficulty shift. Returns a
  `DifficultyModifier` (Easy on success, Difficult on failure), which composes through the same
  pipeline's existing non-stacking difficulty state (ADR 0007) — "only one degree of adjustment
  is possible" needs no bespoke arithmetic, because the pipeline already collapses any number
  of same-direction difficulty sources into one step and cancels opposite ones pairwise.
- **`ExperienceSystem.RecordAugmentUse`** — the additive experience gate: refuses unconditionally
  when the primary roll failed, otherwise defers to the ordinary `RecordUse` with the augment's
  own success as the `succeeded` argument (see that method's XML doc for how the two
  `ExperiencePolicy` values are reused, not reimplemented, in that fallthrough).

### Rounding — house rule

The book's own worked example (Medicine 65% + Science (Pharmacy) 40% → +8%) never exercises a
remainder (40/5 is exact), and prints no rounding rule for the general case. `ComplementarySkill.Bonus`
rounds down, matching the choice already made for the identical fraction in
`docs/decisions/0023-healing-and-recovery.md`, so the two occurrences of "1/5 of a skill rating"
in this engine round identically rather than drifting.

### Chapter citation — corrected

Issue #114 cited "Ch 5 optional rules." The section is Ch 3: Skills, p.34 (confirmed via
`tools/source-slice.py --pages 34 --expect "Augments and Complementary"`), immediately after
the Alphabetical Skill List and immediately before "Skill Ratings Above 100%." ADR 0007 already
cited Augments correctly as "Ch 3" when using it as supporting evidence for the non-stacking
difficulty rule; this record aligns the new code with that citation rather than the issue's.

## Consequences

- `Brp.Data.NoirComplementarySkillsRuleset` loads the 1/5 fraction from
  `complementary-skills-ruleset.json` (AGENTS.md invariant 7: rules values are data). The
  Augment difficulty shift carries no numerator/denominator of its own — same precedent as
  every other `DifficultyModifier` (see its remarks) — so there is nothing to load for it.
- Neither mechanic is wired into a specific skill pair (e.g. Knowledge (Accounting) assisting
  Research, `orc-scope-filter.md`'s example). This issue is the general primitive; wiring
  specific skill pairs into scene/case content is Layer 5 work, out of scope here.
- Choosing which of the two mechanics applies to a given roll, and which single helper skill
  supplies it, remains a caller/scenario decision — the book leaves it to the gamemaster
  ("Many complementary uses are noted in the skill descriptions that follow... you and the
  other players will doubtless devise more"), and there is no gamemaster at runtime here either.

## Known limitations

- `Augment.DifficultyShift` does not itself enforce "only one degree of adjustment is possible"
  or the complementary/augment mutual exclusion — both already fall out of ADR 0007's
  non-stacking difficulty state and of a caller supplying at most one modifier per roll, so no
  separate guard was added, but nothing here would catch a caller that (incorrectly) passed
  both an augment shift and a complementary bonus for the same roll and expected the book's
  named exclusivity rule to error rather than simply compose harmlessly.
