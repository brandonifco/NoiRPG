#!/usr/bin/env python3
"""Skill category bonuses for NoiRPG (docs/decisions/0006-skill-bonus-system.md).

Computes each background package's skill category bonuses from its characteristics,
then derives the BASE skill ratings that reproduce the authored effective ratings in
tools/case_validator.py.

The direction matters. The authored BUILDS numbers are treated as FINAL EFFECTIVE
ratings, and base ratings are derived by subtracting the bonus:

    base = effective - category_bonus

Doing it the other way round -- treating the authored numbers as base and adding a
bonus on top -- would shift every rating and silently change which doors open in
already-audited cases. This way the Overpass audit is preserved by construction,
while characteristics still drive skill ratings for any character the player builds.

Usage:
    tools/skill_bonus.py            # report bonuses and derived base ratings
    tools/skill_bonus.py --check    # verify effective ratings round-trip; exit 1 if not
"""

from __future__ import annotations

import argparse
import math

# --- Point-buy (source: Ch 2, "Point-Based Character Creation (Option)", p. 10) --
# The seven listed characteristics start at 10. 24 points to spend. DEX/INT/POW
# cost 3 per point; STR/CON/SIZ/CHA cost 1. Reducing below 10 refunds at the same
# rate. Range 3-21.
#
# EDU is not part of that seven-characteristic pool. Ch 2, "Education (Option)",
# p. 10 says the GM assigns it from age and background; players may modify that
# assigned value at 3 pool points per EDU point. These authored packages record only
# their GM-assigned EDU values, so their 24-point validation deliberately excludes it.
POINT_BUDGET = 24
COSTS = {"STR": 1, "CON": 1, "SIZ": 1, "CHA": 1, "DEX": 3, "INT": 3, "POW": 3}
POINT_BUY_CHARACTERISTICS = frozenset(COSTS)

# --- Category formulas (source: Ch 2, "Skill Category Bonuses (Option)", pp. 18-19)
# primary: +/-1 per point from 10
# secondary: +/-1 per 2 points from 10, magnitude rounded down
# negative: inverted primary
CATEGORIES = {
    "Combat": {"primary": "DEX", "secondary": ["INT", "STR"], "negative": []},
    "Communication": {"primary": "INT", "secondary": ["POW", "CHA"], "negative": []},
    "Manipulation": {"primary": "DEX", "secondary": ["INT", "STR"], "negative": []},
    "Mental": {"primary": "INT", "secondary": ["POW", "EDU"], "negative": []},
    "Perception": {"primary": "INT", "secondary": ["POW", "CON"], "negative": []},
    "Physical": {"primary": "DEX", "secondary": ["STR", "CON"], "negative": ["SIZ"]},
}

# --- The canonical 18, mapped to book categories ----------------------------------
# Assignments come from the book's Skill List by Category, via each NoiRPG skill's
# book equivalent. Intimidate is the only one with no book equivalent; Communication
# is the natural home.
SKILL_CATEGORY = {
    "Streetwise": "Mental",        # Knowledge (Streetwise)
    "Law": "Mental",               # Knowledge (Law)
    "Accounting": "Mental",        # Knowledge (Accounting)
    "First Aid": "Mental",         # First Aid is Mental in the source, not Manipulation
    "Insight": "Perception",
    "Research": "Perception",      # Perception, not Mental
    "Spot": "Perception",
    "Fast Talk": "Communication",
    "Persuade": "Communication",
    "Intimidate": "Communication",  # original skill; no book equivalent
    "Photography": "Manipulation",  # Art (Photography)
    "Locksmith": "Manipulation",    # Fine Manipulation
    "Firearms": "Combat",
    "Brawl": "Combat",
    "Shadow": "Physical",           # Stealth, used for tailing
    "Stealth": "Physical",
    "Dodge": "Physical",
    "Drive": "Physical",            # Physical in the source, not Manipulation
}

# --- Background packages ----------------------------------------------------------
# Each spends exactly POINT_BUDGET on the seven point-buy characteristics. EDU is a
# separate GM-assigned value (Ch 2, "Education (Option)", p. 10). Until Layer 3
# character creation assigns it from age and background, these audit fixtures use the
# neutral value 10 so existing package bonuses and derived base ratings stay intact.
# Shapes otherwise match the packages' fiction: the cop is a rounded generalist, the
# accountant trades physicality for INT/POW, and the soldier trades INT/CHA for
# DEX/STR/CON.
CHARACTERISTICS = {
    "ex-cop": {"STR": 12, "CON": 13, "SIZ": 12, "INT": 13, "POW": 10, "DEX": 12, "CHA": 12, "EDU": 10},
    "ex-accountant": {"STR": 8, "CON": 12, "SIZ": 11, "INT": 15, "POW": 12, "DEX": 10, "CHA": 12, "EDU": 10},
    "ex-soldier": {"STR": 14, "CON": 14, "SIZ": 12, "INT": 10, "POW": 11, "DEX": 14, "CHA": 9, "EDU": 10},
}

# Ch 2, "Skill Bonus Table", pp. 18-19. Each numbered secondary column entry is a
# falsification row for the Mental formula below, with INT and POW held at 10.
PRINTED_SECONDARY_BONUS_ROWS = {
    1: -4, 2: -4, 3: -3, 4: -3, 5: -2, 6: -2, 7: -1, 8: -1, 9: 0, 10: 0,
    11: 0, 12: 1, 13: 1, 14: 2, 15: 2, 16: 3, 17: 3, 18: 4, 19: 4, 20: 5,
    21: 5,
}

# The table's continuation row reads "+1%/2 points" after its numbered 1-21 rows.
# These cases pin the immediate parity boundary and a farther value beyond that range.
SECONDARY_BONUS_CONTINUATION_CASES = {22: 6, 23: 6, 40: 15}

# Authored effective ratings. Must stay in sync with BUILDS in case_validator.py.
EFFECTIVE = {
    "ex-cop": {"Streetwise": 65, "Intimidate": 65, "Firearms": 60, "Spot": 60,
               "Insight": 55, "First Aid": 45, "Fast Talk": 40, "Law": 35,
               "Photography": 30, "Accounting": 15},
    "ex-accountant": {"Accounting": 70, "Research": 65, "Law": 55, "Insight": 50,
                      "Persuade": 50, "Fast Talk": 45, "Photography": 25,
                      "First Aid": 25, "Streetwise": 20},
    "ex-soldier": {"Firearms": 70, "Dodge": 60, "Brawl": 55, "Spot": 55,
                   "First Aid": 50, "Intimidate": 50, "Drive": 45, "Stealth": 45,
                   "Streetwise": 35, "Persuade": 25},
}


def point_cost(chars: dict[str, int]) -> int:
    """Return the seven-characteristic pool cost, excluding GM-assigned EDU."""
    return sum((chars[name] - 10) * COSTS[name] for name in POINT_BUY_CHARACTERISTICS)


def signed_half(delta: int) -> int:
    """+/-1 per 2 points from 10, magnitude rounded down."""
    return int(math.copysign(abs(delta) // 2, delta)) if delta else 0


def category_bonus(chars: dict[str, int], category: str) -> int:
    spec = CATEGORIES[category]
    bonus = chars[spec["primary"]] - 10
    for name in spec["secondary"]:
        bonus += signed_half(chars[name] - 10)
    for name in spec["negative"]:
        bonus -= chars[name] - 10
    return bonus


def bonuses_for(build: str) -> dict[str, int]:
    chars = CHARACTERISTICS[build]
    return {cat: category_bonus(chars, cat) for cat in CATEGORIES}


def base_ratings(build: str) -> dict[str, int]:
    """Derive base ratings that reproduce the authored effective ratings."""
    bonus = bonuses_for(build)
    return {
        skill: rating - bonus[SKILL_CATEGORY[skill]]
        for skill, rating in EFFECTIVE[build].items()
    }


def report() -> None:
    for build in CHARACTERISTICS:
        chars = CHARACTERISTICS[build]
        cost = point_cost(chars)
        flag = "" if cost == POINT_BUDGET else f"  <-- SPENDS {cost}, BUDGET {POINT_BUDGET}"
        print(f"\n{build}{flag}")
        print("  " + "  ".join(f"{k} {v}" for k, v in chars.items()))
        bonus = bonuses_for(build)
        print("  bonuses: " + "  ".join(f"{c} {v:+d}" for c, v in bonus.items()))
        print(f"  {'skill':<12} {'base':>5} {'bonus':>6} {'effective':>10}")
        for skill, eff in sorted(EFFECTIVE[build].items(), key=lambda kv: -kv[1]):
            cat = SKILL_CATEGORY[skill]
            print(f"  {skill:<12} {eff - bonus[cat]:>5} {bonus[cat]:>+6} {eff:>10}")


def check() -> int:
    """Base + bonus must reproduce the authored effective ratings exactly."""
    failures = []
    for build in CHARACTERISTICS:
        if set(CHARACTERISTICS[build]) != POINT_BUY_CHARACTERISTICS | {"EDU"}:
            failures.append(f"{build}: must define the seven point-buy characteristics and EDU")
        cost = point_cost(CHARACTERISTICS[build])
        if cost != POINT_BUDGET:
            failures.append(f"{build}: spends {cost}, budget is {POINT_BUDGET}")
        bonus = bonuses_for(build)
        for skill, base in base_ratings(build).items():
            got = base + bonus[SKILL_CATEGORY[skill]]
            want = EFFECTIVE[build][skill]
            if got != want:
                failures.append(f"{build}/{skill}: {got} != {want}")
    for skill in EFFECTIVE["ex-cop"] | EFFECTIVE["ex-accountant"] | EFFECTIVE["ex-soldier"]:
        if skill not in SKILL_CATEGORY:
            failures.append(f"{skill} has no category")
    for edu, expected in PRINTED_SECONDARY_BONUS_ROWS.items():
        got = category_bonus({"INT": 10, "POW": 10, "EDU": edu}, "Mental")
        if got != expected:
            failures.append(f"Mental/EDU {edu}: {got:+d} != printed secondary bonus {expected:+d}")
    for edu, expected in SECONDARY_BONUS_CONTINUATION_CASES.items():
        got = category_bonus({"INT": 10, "POW": 10, "EDU": edu}, "Mental")
        if got != expected:
            failures.append(f"Mental/EDU {edu}: {got:+d} != continuation secondary bonus {expected:+d}")
    if failures:
        print("FAIL")
        for line in failures:
            print("  " + line)
        return 1
    print("OK: seven-characteristic point-buy totals correct; Mental reproduces all 21 numbered EDU secondary rows")
    print("    -> Mental also passes EDU continuation checks at 22, 23, and 40")
    print("    -> base + bonus reproduces every authored rating")
    print("    -> door coverage in audited cases is unchanged by construction")
    return 0


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("--check", action="store_true")
    args = parser.parse_args()
    raise SystemExit(check() if args.check else (report() or 0))
