# Design Framework Review Notes
*Review of `noir-rpg-framework.md` v0.1 — 2026-08-17*

## Overall Assessment

A strong v0.1 — coherent, honest about its risks, with real scope discipline. The ~100-asset budget with Her Story / Papers, Please / Golden Idol / Obra Dinn as precedents is the right comparison class, and the ORC licensing analysis is accurate (including the detail that Call of Cthulhu and RuneQuest are not covered). The three-rule art bible is the best part of the doc: each rule is cheap to enforce, and "every future asset either follows its layer's rule or is rejected" is exactly how a small project keeps a look coherent.

The pattern in the gaps: the doc is strongest where BRP already did the design work, and thinnest at the three points where this stops being an adaptation and becomes an original game — clue routing per build, narrative state discipline, and the interrogation minigame.

## 1. The clue rule and the build system are in tension

The doc says "having the right skill in the right place always yields the clue" — but the protagonist is a point-buy build who may **not have** the right skill. GUMSHOE can make that guarantee because every investigator has every investigative ability at some level; BRP builds don't.

Each core clue therefore needs either:
- multiple skill routes (the ex-accountant finds it in the ledger, the ex-cop gets it from a contact), or
- a guaranteed fallback path.

That's a per-clue authoring cost the framework doesn't acknowledge. Solvable, but it should be a **named rule**, because it's where the elegant clue rule collides with "replayability comes from builds."

## 2. The real budget is writing, not art

"Failure is a branch, not a wall" is the right philosophy, but every branch is authored content: each meaningful check needs a failure state that's story, not a retry. Add cross-case intersections (witness in one case, suspect in another) and case decay, and the state space to write **and QA** grows multiplicatively.

The doc has a scope-discipline paragraph for geography ("map of nodes, never modeled streets"). It needs the equivalent paragraph for narrative state — something like: *a case may read from other cases' outcomes at only N defined junction points.* That constraint is the one that will save the project.

## 3. Interrogation is the load-bearing minigame and gets two sentences

Turn-based, statement-by-statement, Insight/Fast Talk vs. a composure meter — that's the pitch, not the design. This is the mode players will repeat most, and the one with the worst failure precedent (L.A. Noire's truth/doubt/lie problem: the player's reasoning and the input verbs didn't line up).

Before committing to the structure, this deserves its own design doc and a **paper prototype**. If interrogations are fun, the game works; if they're a slot machine with a meter, nothing else in the framework rescues it.

## 4. Dice plus reloading needs a decision, not just mitigation

The clue rule and narrative-failure design remove the *need* to save-scum, but not the *ability* — a player can still reload before a big Fast Talk roll, and percentile systems make that temptation constant. Notably, every precedent the doc cites is deterministic.

Options (any works; leaving it unaddressed undermines the "rolls create consequence" pillar):
- autosave-only / ironman default
- pre-seeded rolls
- rolls resolved at scene entry

## 5. Advancement may be nearly invisible at video-game length

The experience-check system is elegant over a 40-session tabletop campaign. Over 8–12 cases, a skill might climb 15 points total — will the player ever feel it? The case-closed improvement-roll screen is a nice dramatic beat, but the math should be checked: **simulate a playthrough's worth of ticks** and see whether the numbers move enough to register. If not, the downtime-training layer needs to carry more weight than the doc currently gives it.

## Smaller notes

- The **time pressure model** is listed last among open questions but is actually structural — case decay determines how much of the multiplicative state space from point 2 the project signs up for. Resolve it early.
- The **accent color** question is safe to defer exactly as the doc says.
