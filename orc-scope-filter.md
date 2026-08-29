# ORC Content Document — Scope Filter for NoiRPG
*What we implement, what we cut. Source: `BasicRoleplaying-ORC-Content-Document.pdf` — Basic Roleplaying: Universal Game Engine, Chaosium 2023, 303 pp.*

## Why this document exists

The ORC Content Document is 303 pages / ~37,000 lines of extracted text. NoiRPG is modern-day, no magic, no unrealistic technology, one player-created protagonist, investigation-forward. **Roughly 60% of the book is inapplicable** — magic, sorcery, superpowers, psychic powers, mutations, fantasy weapons and armor, spacecraft, and a bestiary of monsters. Implementing it would be the single largest source of wasted effort in this project.

This filter is the authority on what enters the engine. If a rule isn't listed as IN below, it doesn't get written.

---

## Chapter-level verdict

| Ch | Title | Lines | % of book | Verdict |
|---|---|---:|---:|---|
| 1 | Introduction | 163 | 0.4% | **Reference only** — terminology glossary is useful for naming things consistently in code. |
| 2 | Characters | 2,264 | 6.1% | **IN, mostly** — 10-step creation, characteristics, derived stats, professions. |
| 3 | Skills | 2,017 | 5.4% | **IN, filtered** — keep the modern/investigative skills, drop the rest. |
| 4 | **Powers** | 4,992 | **13.5%** | **CUT ENTIRELY.** Magic, sorcery, psychic powers, superpowers, mutations. Nothing here applies. |
| 5 | **System** | 2,770 | 7.5% | **IN — this is the core.** Resolution, difficulty, opposed rolls, resistance table, experience. |
| 6 | **Combat** | 1,537 | 4.1% | **IN, mostly.** |
| 7 | Spot Rules | 1,700 | 4.6% | **IN, filtered** — keep environmental/injury rules, drop the fantastical ones. |
| 8 | **Equipment** | 12,153 | **32.8%** | **CUT ~80%.** Largest chapter in the book and mostly fantasy/sci-fi gear tables. Keep the modern subset. |
| 9 | Gamemastering | 1,065 | 2.9% | **Reference** — informs the scenario-authoring model and the adjudication ports, not the rules engine. |
| 10 | Settings | 1,713 | 4.6% | **CUT ~97%** — but read the **Noir** and **Modern** entries (~40 lines). See below. |
| 11–12 | Creatures & Appendices | 6,632 | 17.9% | **CUT ~90%.** Keep the NPC-construction methodology and ordinary real animals. |

**Net: about 40% of the book becomes engine work.** That is the single most useful number in this document.

---

## Modern noir: Noir tone, Modern era baselines

Chapter 10 has both a **Noir** entry and a **Modern** entry. NoiRPG is neither one cleanly — it takes **tone and optional rules from Noir, technology and skill baselines from Modern**. This is not a flavor note; BRP's skill base chances are era-dependent, so the split has direct mechanical consequences.

**From the Noir entry** — its recommended optional rules are **Education/Knowledge Roll, Increased Personal Skill Points, and Passions**, and its Powers line is *usually none*. Two consequences:

1. **Passions are the sanctioned mechanism for the framework's "vices, obligations, loyalties."** Don't invent a parallel system; build on Passions.
2. **Increased Personal Skill Points** is the sanctioned lever for "the protagonist is a competent professional, not a novice" — which the framework wants and which plain character creation underdelivers.

**From the Modern entry** — which explicitly covers thrillers and permits realistic technology. This is where the era baselines come from. Concretely, several skills in the book carry *two* base chances, one modern and one historical, and **NoiRPG always takes the modern value**:

| Skill | Modern base | Historical base |
|---|---|---|
| Drive | 20% | 01% |
| First Aid | 30% | INT×1 |
| Knowledge (any) | 05% | 01% |
| Medicine | 05% | 00% |
| Literacy | universal — treat as automatic, no roll | varies |

Getting this wrong silently makes every starting detective worse than intended, and it's the kind of error that only surfaces after balance testing.

**Further consequences of "modern," each of which changes what gets implemented:**

- **Technical Skill (Computer Use), Technical Skill (Security Systems), and Repair (Electronic) are live, frequently-used skills**, not exotica. A present-day PI works phone records, databases, cameras, and alarm panels. Under a 1940s reading these would have been cut.
- **Research means databases and the internet**, not only newspaper morgues. Same skill, much wider applicability — which matters because Research is likely to be the single most-rolled skill in the game.
- **Science specialties skew forensic** — Chemistry, Biology, and a Forensics specialty — rather than the book's default spread.
- **Firearms**: modern handguns, shotguns, and submachine guns. Not period-only revolvers.
- **Art (Photography)** is a phone camera as often as a telephoto lens.
- The Noir entry's *"still traumatized by World War II"* framing is discarded outright. Composure/Corruption needs a present-day source of erosion — moral compromise, violence, betrayal — which is what the framework already specifies.

**One thing the Noir entry supplies that Modern does not**: a stated tonal contract — urban, bleak, world-weary, morally compromised, betrayal-driven, "no way out" scenarios where ordinary people resort to desperate measures. That is a design constraint worth keeping verbatim in the scenario-authoring model, because it's a test any authored case can be checked against.

---

## Chapter 4: Powers — cut in full

Nothing in this chapter enters the codebase: magic, sorcery, spells, divine/rune magic, psychic abilities, superpowers, mutations, and the `Projection` skill that exists only to serve them.

**Downstream deletions this forces**, which are easy to miss because they're scattered outside Chapter 4:
- **Power Points** as a spendable resource. POW still exists as a characteristic (it drives the Luck roll and POW-vs-POW resistance rolls); the *spendable pool* goes.
- `Projection` skill.
- "Starting Equipment with Powers" (Ch 8).
- Power-point reservoirs in items, enchanted gear, artifacts.
- **Fate Points** — an optional rule that lets players spend power points to adjust dice. Interesting for a video game, but it's built on the power-point economy we're deleting, so it would need re-basing on a different currency. *Defer; don't cut on principle.*
- All power-related entries in the creature statblocks.

Per the user's direction: no superheroes, no cyborgs, no galactic knights, no sorcerers. This chapter is where all four live.

---

## Chapter 3: Skills — the filter

**IN** — investigation and social core: Appraise, Art (Photography), Bargain, Command, Disguise, Drive, Etiquette, Fast Talk, Fine Manipulation, First Aid, Gaming, Insight, Knowledge (Law / Streetwise / Group / Region / Politics / Accounting-as-specialty), Language, Listen, Medicine, Navigate, Perform, Persuade, Psychotherapy, Repair (Electrical/Electronic/Mechanical), Research, Science (Chemistry/Forensics-as-specialty), Sense, Sleight of Hand, Spot, Status, Stealth, Strategy, Teach, Technical Skill (Computer Use / Electronics / Security Systems), Track, Hide, Climb, Jump, Swim, Throw.

**IN** — combat, deliberately minimal: Brawl, Grapple, Dodge, Firearm (Handgun / Shotgun / Rifle), Melee Weapon (Knife / Club), Martial Arts.

**CUT**: Artillery, Energy Weapon, Fly, Heavy Machine, Heavy Weapon, Missile Weapon (bows/crossbows), Pilot (aircraft/spacecraft), Projection, Ride, Shield, Literacy (universal in a modern setting — treat as automatic).

**Skill naming: the framework's names win.** `noir-rpg-framework.md` uses an
18-skill list including *Streetwise, Shadow, Intimidate, Law, Accounting, Photography,
Locksmith*. Four of those map onto existing book skills under different names:

| Framework name | Book equivalent |
|---|---|
| Shadow (tailing) | `Stealth`, opposed by `Spot` |
| Locksmith | `Fine Manipulation` (the book names lockpicking explicitly) |
| Accounting | `Knowledge (Accounting)` specialty |
| Photography | `Art (Photography)` specialty |
| Law | `Knowledge (Law)` specialty |
| Intimidate | No direct equivalent — an original skill |

An earlier draft of this document recommended adopting the book's names in the engine
and aliasing the framework names for display. **That is wrong and is withdrawn.**
`tools/case_validator.py` hardcodes the canonical 18 as the framework names, and
`cases/overpass.yaml` and the paper-kit build sheets are authored against them. The
engine must use the framework names as canonical and map *inward* to the book's skill
definitions, not the reverse. Existing tooling and authored content win over
nomenclatural tidiness.

---

## Chapter 8: Equipment — keep the modern slice

**IN:**
- **Money and Wealth levels** — a clean abstraction (Destitute → Wealthy) that suits a game where the PI's finances are a story element, and it ties to the `Status` skill.
- General equipment rules: starting equipment, purchasing, equipment quality modifiers.
- The **Skills-and-Equipment mapping** — which gear enables or bonuses which skill. Directly useful for "do you have what you need to work this scene?"
- **Modern firearms**: handguns, shotguns, rifles, submachine guns. Ranges, damage, malfunction, reload.
- **Modern armor**: ballistic vests, and the skill penalties armor imposes.
- **Improvised weapons**, knives, clubs.
- **Poisons** (POT vs CON on the resistance table) and modern drugs.
- **Vehicles**: cars only.
- Item SIZ/hit points for breaking doors, windows, locks.

**CUT:** all pre-modern melee weapons, all archery, all fantasy and historical armor, energy/laser weapons, artillery, heavy weapons, explosives beyond basic, aircraft, watercraft, spacecraft, and every enchanted or powered item.

This chapter is a third of the book and perhaps a fifth of it survives. **Do not transcribe it wholesale into ruleset JSON** — hand-pick the entries a noir detective could plausibly encounter. A dozen firearms and three armor types is the realistic target, not two hundred rows.

---

## Chapters 11–12: Creatures — near-total cut

**CUT:** the entire fantasy and science-fiction bestiary. No monsters, no demons, no aliens, no nonhuman player races.

**IN:**
- The **NPC-construction methodology** ("Customizing Creatures and Nonplayer Characters"). This is the actual value in the chapter — it's how suspects, informants, thugs, and witnesses get statted, and NoiRPG needs dozens of them.
- Ordinary real animals: **dogs** (guard dogs are a genuine noir obstacle). Possibly rats. Nothing else.

---

## Optional rules — the toggle list

The book ships a checklist of every optional rule. This is architecturally important: **it confirms the engine should be built as a core plus toggleable modules**, and the book has already done the work of enumerating them. Encode the checklist as the ruleset's feature-flag surface.

### ON for NoiRPG
| Rule | Why |
|---|---|
| **Point-based Character Creation** | The framework already committed to point-buy over rolling. The book supports it natively. |
| **Choosing Characteristic Values** | Same. |
| **Education/Knowledge Roll** | Recommended by the book's own Noir entry; suits a modern investigator. |
| **Increased Personal Skill Points** | Recommended for Noir. Makes the PC a competent professional at start. |
| **Passions** | Recommended for Noir. The basis for vices, obligations, loyalties. |
| **Sanity** | The basis for Composure/Corruption. |
| **Complimentary Skills** | Lets one skill augment another (+1/5 rating). Excellent for investigation: `Knowledge (Accounting)` assisting `Research` on a ledger is exactly the multi-route clue access the review notes demand. |
| **Distinctive Features** | Cheap flavor, drives portrait variation. |
| **Damage and Hit Locations** + **Armor by Hit Locations** | Delivers the framework's persistent visible injury — "the detective working a case with an arm in a sling." |
| **Major Wounds** | Same. Central to "danger never retires." |
| **Freeform Professions** | Needed for custom background packages. |
| **Skill Ratings Over 100%** | Cheap to support; the results table already extends past 100. |

### OFF
Nonhuman Characters, Higher Starting Characteristics, Cultural Modifiers, Projection, Allegiance (gods actively intervening — no), Fatigue Points *(defer — could serve noir exhaustion, but adds a tracked resource)*, Encumbrance, Miniatures/Maps/VTT, Dodging Missile Weapons, Attacks and Parries over 100%, Dying Blows *(defer — arguably good for a lethal noir climax)*, Aging and Inaction, Total Hit Points, Initiative Rolls.

### Undecided — needs a call
- **Skill Category Bonuses vs. Simpler Skill Bonuses** — mutually exclusive; the book says so explicitly. Category bonuses add characteristic-derived modifiers per skill category. Pick one before Layer 2 is written, because it changes how a skill's effective rating is computed for every skill in the game.
- **Fate Points** — needs re-basing off power points (see Ch 4 cut).
- **Acting Without Skill** — a minor chance at untrained skills. Interacts directly with the framework's clue rule; the book warns it can break plausibility.

---

## What this changes in the implementation plan

`engine-implementation-plan.md` was written against the 23-page BRP SRD 1.0.2 and its §0 and §2 are now wrong. Corrected there; summarized here:

1. **Five degrees of success, not four.** Fumble < Failure < Success < Special < Critical.
2. **The resolution formulas differ from 1.0.2** and are re-derived and verified against all 24 rows of this book's Skill Results Table.
3. **No Prohibited Content list.** ORC has no equivalent of the BRP-OGL's restriction, and Sanity, Passions, Allegiance, and Augments are all inside the licensed text. The earlier warning that Composure and Vices had to be original designs is withdrawn.
4. **No BRP logo obligation.** ORC requires an attribution notice; the document requests a "Powered by BRP" credit. This is materially lighter than BRP-OGL §15.
5. **The characteristic is CHA**, not APP. The framework doc was right.
6. Hit locations, major wounds, chases, fatigue, and Augments all exist and are available.
