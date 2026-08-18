# Noir RPG — Design Framework
*Working document, v0.1 — compiled from design discussions, August 2026*

## Overview

A modern-day noir action/mystery RPG video game with no magic and no unrealistic technology. The game is built on the Basic Roleplaying (BRP) engine under the ORC license, presented in black and white, and designed to be deeply engaging while requiring as little bespoke art content as possible. Realism in skills, advancement, and abilities is a secondary but important goal; fun comes first, and the design leans on the fact that BRP's grounded mechanics generate tension naturally rather than fighting the fun.

## Design Pillars

Four commitments that every later decision should be tested against. First, fun first, realism close behind: the system should feel believable without ever stalling play. Second, the player's mind is the content: deduction, reading, and decision-making carry the game, not rendered spectacle. Third, danger never retires: a revolver is as lethal in the final chapter as in the first, because hit points do not grow. Fourth, failure is a branch, not a wall: losing produces consequences and new story, not game-over screens.

## System Foundation: BRP under ORC

The mechanical skeleton is Chaosium's *Basic Roleplaying: Universal Game Engine* (2023), released under the ORC (Open RPG Creative) license. ORC permits commercial use and adaptation of the licensed mechanics in any medium, including video games, with no permission, royalties, or approval process. Compliance requires including an ORC Notice attributing the source material.

Boundaries to respect: the license covers mechanics, not branding — the BRP logo and Chaosium trademarks are not included, so the game is "built on ORC-licensed material," not "a BRP product," unless a separate logo license is pursued. Call of Cthulhu and RuneQuest are not ORC-licensed; the Universal Game Engine book is the sole legal source text. Any mechanics we publish become ORC-licensed in turn, while our setting, story, characters, writing, art, and code remain fully owned reserved material.

## Core Resolution

Characters are defined by seven characteristics (Strength, Constitution, Size, Intelligence, Power, Dexterity, Charisma) and a trimmed list of percentile skills. To attempt an action, the game rolls d100 against the skill rating; rolling at or under succeeds. The rating is the displayed probability — a 65% Streetwise check shows the player exactly 65% — which gives the transparency players know from XCOM-style games with none of the hidden math.

Rolls at or under one-fifth of skill are special successes; at or under one-twentieth are criticals. These grades are content hooks: a normal success, a special, and a failure on the same check can each route to different scene outcomes. Opposed situations (tailing versus noticing, interrogating versus stonewalling) compare success levels between two rolls.

The working skill list is period-flavored and investigation-forward: Streetwise, Shadow (tailing), Insight, Fast Talk, Intimidate, Persuade, Law, Accounting, Photography, Locksmith, Research, First Aid, Firearms, Brawl, Dodge, Drive, Stealth, Spot.

## Advancement

Advancement uses BRP's experience-check system, which a computer runs better than any table can. During a case, a skill that succeeds under real stakes gets ticked — once per case maximum, and only in scenes that carry consequences, never in free-roam spam. When the case closes, an improvement roll is made for each ticked skill: d100 rolled *above* the current rating raises the skill a few points. Low skills improve almost every time; a 90% skill almost never budges. Diminishing returns and learn-by-doing in a single elegant roll, shown on the case-closed screen as a small moment of drama.

This structure makes grinding mathematically pointless, which addresses the classic failure mode of use-based advancement in video games. Between cases, a downtime layer lets the player train one or two chosen skills (the range, the law library, drying out), providing the deliberate-growth valve that pure use-based systems lack. A small pool of milestone points for characteristics and perks covers the remaining blind spot.

## Combat, Lethality, and Failure

Hit points derive from Constitution and Size and never increase with experience. Armor reduces damage rather than preventing hits. Guns are terrifying at every stage of the game. Experienced characters survive through better Dodge, better positioning, and better judgment — not thicker health bars.

Because lethality plus save-scumming is a dull loop, defeat is designed as narrative consequence in the noir tradition: getting blackjacked and waking in an alley minus the evidence, being arrested, being warned off the case, owing a favor to the wrong people. True death is reserved for ignoring several escalating warnings. The major-wound module is used so injuries persist visibly across scenes — the detective working a case with an arm in a sling is a feature, not a punishment.

## Investigation and the Clue Rule

One deliberate deviation from pure BRP, borrowed from GUMSHOE's design philosophy: a mandatory core clue is never gated behind a failable roll. Having the right skill in the right place always yields the clue needed to keep the case moving. The d100 roll instead determines texture and margin — the extra lead, the witness who volunteers more, the clean exit versus the noisy one. Rolls create consequence; they never stall the plot. This eliminates the reload-scumming and stalled-mystery failure modes in one rule.

Deduction itself is mechanical, not narrated. The player pins suspects and evidence to a case board, draws connections, and commits to accusations, in the tradition of Return of the Obra Dinn and The Case of the Golden Idol. Skill checks determine the quality of evidence extracted; the player's reasoning does the rest.

## Noir Modules

BRP's optional subsystems are re-skinned for the genre. The sanity mechanic becomes Composure (or Corruption): moral compromises, violence, and betrayals erode it, and its level can gate dialogue options, distort presentation, and unlock darker choices. Passions and allegiances become vices, obligations, and loyalties with real mechanical weight. The chase rules become structured pursuit sequences on foot or by car, built from opposed-roll ladders.

## The Detective

The protagonist is player-created. Character creation is built directly from BRP: the player allocates the seven characteristics (point-buy for fairness rather than rolled), chooses a background/former-occupation package that sets starting skill ratings (ex-cop, ex-journalist, ex-lawyer, ex-soldier, ex-accountant — each opening different investigative approaches), and picks starting vices and obligations from the noir modules. This makes character creation itself the first meaningful gameplay: the skill spread determines how cases can be approached, and replayability comes from builds as much as from branches. Consequence for presentation: the first-person narrator voice must be written build-agnostic or delivered as unvoiced internal monologue text, with the voice-acting budget shifted to suspects, informants, and the city itself.

## Game Structure

The game is an open city containing multiple cases. The city is presented as the stylized map — a hub of unlockable locations, not simulated streets — and several cases can be open at once, each with its own board, suspects, and evidence set. Cases can intersect: a witness in one is a suspect in another, and evidence pinned on one board can connect to a second. The player chooses which threads to pull, and time spent on one case can let another develop or decay. Skill improvement rolls run when a case closes; downtime training is woven into city life (locations that train skills) rather than sequestered in interludes.

Scope discipline is the critical risk of this structure: the open city must remain a map of nodes and cases-as-data, never a demand for modeled streets and ambient population. The city feels big through documents, radio, phone calls, and cross-case connections — not through rendered geography.

Interrogations are turn-based gameplay — statement by statement, with Insight and Fast Talk checks against a suspect-composure meter — rather than scrolling dialogue trees. Play alternates constantly between modes (read a document, make a check, pin evidence, place a call, drive the map, interrogate) so no single mode fatigues.

## Interface: The Detective's Desk

The entire game frame is one diegetic interface — a modern private investigator's desk rendered once: case folders, a corkboard evidence wall with string, a smartphone, a laptop, a voice-recorder app, a coffee-stained city map. Every screen is an object on that desk. The setting is genuinely modern, so the document pool is modern paper and screens presented as artifacts: printouts, bank statements, phone-record subpoenas, text-message transcripts, social media screenshots, bodycam stills, a coroner's PDF with a coffee ring on the printout. Text reads as objects and interaction, never as walls of prose — and real modern PIs still drown in paper, so the desk metaphor holds without anachronism. Navigation runs through a single stylized city map used throughout the game. Reading is largely self-directed through search, rolodex, and file structures rather than pushed at the player.

## Art Direction: Black and White

The game is black and white throughout, governed by a three-rule style bible. Rule one: the world and its lighting are always silvery film-noir grayscale — full tonal range, grain, deep shadows, light doing the storytelling (venetian-blind slats, desk-lamp pools, headlight sweeps — shader work, not drawn assets). Rule two: documents and photographs always carry a degraded-reproduction treatment — the modern-day equivalent of halftone: photocopy grain, laser-toner banding, scan lines, compression artifacts on screen-captured images. Same design job as newsprint (making paper and photos feel like physical evidence that has passed through machines and hands), but native to the era; classic halftone can survive on genuinely printed items like newspapers and flyers. Rule three: character portraits are always high-contrast ink silhouettes, varied by palette and props. One look per layer, applied without exception; the layers cohere because all three textures coexisted in real print culture. Every future asset either follows its layer's rule or is rejected.

Information hierarchy is carried by value and weight instead of color: a defined scale of four or five gray levels with fixed meanings, bright white for what matters now, enforced like a palette. This is inherently colorblind-safe. One open decision, to be made once and kept rare: whether to reserve a single accent color as the game's loudest signal, or stay pure black and white and let animation and sound carry that signaling. Legibility beats drama wherever they conflict; blacks stay slightly lifted or a softer reading mode is offered, since the game asks for hours of reading.

## Audio

The real production budget goes to sound, which buys more atmosphere per dollar than art: rain, neon buzz, saxophone, tape hiss, footsteps, a lighter clicking. Selective voice acting rather than full VO — a hard-boiled first-person narrator recorded like a radio drama, plus voiced interrogation suspects — carries the emotional load. A game that sounds rich never feels like a text game.

## Minimal Asset Budget

The complete bespoke art scope, as currently designed: the desk interface and its props; one stylized city map; a case-board view; an interrogation view; a few dozen document templates; ink-silhouette portraits with prop variations; one treated location card per location type, reused with grading and weather overlays; typography, which is treated as a first-class art asset (typewriter animation, redaction bars, stamps). Total bespoke assets on the order of one hundred. Proven precedents for this scale of production: Her Story, Papers, Please, The Case of the Golden Idol, Return of the Obra Dinn, 80 Days.

## Decisions Made

Era: genuinely modern day — modern documents, phones, and forensics, with the noir carried by lighting, black and white treatment, and tone rather than period props. Protagonist: player-created via BRP point-buy with background packages; narrator voice is build-agnostic or unvoiced. Structure: open city with multiple concurrent, intersecting cases, built as map nodes and cases-as-data.

## Open Questions

Accent color: pure black and white versus one reserved signal color. Distribution: personal project versus commercial release, which sets how formally the ORC Notice and licensing review need to be handled. Platform and engine: undecided. Time pressure model: whether neglected cases decay in real gameplay consequences or simply wait — this tunes how much pressure the open structure applies.
