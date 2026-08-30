#!/usr/bin/env python3
"""Advancement simulation for NoiRPG (design-review-notes.md, point 5).

Simulates BRP experience-check advancement over a playthrough of cases to
answer: does the player ever *feel* their skills improve at video-game length?

Model
-----
A character has skills in three build tiers (primary/secondary/tertiary) set by
their background package. Per case, each skill is exercised under real stakes
with a tier-dependent probability (players lean into their build). A skill
gets at most one tick per case. At case close, each ticked skill makes an
improvement roll: d100 strictly greater than the current rating raises the
skill by 1d6, except that a skill at or above 100 uses a fixed threshold of
100 instead of its own rating (Ch 5: System, "Exceeding 100% in a Skill",
p.138 — "any roll of 100 or over earns a skill improvement"; an unmodified
d100 can never again beat a rating that has itself reached 100). This mirrors
`Brp.Rules.Advancement.ExperienceSystem.ImprovementRoll`, kept in sync so this
simulation and the engine share one improvement rule. Between cases, downtime
training grants one chosen skill an extra improvement roll (the player trains
their weakest primary).

Variants compared:
  A. RAW BRP  — a skill ticks only if it *succeeded* under stakes.
  B. Tick-on-use — exercising the skill under stakes ticks it, success or not.
Each variant is run with and without downtime training.
"""

import random
import statistics
from dataclasses import dataclass, field

CASES = (8, 12)          # playthrough lengths to report
TRIALS = 10_000          # characters simulated per scenario
GAIN_DIE = 6             # improvement: +1d6 on a successful improvement roll

# Build tiers: (starting rating, per-case chance the skill sees real stakes, count)
TIERS = {
    "primary":   (65, 0.90, 2),
    "secondary": (45, 0.60, 4),
    "tertiary":  (30, 0.30, 6),
}


@dataclass
class Skill:
    tier: str
    rating: int
    gained: int = 0

    def improvement_roll(self, rng: random.Random) -> None:
        roll = rng.randint(1, 100)
        # Ch 5 p.138, "Exceeding 100% in a Skill": once a rating reaches 100, an
        # unmodified d100 can never again roll strictly higher than it, so the book
        # pins the threshold at 100 itself ("any roll of 100 or over earns a skill
        # improvement") rather than at the skill's own, possibly much higher, rating.
        threshold = min(self.rating, 100)
        succeeded = roll >= 100 if threshold >= 100 else roll > threshold
        if succeeded:
            g = rng.randint(1, GAIN_DIE)
            self.rating += g
            self.gained += g


def run_character(rng, n_cases, tick_on_use, downtime):
    skills = [
        Skill(tier, rating)
        for tier, (rating, _, count) in TIERS.items()
        for _ in range(count)
    ]
    for _ in range(n_cases):
        for sk in skills:
            _, use_p, _ = TIERS[sk.tier]
            if rng.random() >= use_p:
                continue  # skill saw no real stakes this case
            succeeded = rng.randint(1, 100) <= sk.rating
            if succeeded or tick_on_use:
                sk.improvement_roll(rng)
        if downtime:
            trained = min((s for s in skills if s.tier == "primary"),
                          key=lambda s: s.rating)
            trained.improvement_roll(rng)
    return skills


def scenario(label, tick_on_use, downtime, n_cases, seed=1913):
    rng = random.Random(seed)
    by_tier = {t: [] for t in TIERS}
    best = []
    for _ in range(TRIALS):
        skills = run_character(rng, n_cases, tick_on_use, downtime)
        for t in TIERS:
            by_tier[t].append(
                statistics.mean(s.gained for s in skills if s.tier == t))
        best.append(max(s.gained for s in skills))
    cells = [f"{statistics.mean(by_tier[t]):5.1f}" for t in TIERS]
    feel = statistics.mean(b >= 15 for b in best) * 100
    print(f"  {label:<28}" + "".join(f"{c:>11}" for c in cells)
          + f"{statistics.mean(best):>11.1f}" + f"{feel:>10.0f}%")


def main():
    for n_cases in CASES:
        print(f"\n=== {n_cases}-case playthrough "
              f"(mean gain in points; {TRIALS} characters/scenario) ===")
        header = ["prim(65)", "sec(45)", "tert(30)", "best skill", "feel-it*"]
        print(f"  {'scenario':<28}" + "".join(f"{h:>11}" for h in header[:-1])
              + f"{header[-1]:>10}")
        scenario("A  RAW (tick on success)", False, False, n_cases)
        scenario("A+ RAW + downtime", False, True, n_cases)
        scenario("B  tick on use", True, False, n_cases)
        scenario("B+ tick on use + downtime", True, True, n_cases)
    print("\n*feel-it = share of runs where at least one skill gained 15+ "
          "points (a jump the player unambiguously notices).")


if __name__ == "__main__":
    main()
