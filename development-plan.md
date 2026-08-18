# NoiRPG — Development Plan (Draft Outline)
*Draft v0.1 — 2026-08-17. Companion to `noir-rpg-framework.md` v0.1 and `design-review-notes.md`.*

## Guiding Principle

Sequence by risk, not by discipline. The framework is strongest where BRP already did the design work and thinnest at the three points where this becomes an original game: clue routing per build, narrative state discipline, and the interrogation minigame. Everything expensive (engine, art, writing at volume) waits until those three are validated on paper. The review's verdict is the plan's spine: *if interrogations are fun, the game works; if not, nothing else rescues it.*

## Phase 0 — Structural Decisions (design work, no code)

Resolve the open questions that shape everything downstream. Each produces a short written addition to the framework doc.

1. **Time pressure model** — decide whether neglected cases decay or wait. Listed last in the framework but structurally first: it determines how much multiplicative narrative state the project signs up for.
2. **Roll integrity model** — pick one: autosave-only/ironman default, pre-seeded rolls, or rolls resolved at scene entry. A decision, not a mitigation; leaving it open undermines the "rolls create consequence" pillar.
3. **Name the clue-routing rule** — every core clue must have either multiple skill routes or a guaranteed fallback path. Write it as a named, testable authoring rule (working name: *the Three Doors rule — every core clue reachable by at least two skill routes plus one skill-free fallback*), and acknowledge its per-clue authoring cost in the scope budget.
4. **Narrative state discipline rule** — the geography scope paragraph's missing twin: *a case may read other cases' outcomes at only N defined junction points* (propose N=2–3). This caps the write-and-QA state space before any case is authored.
5. **Distribution intent** — personal vs. commercial. Sets how formally the ORC Notice and licensing review are handled, and whether budget assumptions (VO, audio) are real line items.

Deferred on purpose: accent color (safe to defer, per review), platform/engine (decided in Phase 2, after prototypes).

**Exit criteria:** framework doc updated to v0.2 with all five decisions written in.

## Phase 1 — Paper & Spreadsheet Validation (cheapest possible tests of the riskiest systems)

Run in parallel; none requires an engine.

1. **Interrogation paper prototype** — the load-bearing minigame gets its own design doc plus a tabletop test: statement-by-statement play, Insight/Fast Talk vs. a suspect-composure meter, explicitly designing against the L.A. Noire failure mode (player reasoning misaligned with input verbs). Iterate until it's fun with index cards, or kill/redesign the structure.
2. **Advancement simulation** — script a full playthrough's worth of skill ticks across 8–12 cases and check whether the numbers move enough for the player to feel them. Output tunes the improvement-roll math and decides how much weight downtime training must carry.
3. **Case-board deduction paper test** — author one small case (3–4 suspects, ~a dozen evidence items) and run the pin-and-connect deduction loop on a corkboard, exercising the Three Doors rule with two different background builds to prove the routing rule works in practice.
4. **One case authored as pure data** — write the same test case in a structured format (YAML/JSON: nodes, clues, routes, junction points, decay hooks). This is the first draft of the case schema — the project's real production format — and will expose what the authoring rules missed.

**Exit criteria (go/no-go gate):** interrogation prototype is fun on paper; advancement math registers; one case exists as clean data. If interrogation fails here, return to design — do not proceed to engine work.

## Phase 2 — Vertical Slice (first code, smallest complete proof)

1. **Engine/platform choice** — decided now, informed by what the prototypes revealed. Requirements are modest by design: 2D presentation, heavy text/document rendering, shader work for the grayscale look, data-driven case loading. Evaluate against the desk-interface and typography demands, not action-game criteria.
2. **The slice:** one complete small case, playable start to finish — desk interface with core props, case board, one interrogation, 6–10 documents, the city map with 3–4 nodes, one location card, working d100 resolution with success grades, improvement rolls at case close.
3. **Art bible proof** — produce one real asset per layer (a graded scene, a degraded document, an ink-silhouette portrait) and enforce the three rules on them. This is where "every asset follows its layer's rule or is rejected" gets its first test.
4. **Case pipeline v1** — the slice's case must load from the Phase 1 data format, not be hand-wired. The tooling that authors cases *is* the production line; building it now is what makes Phase 3 writing-bound instead of engineering-bound.

**Exit criteria:** a stranger can play the slice case unaided and wants a second case. The desk/board/interrogation loop demonstrably alternates modes without fatigue.

## Phase 3 — Production (the real budget: writing)

1. **Case authoring at volume** — target 8–12 cases with intersections held to the junction-point rule. Every meaningful check gets an authored failure branch; the review is right that this, not art, is the dominant cost — plan and track it as such.
2. **Writing QA process** — state-space testing of branches, decay outcomes, and cross-case junctions; automated traversal of case data to find orphaned clues and Three Doors violations.
3. **Asset production** — the ~100-asset budget executed against the style bible; typography treated as a first-class asset alongside.
4. **Noir modules integration** — Composure/Corruption, vices and obligations, pursuit sequences — added after the core loop is proven, not before.
5. **Audio pass** — the deliberate budget priority: ambient sound design, selective VO (narrator as radio drama, voiced interrogation suspects). Scoped once distribution intent (Phase 0) sets the real budget.

## Phase 4 — Polish & Release

1. **Full-game balance pass** — advancement curve, downtime training weight, lethality tuning across all cases.
2. **Readability/accessibility pass** — hours of reading demands it: lifted blacks or soft reading mode, the gray-value hierarchy verified, final accent-color decision.
3. **ORC compliance** — ORC Notice drafted and reviewed; reserved-material boundaries (setting, story, art, code) documented; formality per the Phase 0 distribution decision.
4. **Release prep** — platform requirements, storefront, launch scope.

## Risk Register (top items)

| Risk | Mitigation | Where |
|---|---|---|
| Interrogation isn't fun | Paper prototype before any code; hard go/no-go gate | Phase 1 |
| Narrative state explodes | Junction-point rule decided before authoring; automated case-data QA | Phase 0, 3 |
| Clue rule breaks under builds | Named Three Doors rule; two-build paper test | Phase 0, 1 |
| Advancement invisible | Simulation before tuning; downtime layer as pressure valve | Phase 1 |
| Save-scumming nullifies rolls | Explicit roll-integrity decision | Phase 0 |
| Scope creep (city, art) | Existing framework rules enforced at asset/case review | All |

## Immediate Next Steps

1. Decide the time pressure model and the roll integrity model (Phase 0, items 1–2).
2. Draft the interrogation design doc and paper-prototype rules (Phase 1, item 1).
3. Write the advancement simulation script (Phase 1, item 2 — an afternoon of work, high information value).
