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
| 2 | skills: definitions, specialties, registry (category-bonus *application* is a gap — #110) | ✅ data done |
| 3 | characters: aggregate, point-buy, tick-on-use experience | ✅ done |
| 4 | combat: range bands (#21), combat round (#47), attack/defense matrix (#49), gear, damage (#52), spot rules (#50), injury (#96), fumble tables (#97) | 🚧 scaffold done; **completeness backlog open** — see below |
| 5 | the noir game: cases, clue-routing, interrogation | ⬜ not started (design-led) |

**A 2026-08-31 engine-completeness audit** (six parallel domain auditors vs. the book + `orc-scope-filter.md`)
found Layers 0–1 and skill *data* complete and correct, but a real backlog of in-scope, book-derivable
gaps — several of them mechanics the scope filter/ADRs marked **ON** that were never built. So the engine
is **not** complete; "Layer 4 nearly done" was wrong. The backlog is objective (no playtesting) and tracked
below.

**Game — Phase 0/1 partially done on paper** (interrogation design, a paper case, the case
schema, advancement sim), **Phase 2+ (game code) not started, and design-led (needs a human, not agents).**

---

## What to build next (ordered)

### 1. Complete the engine (objective, book-derivable — no playtesting; the audit's backlog)

All of these are ideal team work (engine-dev implements; rules-extractor / rules-conformance /
scope-warden verify — the workflow that shipped #50/#96/#97).

**🔴 High** — the four that stand between here and a whole injure→heal→advance engine:
- **[#110] Skill category bonus** applied in the engine (ADR 0006 mandates it; today it lives only in a Python tool, so player-built characters silently lack it).
- **[#111] Major Wounds effect** + a damage amount on `Wound` (only the *threshold* exists).
- **[#112] Hit locations** — formally decided ON (#4), only string scaffolding exists.
- **[#109] Healing / recovery** — First Aid, natural healing (flat 1D3/week), characteristic restoration.

**🟡 Medium:**
- **[#113]** special-damage *effects* (crushing stun, impaling lodged) + Fighting Defensively.
- **[#114]** complimentary/augment skills (+1/5).
- **[#115]** advancement: Research (self-study) + the default-+3 gain option.

**🟢 Low-Med:**
- **[#116]** Ch 8 world cluster — equipment quality, gear↔skill, wealth, item HP, vehicles, drugs.

### 2. Then Layer 5 — the game (design-led; the owner drives, agents support)
- **[#98] Epic: the noir game layer (`Noir.Rules` + `Noir.Scenario`)** — case schema→code, the
  Three Doors clue-routing engine, narrative-state junction budget, and the **interrogation
  minigame**.

> **Read this before choosing.** `development-plan.md` is blunt: *"if interrogations are fun, the
> game works; if not, nothing else rescues it."* The engine is the well-understood, objective part;
> the game's three original systems (clue routing, narrative state, interrogation) are the real risk
> and the actual product — and they are **design work that needs a human, not agents** (the #98
> entry point #101 is a paper playtest only a person can judge). Its sub-issues are decomposed under
> #98 (#101–#105).

**Recommended sequence:** finish the engine backlog above (the team, in priority order — start with
the 🔴 items), which is objective and closable cold; the Layer-5 game (#98) advances when the owner
runs the Phase-1 playtest and drives the design.

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
