# NoiRPG Roadmap — where we are and what to build next

**This is the ordered index for agents and agent teams.** It ties the two plans together
and points at the live work queue. It is **not** authoritative for mechanics or scope —
those live in the source docs and the book. Read this for *sequence and priority*, then
pick a `ready` issue and follow the work protocol in [`AGENTS.md`](AGENTS.md).

- **The live queue is GitHub Issues** (`AGENTS.md`: "the only work queue"). This file names
  the *next* issues; the tracker is the source of truth for what is open/ready/blocked.
- **Two source plans**, referenced not duplicated:
  - [`engine-implementation-plan.md`](engine-implementation-plan.md) §3 — the engine build,
    dependency-ordered by layer (Brp.*). *Structure authoritative; its formulas are not — take
    mechanics from the book (`AGENTS.md` invariant 2).*
  - [`development-plan.md`](development-plan.md) — the game, by phase (design → paper → slice →
    production → release).

---

## Two tracks

NoiRPG is an **engine** (`Brp.*`, a faithful Basic Roleplaying rules engine) with a **game**
(`Noir.*`, the original noir detective content) built on top. They are sequenced differently:

| | Engine track (`Brp.*`) | Game track (`Noir.*`) |
|---|---|---|
| Plan | `engine-implementation-plan.md` §3 | `development-plan.md` |
| Unit | dependency-ordered **layers** 0–5 | **phases** 0–4 |
| Ordering rule | Layer *n* never references Layer *n+1* | sequence by **risk**, not discipline |

---

## You are here

**Engine — Layers 0–3 complete; Layer 4 scaffold complete but with an objective completeness backlog.**

| Layer | What | Status |
|---|---|---|
| 0 | dice / entropy / resolution kernel / modifier pipeline | ✅ done |
| 1 | abilities: characteristics-as-data, rolls, recomputing derived values | ✅ done |
| 2 | skills: definitions, specialties, registry | ✅ data done |
| 3 | characters: aggregate, point-buy, tick-on-use experience | ✅ done |
| 4 | combat: range bands (#21), combat round (#47), attack/defense matrix (#49), gear, damage (#52), spot rules (#50), injury (#96), fumble tables (#97) | 🚧 scaffold done; **completeness backlog open** — see below |
| 5 | the noir game: cases, clue-routing, interrogation | ⬜ not started (design-led) |

**A 2026-08-31 engine-completeness audit** (six parallel domain auditors vs. the book + `orc-scope-filter.md`)
found Layers 0–1 and skill *data* complete and correct, but a real backlog of in-scope, book-derivable
gaps — several of them mechanics the scope filter/ADRs marked **ON** that were never built. So the engine
is **not** complete; "Layer 4 nearly done" was wrong. The backlog is objective (no playtesting); its live
contents live on GitHub (see below), not in this file.

**Game — Phase 0/1 partially done on paper** (interrogation design, a paper case, the case
schema, advancement sim), **Phase 2+ (game code) not started, and design-led (needs a human, not agents).**

---

## What to build next (ordered)

Two bodies of work remain, and they differ in *kind* — one is objective and closable
cold, the other is design-led. This section owns the **ordering and the reasons**;
GitHub owns the live list of what is open. (This file used to enumerate specific
issues as "still to build" and drifted the moment they merged — don't reintroduce
that. State lives in the issue tracker; meaning lives here.)

### 1. Complete the engine (objective, book-derivable — no playtesting; the audit's backlog)

This is ideal team work: engine-dev implements; rules-extractor / rules-conformance /
scope-warden verify. It needs no playtesting — where the book prints a table, that
table is the authority.

**The live backlog is on GitHub, not here:** the open `feature` issues labelled
`rules` / `data`. `tools/ready-issues.sh --ready` selects the next dispatchable one.
Work it highest-value-first — finish the injure → heal → advance spine (hit locations
and the remaining combat-completeness and advancement mechanics) before the lower-value
Ch 8 world cluster (equipment quality, gear↔skill, wealth, item HP, vehicles, drugs).
The issue labels and `ready-issues.sh` carry the per-item detail and current status.

### 2. Then Layer 5 — the game (design-led; the owner drives, agents support)

The noir game layer (`Noir.Rules` + `Noir.Scenario`) — case schema → code, the Three
Doors clue-routing engine, the narrative-state junction budget, and the **interrogation
minigame** — is the real risk and the actual product.

> **Read this before choosing.** `development-plan.md` is blunt: *"if interrogations are
> fun, the game works; if not, nothing else rescues it."* The engine is the
> well-understood, objective part; the game's three original systems (clue routing,
> narrative state, interrogation) are **design work that needs a human, not agents** —
> their entry point is a paper playtest only a person can judge. The epic and its
> sub-issues live on GitHub (labelled `scenario` / `noir`, and `needs-design` where a
> human gate applies).

**Recommended sequence:** finish the engine backlog first (objective, closable cold);
the Layer-5 game advances when the owner runs the Phase-1 playtest and drives the design.

---

## How an agent/team uses this file

1. Read this roadmap for **sequence and priority**, and `AGENTS.md` for the **rules of engagement**.
2. Pick a `ready` issue (`tools/ready-issues.sh --ready`), or the next one named above.
3. Follow the `AGENTS.md` work protocol: one concern, one branch, implement, verify with the
   roster ([`docs/agent-team.md`](docs/agent-team.md)), PR with `Closes #<n>`.
4. For rules work, the book is the source of truth (`docs/source-handling.md`); reproduce printed
   tables row-by-row; mark every decision **sourced** or **house rule**.

Keep this file current: when a layer/phase completes or the next issues change, update the
"You are here" and "What to build next" sections in the same PR that changes the state.
