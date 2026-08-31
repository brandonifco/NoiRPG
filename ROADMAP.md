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

## You are here (2026-08-31)

**Engine — Layers 0–3 complete, Layer 4 nearly complete.**

| Layer | What | Status |
|---|---|---|
| 0 | dice / entropy / resolution kernel / modifier pipeline | ✅ done |
| 1 | abilities: characteristics-as-data, rolls, recomputing derived values | ✅ done |
| 2 | skills: definitions, specialties, registry | ✅ done |
| 3 | characters: aggregate, point-buy, tick-on-use experience | ✅ done |
| 4 | combat/gear/spot rules: range bands (#21), combat round (#47), attack/defense matrix (#49), gear, damage (#52), **situational spot rules (#50)** | 🚧 nearly done — see next |
| 5 | the noir game: cases, clue-routing, interrogation | ⬜ not started |

**Game — Phase 0/1 partially done on paper** (interrogation design, a paper case, the case
schema, advancement sim), **Phase 2+ (game code) not started.**

---

## What to build next (ordered)

### 1. Finish Layer 4 (engine — small, well-specified, `ready` now)
- **[#96] Injury/environmental spot rules** — falling, poison + antidotes, disease ladder. The
  sibling of #50 (damage/drain, not roll modifiers). Builds on damage #52. `ready`.
- **[#97] Fumble tables** — the printed fumble-results table as data + resolver; #10/#49/#50 all
  reference it. `ready`.

Either can be picked up cold by the **engine team** (engine-dev implements; rules-extractor,
rules-conformance, scope-warden verify) — the same workflow that shipped #50.

### 2. Start Layer 5 — the game (the priority; `needs-design`)
- **[#98] Epic: the noir game layer (`Noir.Rules` + `Noir.Scenario`)** — case schema→code, the
  Three Doors clue-routing engine, narrative-state junction budget, and the **interrogation
  minigame**.

> **Read this before choosing.** `development-plan.md` is blunt: *"if interrogations are fun, the
> game works; if not, nothing else rescues it."* The engine is the well-understood part and it is
> nearly done; the game's three original systems (clue routing, narrative state, interrogation)
> are the real risk and the actual product. **#98 is the highest-value frontier.** Its first
> sub-issue is to confirm the Phase-1 paper gate (interrogation is fun on paper) before committing
> to heavy code — the assets to judge that already exist in the repo.

**Recommended sequence:** clear #96 and #97 to close out the engine (fast, low-risk, keeps the
combat surface complete), then commit the team to **#98**, decomposing it into `blocked_by`
sub-issues as `development-plan.md` Phase 1→2 describes.

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
