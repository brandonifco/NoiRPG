# NoiRPG — Agent Operating Contract

Modern-day noir detective RPG. A C#/.NET rules engine implementing Chaosium's
Basic Roleplaying (BRP), plus a game and gamemaster-tooling layer on top.

Read this file, then the one Issue you are working. Do not read the whole repo.

For where the project is and what to build next, see [`ROADMAP.md`](ROADMAP.md) — the
ordered index of layers, phases, and the next issues.

## Source-of-truth documents, in order

| Document | Authority over |
|---|---|
| `BasicRoleplaying-ORC-Content-Document.pdf` | **The rules.** The only valid source for mechanics. |
| `orc-scope-filter.md` | **What we implement and what we cut.** ~60% of the book is out of scope. |
| `docs/source-handling.md` | How to extract from the book, the verification discipline, and known errata in it. |
| `engine-implementation-plan.md` | Historical design context and the build-layer map. **Not authoritative for mechanics** — see the warning below. |
| `noir-rpg-framework.md` | Game design: setting, tone, structure, art direction. |
| `design-review-notes.md` | Known open design risks. |
| `docs/decisions/` | Durable architectural decisions. Linked from Issues, not copied. |
| GitHub Issues | Current and future work. **The only work queue.** |

If two of these conflict, the higher row wins, and the conflict is a bug — file an Issue.

## Safety-critical invariants

1. **`BRP SRD 1.0.2.pdf` is not our source.** It is a different, superseded 2020
   document with a different resolution table and only four success grades. It is
   gitignored. If you find a copy, do not read it for mechanics.
2. **`engine-implementation-plan.md` is not authoritative for mechanics.** It predates
   the source-text decision and has been found wrong on three separate topics — the
   modifier ordering, the weapon range bands (wrong in every particular, including a
   rule that does not exist), and resistance rolls (one line cites a section number from
   the superseded book). Its architecture and build-layer material is still useful. Its
   formulas are not. Take mechanics from the book.
3. **The scope filter is binding.** No magic, sorcery, psychic powers, superpowers,
   mutations, fantasy weapons, spacecraft, or monsters. If an Issue seems to require
   out-of-scope content, stop and ask rather than implementing it.
4. **Modern era baselines, not historical.** Several BRP skills carry two base
   chances. Always take the modern value. See `orc-scope-filter.md`.
5. **All randomness is injected and seeded.** No `System.Random` statics, no
   `DateTime.Now` in the core. Same seed plus same call sequence must produce a
   byte-identical roll log. This is load-bearing for tests, replay, and balance
   simulation — not a style preference.
6. **`Brp.Core` and `Brp.Rules` take no game-engine dependency.** No Unity, Godot,
   or MonoGame references.
7. **Rules values are data, not constants.** Numbers from the book belong in
   ruleset JSON under `src/Brp.Data/`, not hardcoded in C#.

## Rules-conformance rule

Any change implementing a mechanic must cite the chapter and section it comes from,
and where the book prints a table, the test suite must reproduce that table exactly
rather than spot-checking it. Derived formulas are verified against every printed
row, not a sample. `docs/source-handling.md` has the extraction recipe, the known
errata, and the defect classes that keep recurring — read it before touching rules code.

Two conventions you will meet and should follow:

**Sourced or house rule.** Every mechanical claim in a decision record either cites the
chapter it was verified against, or says plainly that the book is silent and the choice
is ours. An unmarked assertion is a defect regardless of whether it happens to be true —
two rewrites on #11 came from one sitting unnoticed beside verified claims.

**Pinning tests.** A few tests deliberately assert behaviour that is correct in one
context and wrong in another, and say so in their name and comment. The test *continuing
to pass* is the alarm: it means someone reused the narrow thing somewhere it does not
belong. `ResolutionPolicyTests.Skill_rolls_fail_at_96_even_at_full_chance_which_is_why_resistance_is_a_separate_path`
is the worked example. Do not "fix" one of these; understand why it exists first.

## Work protocol

One concern, one Issue, one branch, one pull request.

1. Pick one Issue labelled `ready`.
2. Branch from `main`.
3. Implement only that Issue.
4. Open a PR with `Closes #<n>` — a real issue number, never a blank `#`. This holds
   for every PR, docs and tooling and orchestration included; `pr-policy` rejects a
   body with no `Closes #<n>` / `Fixes #<n>`.
5. Automated checks and review.
6. Merge; the Issue closes automatically.

If you discover unrelated work, file a separate Issue. Do not enlarge the current one.

## State of the code

The BRP engine is built through Layer 4. `Brp.Core` (Layers 0–2) holds seeded dice
(`Dice/`, `Randomness/`), the five-grade resolution kernel (`Resolution/`), the modifier
pipeline (`Modifiers/`), resistance and opposed rolls (`Contests/`), data-defined
characteristics with live-recomputing derived values (`Abilities/`), and the skill system
(`Skills/`). `Brp.Rules` (Layers 3–4) adds characters — point-buy creation and tick-on-use
experience — and combat: range bands, the combat round, the attack/defense matrix, gear,
damage/wounds, spot rules, injury, and fumble tables (`Combat/`). `Brp.Data` supplies the
ruleset JSON. The solution has ~2,168 tests, including printed tables reproduced cell by cell.

`tools/Brp.Cli` is the `brp` command line over that kernel — one command, `roll`, which
resolves a check and prints its whole derivation. It has its own `AGENTS.md`.

**What's left — the engine is NOT complete.** A 2026-08-31 completeness audit found Layers 0–1
and skill *data* correct, but a real backlog of in-scope, book-derivable gaps — several marked ON
by the scope filter/ADRs yet never built. See [`ROADMAP.md`](ROADMAP.md) for the ordered backlog;
the high-priority ones: skill category bonus not applied in the engine (#110), Major Wounds effect
(#111), hit locations (#112), healing/recovery (#109). Layer 5 — the noir game (cases, clue-routing,
interrogation, #98) — has not started and is design-led. `engine-implementation-plan.md` §3 has the
layer map — the *structure* there is sound even though its formulas are not.

## Commands

```
dotnet build
dotnet test
dotnet format --verify-no-changes
```

Target framework: **`net10.0`** (current LTS). The SDK version is pinned in `global.json`
and is the single source of truth for both local builds and CI. See
`docs/decisions/0005-target-framework.md`.

Update this section in the same PR that changes the commands.

## Locked decisions

These are settled. Do not reopen them inside an implementation PR:

- **Roll integrity: pre-seeded at scene entry.** Reloading replays the same result.
- **Advancement: tick-on-use** — a deliberate deviation from BRP RAW, validated by
  `tools/advancement_sim.py`. A skill ticks when exercised under real stakes whether
  the roll succeeded or failed; the improvement roll at case close still gates the gain.
- **Case decay: junction-only.** At most 3 junctions per case.
- **Clue routing: the Three Doors rule**, machine-enforced by `tools/case_validator.py`.
- **The canonical skill list is the framework's 18 names** (Streetwise, Shadow,
  Intimidate, Locksmith, and so on), as hardcoded in `tools/case_validator.py`. Do not
  rename them to the source book's names — existing tooling depends on these.

## Agent team

Work is routed to the smallest model that can do it correctly. See
`docs/agent-team.md` for the roster and routing rules, and `docs/decisions/0004-agent-team.md`
for why. In short: cheap gates before expensive ones, escalate on risk rather than
diff size, verification agents get read-only tools, and Codex is a cross-vendor
verification instrument rather than a second workhorse.

## Where deeper guidance lives

Subsystem-specific instructions belong in `AGENTS.md` files next to the code they
govern, not here. Keep this file short.
