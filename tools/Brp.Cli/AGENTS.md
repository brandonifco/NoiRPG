# `brp` — command-line tooling

A gamemaster-facing shell over `Brp.Core`. Four rules govern it.

**The rendered output is the contract.** It is read by a person, pasted into Issues, and
diffed. `RollCommandTests.Acceptance_invocation_prints_the_whole_chain_and_the_graded_outcome`
pins it as an exact string. Changing a line of the report means changing that test on
purpose, which is the point — not an obstacle to route around.

**The CLI computes no rules.** Every number it prints comes from the kernel: the derivation
is read off `ModifierChain.Contributions`, and the outcome bands are produced by asking
`SkillResolver` to grade all 100 possible rolls and collecting the runs. Re-deriving a
threshold here would create a second implementation of the resolution table that no
conformance fixture guards. If a value cannot be obtained from `Brp.Core`, that is a gap in
`Brp.Core`.

**A rating is not a base chance.** `--skill` is the character's rating, which the modifier
chain starts from. `--base-chance` is the skill's printed starting value, and the only thing
that reads it is the 5% floor (Ch 5: System, "Skill Rolls" — "any skill which normally has a
base chance of 5% or higher... even if difficulty, conditional modifiers, or other factors
reduce the skill rating below 5%"). They default to the same number, which is correct for
every skill printed at 5% or above and wrong for the in-scope ones printed at 01% — Science,
Strategy, Martial Arts. Do not collapse them back into one input.

**No implicit seed, and no clock.** `--seed` is required. A tool whose whole purpose is
showing how a result was produced cannot be the one place in the engine where a result
cannot be reproduced from what was typed (AGENTS.md invariant 5, ADR 0003).

**72 columns.** Enforced by `RollBandTests.The_report_fits_a_terminal_seventy_two_columns_wide`.
It leaves room for the indentation a diff or an Issue comment wraps around the output.
