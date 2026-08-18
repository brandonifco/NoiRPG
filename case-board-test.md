# Case-Board Paper Test — "The Overpass"
*Draft v0.1 — 2026-08-17. Executes development-plan.md Phase 1 item 3: one small case run through the pin-and-connect deduction loop, with the Three Doors rule exercised against different builds. The case itself lives in `cases/overpass.yaml`; the analytical audit below has been run (by `tools/case_validator.py`), the live tabletop session still needs players.*

## Purpose

Two questions, in order of importance:
1. **Is the board loop fun on paper?** Pinning suspects and evidence, drawing string, and committing to an accusation must carry a session before any engine work makes it prettier.
2. **Does the Three Doors rule hold under real builds?** Every core clue must be reachable for any background package — through skill doors when the build has them, through interrogation records and fallbacks when it doesn't, with fallback costs that feel like noir, not punishment.

## The Case in Brief

Payroll manager Daniel Reyes, found shot in his sedan under the Route 9 overpass, ruled probable suicide. The truth (facilitator's eyes only): CFO Marla Voss runs a fake-vendor skim, security contractor Gil Hartney was cut in to keep the depot logs clean, and Hartney staged the suicide when Reyes got close. Petra Okafor tipped Reyes off and is terrified; the estranged wife's insurance policy is the authored red herring. Five core clues form the spine; the accusation form asks for killer, orchestrator, proof of staging, and motive.

## Materials

- **Evidence cards** — one per item in `overpass.yaml` (20 items): name, one-line content, treatment note. Index cards are fine; write the id in the corner.
- **Suspect cards** — four, with portrait silhouette placeholder, role, and visible composure/stonewall numbers.
- **The board** — corkboard or table surface, string or drawn lines, pins.
- **Door sheets** (facilitator only) — per core clue, the doors from the YAML: which skill at which location yields which card, and what each fallback costs.
- **Build sheets** — the audit builds below; the playtester picks one blind.
- **d100** for texture rolls at doors (per the clue rule: the roll grades margin, never reachability).

## Procedure

1. Playtester picks a build sheet without seeing the case.
2. Facilitator deals the opening state: the hook, the desk location list, the crime-scene photos and coroner's preliminary as face-down cards at their locations.
3. Play in turns: the player names a location and an approach ("I take the ledgers to my desk and work them — Accounting"). If a door matches and the build clears `min_rating` 40, the card is handed over — then a d100 against the skill grades texture (special: facilitator volunteers the texture line; failure: the card comes with a complication, never nothing). If no door matches, the facilitator notes it and offers nothing — dead approaches must feel like dead approaches.
4. Every earned card gets pinned; the player may draw connections at any time and must narrate each string aloud ("this payment line connects Hartney to Kestrel").
5. Interrogation doors are abstracted for this test: the statement card lands on the record automatically (per interrogation-design.md, the record is guaranteed); the full interrogation loop is a separate prototype.
6. Fallbacks fire on their triggers; the facilitator states the cost out loud ("two days pass — Hartney's guard is up now").
7. The session ends when the player commits the accusation form or concedes.

Run at minimum: one session with the ex-cop build, one with the ex-accountant, one with the ex-soldier (the known stress case — see below). Different playtesters if possible; a repeat playtester must take a different build.

## Success Criteria (gate for the board loop)

1. The player narrates connections in fiction terms ("Voss signed Kestrel's checks"), not system terms ("this card links to that card") — the board is carrying deduction, not matching.
2. At least one wrong theory forms and is *dismantled by evidence* rather than by hint — the red herring should tempt someone and lose.
3. The accusation moment has weight: the player hesitates, checks the board, commits.
4. Fallback costs read as story ("I lost two days and Petra's scared now"), not as taxes.

**Kill criteria:** if players fill the board by exhaustively visiting locations rather than reasoning about where doors *should* be, the loop is a checklist, not deduction — redesign the door-location legibility before engine work.

## Three Doors Build Audit — RESULTS (machine-run)

`tools/case_validator.py` walks every core clue for each build: skill door at `min_rating` 40+ first, else interrogation record, else fallback.

| Core clue | Ex-cop | Ex-accountant | Ex-soldier |
|---|---|---|---|
| cc1 staged suicide | First Aid 45, coroner | Research 65, coroner | First Aid 50, coroner |
| cc2 the skim | Petra's record | Accounting 70, depot | Petra's record |
| cc3 Voss owns shell | Streetwise 65, pawn row | Accounting 70, depot | **fallback** (decay: Petra's fear) |
| cc4 Hartney at scene | Streetwise 65, tow lot | Persuade 50, gas station | **fallback** (time: police file) |
| cc5 the cut | Voss's record | Accounting 70, depot | Voss's record |

**Findings:**

1. **The rule held, and the audit already improved the case.** Audit v1 caught two build holes that were fixed in the data: cc1's original doors were both forensic/physical (Photography, First Aid) — a desk build had no route until a Research door was added at the coroner's office; cc4 had no door for non-street, non-photo builds until the Persuade door at the gas station was added. This is the authoring loop working as designed: audit → hole → new door → re-audit.
2. **Open finding — the ex-soldier is fallback-heavy** (2 of 5 core clues, flagged by the validator as a warning). Playable — that's what fallbacks are for — but a soldier playthrough of this case is slower and pays decay costs others don't. Two remedies to choose between, deliberately not auto-applied: add doors for soldier skills (an Intimidate door on the tow-lot attendant, a Spot door casing the overpass for the camera), or bump Streetwise in the soldier package so ex-military reads as street-capable. **Recommendation:** decide at the live table — if the soldier session still feels like detective work, fallback-heavy is a legitimate texture ("the blunt instrument waits for the file to catch up"); if it feels like waiting, add the doors.
3. **Codified into SCHEMA.md as a guideline:** each background package's top three skills should open at least two doors per case. The validator warns on fallback-heavy builds automatically, so this check runs on every future case for free.

## What This Test Does Not Cover

The interrogation loop (own prototype, own doc), cross-case junctions (needs two cases; `overpass.yaml` declares two junction stubs against future cases), and decay pacing at real session length. Live-table results should be appended to this doc under a Results heading.
